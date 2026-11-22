using System.Text.Json;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Integrations;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Logistics;

public sealed class SapIntegrationService(
    ApplicationDbContext dbContext,
    ISapIntegrationClient sapClient,
    IClock clock,
    IOptions<SapOptions> sapOptions) : ISapIntegrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db = dbContext;
    private readonly ISapIntegrationClient _sap = sapClient;
    private readonly IClock _clock = clock;
    private readonly SapOptions _options = sapOptions.Value;

    public SapStatusView GetStatus()
    {
        var hint = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? null
            : _options.BaseUrl.Trim().TrimEnd('/');
        return new SapStatusView(_sap.IsEnabled, _sap.Mode, hint);
    }

    public Task<IReadOnlyList<SapMaterialDto>> ListRemoteMaterialsAsync(
        CancellationToken cancellationToken = default) =>
        _sap.ListMaterialsAsync(cancellationToken);

    public async Task<SapMaterialSyncResult> SyncMaterialsAsync(
        CancellationToken cancellationToken = default)
    {
        var remote = await _sap.ListMaterialsAsync(cancellationToken);
        var unitCode = string.IsNullOrWhiteSpace(_options.DefaultUnitCode)
            ? "UN"
            : _options.DefaultUnitCode.Trim().ToUpperInvariant();
        var unit = await _db.UnitsOfMeasure.SingleOrDefaultAsync(
            value => value.Code == unitCode,
            cancellationToken);
        if (unit is null)
        {
            unit = await _db.UnitsOfMeasure.OrderBy(value => value.Code)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new LogisticsException(
                    LogisticsErrorKind.Validation,
                    "AAA-SAP-0010",
                    "No hay unidad de medida para sincronizar materiales SAP.");
        }

        var created = 0;
        var updated = 0;
        var mapped = 0;
        var utc = _clock.UtcNow;
        foreach (var row in remote)
        {
            var code = row.MaterialNumber.Trim().ToUpperInvariant();
            var existing = await _db.Materials.SingleOrDefaultAsync(
                value => value.Code == code
                    || value.ExternalId == row.MaterialNumber,
                cancellationToken);
            if (existing is null)
            {
                var material = new Material(
                    code,
                    row.Description,
                    unit.Id,
                    0m,
                    "EUR",
                    row.MaterialNumber);
                _db.Materials.Add(material);
                await UpsertMappingAsync(
                    "Material",
                    row.MaterialNumber,
                    material.Id,
                    utc,
                    cancellationToken);
                created++;
                mapped++;
            }
            else
            {
                existing.Update(
                    row.Description,
                    existing.CatalogPrice,
                    existing.Currency,
                    existing.IsActive,
                    row.MaterialNumber);
                await UpsertMappingAsync(
                    "Material",
                    row.MaterialNumber,
                    existing.Id,
                    utc,
                    cancellationToken);
                updated++;
                mapped++;
            }
        }

        await _db.IntercambiosExternos.AddAsync(
            new IntercambioExterno(
                "Inbound",
                "Sap.Materials.Sync",
                Guid.CreateVersion7().ToString("N"),
                $"Sync materiales SAP: {remote.Count}",
                utc,
                true,
                $"created={created};updated={updated}"),
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return new SapMaterialSyncResult(remote.Count, created, updated, mapped);
    }

    public async Task<SapPushQueuedResult> QueueDeliveryPushAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        var delivery = await _db.Deliveries.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == deliveryId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-SAP-0020",
                "La entrega no existe.");
        var lines = await _db.DeliveryLines.AsNoTracking()
            .Where(value => value.DeliveryId == deliveryId)
            .Select(value => new
            {
                value.MaterialId,
                value.Quantity,
                value.UnitSnapshot,
            })
            .ToListAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(
            new
            {
                deliveryId = delivery.Id,
                number = delivery.Number,
                salesOrderId = delivery.SalesOrderId,
                customerId = delivery.CustomerId,
                documentDate = delivery.DocumentDate,
                lines,
            },
            JsonOptions);
        var idempotency = $"delivery-push:{delivery.Id:D}";
        var existing = await _db.MensajesOutbox.SingleOrDefaultAsync(
            value => value.IdempotencyKey == idempotency,
            cancellationToken);
        var utc = _clock.UtcNow;
        if (existing is not null)
        {
            if (existing.Status == SapOutboxStatus.Failed)
            {
                existing.Requeue(utc);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return new SapPushQueuedResult(
                existing.Id,
                deliveryId,
                existing.Status.ToString());
        }

        var message = new MensajeOutbox(
            "Sap.Delivery.Push",
            payload,
            idempotency,
            utc);
        _db.MensajesOutbox.Add(message);
        _db.IntercambiosExternos.Add(
            new IntercambioExterno(
                "Outbound",
                "Sap.Delivery.Push",
                idempotency,
                $"Cola push DN {delivery.Number}",
                utc,
                true));
        await _db.SaveChangesAsync(cancellationToken);
        return new SapPushQueuedResult(message.Id, deliveryId, message.Status.ToString());
    }

    public async Task<IReadOnlyCollection<SapOutboxRow>> ListOutboxAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.MensajesOutbox.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(value => new SapOutboxRow(
            value.Id,
            value.MessageType,
            value.IdempotencyKey,
            value.Status.ToString(),
            value.AttemptCount,
            value.LastError,
            value.CreatedUtc,
            value.UpdatedUtc,
            value.NextAttemptUtc)).ToArray();
    }

    public async Task ProcessOutboxAsync(CancellationToken cancellationToken = default)
    {
        var utc = _clock.UtcNow;
        var pending = await _db.MensajesOutbox
            .Where(value =>
                value.Status == SapOutboxStatus.Pending
                || (value.Status == SapOutboxStatus.Failed
                    && value.NextAttemptUtc != null
                    && value.NextAttemptUtc <= utc))
            .OrderBy(value => value.CreatedUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var message in pending)
        {
            message.MarkInFlight(utc);
            await _db.SaveChangesAsync(cancellationToken);
            try
            {
                if (string.Equals(
                        message.MessageType,
                        "Sap.Delivery.Push",
                        StringComparison.Ordinal))
                {
                    var deliveryId = ExtractDeliveryId(message.PayloadJson);
                    await _sap.PushDeliveryAsync(
                        deliveryId,
                        message.PayloadJson,
                        cancellationToken);
                }
                else
                {
                    throw new LogisticsException(
                        LogisticsErrorKind.Validation,
                        "AAA-SAP-0030",
                        $"Tipo outbox no soportado: {message.MessageType}");
                }

                message.MarkSucceeded(_clock.UtcNow);
                _db.IntercambiosExternos.Add(
                    new IntercambioExterno(
                        "Outbound",
                        message.MessageType,
                        message.IdempotencyKey,
                        "Outbox enviado a SAP",
                        _clock.UtcNow,
                        true));
            }
            catch (Exception exception) when (exception is LogisticsException
                or HttpRequestException
                or TaskCanceledException
                or JsonException)
            {
                var delay = TimeSpan.FromSeconds(
                    Math.Min(300, 15 * Math.Max(1, message.AttemptCount)));
                message.MarkFailed(exception.Message, _clock.UtcNow, delay);
                _db.IntercambiosExternos.Add(
                    new IntercambioExterno(
                        "Outbound",
                        message.MessageType,
                        message.IdempotencyKey,
                        "Fallo outbox SAP",
                        _clock.UtcNow,
                        false,
                        exception.Message));
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SapFieldMappingView>> ListMappingsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.MapeosIntegracion.AsNoTracking()
            .OrderBy(value => value.SystemCode)
            .ThenBy(value => value.EntityType)
            .ThenBy(value => value.ExternalKey)
            .Take(500)
            .ToListAsync(cancellationToken);
        return rows.Select(ToMapping).ToArray();
    }

    public async Task<SapFieldMappingView> UpsertMappingAsync(
        string systemCode,
        string entityType,
        string externalKey,
        Guid internalId,
        CancellationToken cancellationToken = default)
    {
        var utc = _clock.UtcNow;
        var system = string.IsNullOrWhiteSpace(systemCode) ? "SAP" : systemCode.Trim();
        var map = await _db.MapeosIntegracion.SingleOrDefaultAsync(
            value => value.SystemCode == system
                && value.EntityType == entityType
                && value.ExternalKey == externalKey,
            cancellationToken);
        if (map is null)
        {
            map = new MapeoIntegracion(system, entityType, externalKey, internalId, utc);
            _db.MapeosIntegracion.Add(map);
        }
        else
        {
            map.Touch(internalId, utc);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToMapping(map);
    }

    private async Task UpsertMappingAsync(
        string entityType,
        string externalKey,
        Guid internalId,
        DateTimeOffset utc,
        CancellationToken cancellationToken)
    {
        var map = await _db.MapeosIntegracion.SingleOrDefaultAsync(
            value => value.SystemCode == "SAP"
                && value.EntityType == entityType
                && value.ExternalKey == externalKey,
            cancellationToken);
        if (map is null)
        {
            _db.MapeosIntegracion.Add(
                new MapeoIntegracion("SAP", entityType, externalKey, internalId, utc));
        }
        else
        {
            map.Touch(internalId, utc);
        }
    }

    private static SapFieldMappingView ToMapping(MapeoIntegracion value) =>
        new(
            value.Id,
            value.SystemCode,
            value.EntityType,
            value.ExternalKey,
            value.InternalId,
            value.MappedUtc);

    private static Guid ExtractDeliveryId(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (document.RootElement.TryGetProperty("deliveryId", out var id)
            && id.TryGetGuid(out var guid))
        {
            return guid;
        }

        throw new LogisticsException(
            LogisticsErrorKind.Validation,
            "AAA-SAP-0031",
            "Payload outbox sin deliveryId.");
    }
}
