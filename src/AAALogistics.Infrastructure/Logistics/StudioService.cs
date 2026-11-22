using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;
using AAALogistics.Domain.Industrial;
using AAALogistics.Domain.Integrations;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Security;
using AAALogistics.Domain.Studio;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAALogistics.Infrastructure.Logistics;

public sealed partial class StudioService(
    ApplicationDbContext db,
    IClock clock,
    IAppendixLogisticsService appendix,
    ILogisticsService logistics,
    ISapIntegrationService sapIntegration,
    IRequestContext requestContext,
    ILogger<StudioService> logger) : IStudioService
{
    private readonly ApplicationDbContext _db = db;
    private readonly IClock _clock = clock;
    private readonly IAppendixLogisticsService _appendix = appendix;
    private readonly ILogisticsService _logistics = logistics;
    private readonly ISapIntegrationService _sapIntegration = sapIntegration;
    private readonly IRequestContext _requestContext = requestContext;
    private readonly ILogger<StudioService> _logger = logger;

    // ——— P1 ———

    public async Task<TaxCodeView> CreateTaxCodeAsync(
        CreateTaxCodeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 16);
        if (await _db.TaxCodes.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0101",
                "Ya existe un código fiscal con ese código.");
        }

        var entity = new TaxCode(code, command.Name, command.RatePercent, command.CountryCode);
        _db.TaxCodes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ToTax(entity);
    }

    public async Task<IReadOnlyCollection<TaxCodeView>> ListTaxCodesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.TaxCodes.AsNoTracking()
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToTax).ToArray();
    }

    public async Task<PriceConditionView> CreatePriceConditionAsync(
        CreatePriceConditionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 30);
        if (await _db.PriceConditions.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0102",
                "Ya existe una condición de precio con ese código.");
        }

        await EnsureOptionalExistsAsync(
            _db.Materials,
            command.MaterialId,
            "AAA-STU-0105",
            "Material no encontrado.",
            cancellationToken);
        await EnsureOptionalExistsAsync(
            _db.Customers,
            command.CustomerId,
            "AAA-STU-0104",
            "Cliente no encontrado.",
            cancellationToken);
        var entity = new PriceCondition(
            code,
            command.Name,
            command.PercentOff,
            command.AmountOff,
            command.MaterialId,
            command.CustomerId,
            command.ValidFrom,
            command.ValidTo);
        _db.PriceConditions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ToPriceCondition(entity);
    }

    public async Task<IReadOnlyCollection<PriceConditionView>> ListPriceConditionsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PriceConditions.AsNoTracking()
            .OrderBy(value => value.Code)
            .Take(500)
            .ToListAsync(cancellationToken);
        return rows.Select(ToPriceCondition).ToArray();
    }

    public async Task<RmaView> CreateRmaAsync(
        CreateRmaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0103",
                "El RMA requiere al menos una línea.");
        }

        if (!await _db.Customers.AnyAsync(
                value => value.Id == command.CustomerId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0104",
                "Cliente no encontrado.");
        }

        if (command.SalesOrderId is { } salesOrderId
            && !await _db.SalesOrders.AnyAsync(
                value => value.Id == salesOrderId && value.CustomerId == command.CustomerId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0106",
                "El pedido de venta no existe o no pertenece a ese cliente.");
        }

        var units = await LoadMaterialUnitsAsync(
            command.Lines.Select(line => line.MaterialId).ToArray(),
            "AAA-STU-0105",
            cancellationToken);
        var number = await TakeNumberAsync("STU_RMA", "RMA-", cancellationToken);
        var doc = new RmaDocument(
            number,
            command.CustomerId,
            command.SalesOrderId,
            command.Reason,
            _clock.UtcNow);
        _db.RmaDocuments.Add(doc);
        foreach (var line in command.Lines)
        {
            _db.RmaLines.Add(
                new RmaLine(
                    doc.Id,
                    line.MaterialId,
                    line.Quantity,
                    UnitOrDefault(line.UnitSnapshot, line.MaterialId, units)));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadRmaAsync(doc.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<RmaView>> ListRmasAsync(
        CancellationToken cancellationToken = default)
    {
        var docs = await _db.RmaDocuments.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return await MapRmasAsync(docs, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CreditSummaryRow>> ListCreditSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var terms = await _db.CustomerCommercialTerms.AsNoTracking()
            .OrderBy(value => value.CustomerId)
            .Take(500)
            .ToListAsync(cancellationToken);
        return terms.Select(value => new CreditSummaryRow(
            value.CustomerId,
            value.CompanyId,
            value.CreditLimit,
            value.OpenReceivables,
            value.Currency,
            value.IsCreditBlocked)).ToArray();
    }

    // ——— P2 ———

    public async Task<FrameworkContractView> CreateFrameworkContractAsync(
        CreateFrameworkContractCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await _db.Suppliers.AnyAsync(
                value => value.Id == command.SupplierId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0201",
                "Proveedor no encontrado.");
        }

        EnsureLines(command.Lines.Count, "AAA-STU-0202", "El contrato marco requiere al menos una línea.");
        if (command.ValidTo < command.ValidFrom)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0203",
                "La fecha final del contrato no puede ser anterior a la inicial.");
        }

        var units = await LoadMaterialUnitsAsync(
            command.Lines.Select(line => line.MaterialId).ToArray(),
            "AAA-STU-0204",
            cancellationToken);
        var number = await TakeNumberAsync("STU_FC", "FC-", cancellationToken);
        var contract = new FrameworkContract(
            number,
            command.SupplierId,
            command.ValidFrom,
            command.ValidTo,
            command.Currency,
            _clock.UtcNow);
        _db.FrameworkContracts.Add(contract);
        foreach (var line in command.Lines)
        {
            _db.FrameworkContractLines.Add(
                new FrameworkContractLine(
                    contract.Id,
                    line.MaterialId,
                    line.TargetQty,
                    line.UnitPrice,
                    UnitOrDefault(line.UnitSnapshot, line.MaterialId, units)));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadFrameworkContractAsync(contract.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<FrameworkContractView>> ListFrameworkContractsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.FrameworkContracts.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var result = new List<FrameworkContractView>();
        foreach (var row in rows)
        {
            result.Add(await LoadFrameworkContractAsync(row.Id, cancellationToken));
        }

        return result;
    }

    public async Task<RfqView> CreateRfqAsync(
        CreateRfqCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureLines(command.Lines.Count, "AAA-STU-0205", "La petición de oferta requiere al menos una línea.");
        var units = await LoadMaterialUnitsAsync(
            command.Lines.Select(line => line.MaterialId).ToArray(),
            "AAA-STU-0204",
            cancellationToken);
        var number = await TakeNumberAsync("STU_RFQ", "RFQ-", cancellationToken);
        var doc = new RfqDocument(number, command.Title, _clock.UtcNow, command.DueDate);
        _db.RfqDocuments.Add(doc);
        foreach (var line in command.Lines)
        {
            _db.RfqLines.Add(
                new RfqLine(
                    doc.Id,
                    line.MaterialId,
                    line.Quantity,
                    UnitOrDefault(line.UnitSnapshot, line.MaterialId, units)));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadRfqAsync(doc.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<RfqView>> ListRfqsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.RfqDocuments.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var result = new List<RfqView>();
        foreach (var row in rows)
        {
            result.Add(await LoadRfqAsync(row.Id, cancellationToken));
        }

        return result;
    }

    public async Task<ThreeWayMatchView> CreateThreeWayMatchAsync(
        CreateThreeWayMatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var order = await _db.PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == command.PurchaseOrderId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0206",
                "Pedido de compra no encontrado.");
        var lines = await _db.PurchaseOrderLines.AsNoTracking()
            .Where(value => value.PurchaseOrderId == order.Id)
            .ToListAsync(cancellationToken);
        var poAmount = lines.Sum(value => value.RequestedQuantity * value.PurchasePrice);
        var prices = lines.ToDictionary(value => value.Id, value => value.PurchasePrice);

        var receiptLines = _db.GoodsReceiptLines.AsNoTracking()
            .Join(
                _db.GoodsReceipts.AsNoTracking().Where(value => value.PurchaseOrderId == order.Id),
                line => line.GoodsReceiptId,
                receipt => receipt.Id,
                (line, _) => line);
        if (command.GoodsReceiptId is { } receiptId)
        {
            if (!await _db.GoodsReceipts.AnyAsync(
                    value => value.Id == receiptId && value.PurchaseOrderId == order.Id,
                    cancellationToken))
            {
                throw new LogisticsException(
                    LogisticsErrorKind.NotFound,
                    "AAA-STU-0207",
                    "La recepción no existe o no pertenece a ese pedido.");
            }

            receiptLines = receiptLines.Where(value => value.GoodsReceiptId == receiptId);
        }

        var received = await receiptLines
            .Select(value => new { value.PurchaseOrderLineId, value.Quantity })
            .ToListAsync(cancellationToken);
        var grAmount = received.Sum(value =>
            value.Quantity * prices.GetValueOrDefault(value.PurchaseOrderLineId));

        decimal invoiceAmount;
        if (command.SupplierInvoiceId is { } invoiceId)
        {
            var invoice = await _db.SupplierInvoiceStubs.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == invoiceId, cancellationToken)
                ?? throw new LogisticsException(
                    LogisticsErrorKind.NotFound,
                    "AAA-STU-0208",
                    "Factura de proveedor no encontrada.");
            if (invoice.SupplierId != order.SupplierId
                || (invoice.PurchaseOrderId is { } invoiceOrderId && invoiceOrderId != order.Id))
            {
                throw new LogisticsException(
                    LogisticsErrorKind.Validation,
                    "AAA-STU-0209",
                    "La factura es de otro proveedor o de otro pedido.");
            }

            invoiceAmount = invoice.Amount;
        }
        else
        {
            invoiceAmount = command.InvoiceAmount;
        }

        poAmount = Math.Round(poAmount, 4, MidpointRounding.AwayFromZero);
        grAmount = Math.Round(grAmount, 4, MidpointRounding.AwayFromZero);
        var matched = Math.Abs(poAmount - grAmount) < 0.01m
            && Math.Abs(grAmount - invoiceAmount) < 0.01m;
        var record = new ThreeWayMatchRecord(
            order.Id,
            command.GoodsReceiptId,
            command.SupplierInvoiceId,
            poAmount,
            grAmount,
            invoiceAmount,
            matched,
            command.Notes ?? string.Empty,
            _clock.UtcNow);
        _db.ThreeWayMatchRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        return ToThreeWayMatch(record);
    }

    public async Task<IReadOnlyCollection<ThreeWayMatchView>> ListThreeWayMatchesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ThreeWayMatchRecords.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToThreeWayMatch).ToArray();
    }

    public async Task<MaterialValuationView> UpsertMaterialValuationAsync(
        UpsertMaterialValuationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(command.Method))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0210",
                "Método de valoración no válido (0 = estándar, 1 = precio medio).");
        }

        await EnsureExistsAsync(
            _db.Materials,
            command.MaterialId,
            "AAA-STU-0204",
            "Material no encontrado.",
            cancellationToken);
        await EnsureOptionalExistsAsync(
            _db.Warehouses,
            command.WarehouseId,
            "AAA-STU-0211",
            "Almacén no encontrado.",
            cancellationToken);
        var existing = await _db.MaterialValuations.SingleOrDefaultAsync(
            value => value.MaterialId == command.MaterialId
                && value.WarehouseId == command.WarehouseId,
            cancellationToken);
        if (existing is null)
        {
            existing = new MaterialValuation(
                command.MaterialId,
                command.WarehouseId,
                command.Method,
                command.StandardPrice,
                command.MovingAveragePrice,
                command.Currency);
            _db.MaterialValuations.Add(existing);
        }
        else
        {
            existing.Update(
                command.Method,
                command.StandardPrice,
                command.MovingAveragePrice);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToValuation(existing);
    }

    public async Task<IReadOnlyCollection<MaterialValuationView>> ListMaterialValuationsAsync(
        CancellationToken cancellationToken = default)
    {
        var scopedWarehouses = await ResolveScopedWarehouseIdsAsync(cancellationToken);
        var query = _db.MaterialValuations.AsNoTracking().AsQueryable();
        if (scopedWarehouses is not null)
        {
            query = query.Where(
                value => value.WarehouseId == null
                    || scopedWarehouses.Contains(value.WarehouseId.Value));
        }

        var rows = await query
            .OrderBy(value => value.MaterialId)
            .Take(500)
            .ToListAsync(cancellationToken);
        return rows.Select(ToValuation).ToArray();
    }

    public async Task<SupplierInvoiceStubView> CreateSupplierInvoiceStubAsync(
        CreateSupplierInvoiceStubCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureExistsAsync(
            _db.Suppliers,
            command.SupplierId,
            "AAA-STU-0201",
            "Proveedor no encontrado.",
            cancellationToken);
        if (command.PurchaseOrderId is { } orderId
            && !await _db.PurchaseOrders.AnyAsync(
                value => value.Id == orderId && value.SupplierId == command.SupplierId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0212",
                "El pedido de compra no existe o es de otro proveedor.");
        }

        var number = await TakeNumberAsync("STU_SINV", "SINV-", cancellationToken);
        var stub = new SupplierInvoiceStub(
            number,
            command.SupplierId,
            command.PurchaseOrderId,
            command.Amount,
            command.Currency,
            command.InvoiceDate);
        _db.SupplierInvoiceStubs.Add(stub);
        await _db.SaveChangesAsync(cancellationToken);
        return ToSupplierInvoice(stub);
    }

    public async Task<IReadOnlyCollection<SupplierInvoiceStubView>> ListSupplierInvoiceStubsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.SupplierInvoiceStubs.AsNoTracking()
            .OrderByDescending(value => value.InvoiceDate)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSupplierInvoice).ToArray();
    }

    public async Task<JournalEntryView> PostJournalFromSupplierInvoiceAsync(
        Guid supplierInvoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _db.SupplierInvoiceStubs.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == supplierInvoiceId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0310",
                "Factura de proveedor no encontrada.");

        await SeedSpainChartOfAccountsAsync(cancellationToken);
        var expense = await _db.GlAccounts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Code == "600", cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0311",
                "Falta la cuenta 600 Compras. Use Sembrar CoA ES.");
        var payable = await _db.GlAccounts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Code == "400", cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0312",
                "Falta la cuenta 400 Proveedores. Use Sembrar CoA ES.");

        return await PostJournalEntryAsync(
            new PostJournalEntryCommand(
                "SUPPLIER_INVOICE",
                $"Factura proveedor {invoice.Number}",
                invoice.InvoiceDate,
                [
                    new PostJournalLineCommand(expense.Id, null, invoice.Amount, 0m),
                    new PostJournalLineCommand(payable.Id, null, 0m, invoice.Amount),
                ]),
            cancellationToken);
    }

    // ——— P3 ———

    public async Task<int> SeedSpainChartOfAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var seeds = new (string Code, string Name, GlAccountType Type)[]
        {
            ("570", "Caja", GlAccountType.Asset),
            ("572", "Bancos", GlAccountType.Asset),
            ("430", "Clientes", GlAccountType.Asset),
            ("400", "Proveedores", GlAccountType.Liability),
            ("300", "Mercaderías", GlAccountType.Asset),
            ("600", "Compras", GlAccountType.Expense),
            ("700", "Ventas", GlAccountType.Income),
            ("100", "Capital social", GlAccountType.Equity),
        };
        var added = 0;
        foreach (var (code, name, type) in seeds)
        {
            var normalized = UnitOfMeasure.NormalizeCode(code, 20);
            if (await _db.GlAccounts.AnyAsync(value => value.Code == normalized, cancellationToken))
            {
                continue;
            }

            _db.GlAccounts.Add(new GlAccount(normalized, name, type));
            added++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return added;
    }

    public async Task<IReadOnlyCollection<GlAccountView>> ListGlAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.GlAccounts.AsNoTracking()
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToGl).ToArray();
    }

    public async Task<CostCenterView> CreateCostCenterAsync(
        CreateCostCenterCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 20);
        if (await _db.CostCenters.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0301",
                "Ya existe un centro de coste con ese código.");
        }

        await EnsureOptionalExistsAsync(
            _db.Companies,
            command.CompanyId,
            "AAA-STU-0304",
            "Sociedad no encontrada.",
            cancellationToken);
        var entity = new CostCenter(code, command.Name, command.CompanyId);
        _db.CostCenters.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ToCostCenter(entity);
    }

    public async Task<IReadOnlyCollection<CostCenterView>> ListCostCentersAsync(
        CancellationToken cancellationToken = default)
    {
        var scopedCompanyId = await ResolveScopedCompanyIdAsync(cancellationToken);
        var query = _db.CostCenters.AsNoTracking().AsQueryable();
        if (scopedCompanyId is Guid companyId)
        {
            query = query.Where(
                value => value.CompanyId == null || value.CompanyId == companyId);
        }

        var rows = await query
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToCostCenter).ToArray();
    }

    public async Task<JournalEntryView> PostJournalEntryAsync(
        PostJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0302",
                "El asiento requiere líneas.");
        }

        var debit = command.Lines.Sum(value => value.Debit);
        var credit = command.Lines.Sum(value => value.Credit);
        if (Math.Abs(debit - credit) > 0.0001m)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0303",
                "El asiento no cuadra (debe ≠ haber).");
        }

        if (debit <= 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0305",
                "El asiento necesita importes mayores que cero.");
        }

        var accountIds = command.Lines.Select(value => value.GlAccountId).Distinct().ToArray();
        var knownAccounts = await _db.GlAccounts
            .CountAsync(value => accountIds.Contains(value.Id), cancellationToken);
        if (knownAccounts != accountIds.Length)
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0306",
                "Alguna cuenta contable del asiento no existe.");
        }

        var costCenterIds = command.Lines
            .Where(value => value.CostCenterId is not null)
            .Select(value => value.CostCenterId!.Value)
            .Distinct()
            .ToArray();
        if (costCenterIds.Length > 0
            && await _db.CostCenters.CountAsync(
                value => costCenterIds.Contains(value.Id),
                cancellationToken) != costCenterIds.Length)
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0307",
                "Algún centro de coste del asiento no existe.");
        }

        var number = await TakeNumberAsync("STU_JE", "JE-", cancellationToken);
        var entry = new JournalEntry(
            number,
            command.Source,
            command.Description,
            command.PostingDate,
            _clock.UtcNow);
        _db.JournalEntries.Add(entry);
        foreach (var line in command.Lines)
        {
            _db.JournalLines.Add(
                new JournalLine(
                    entry.Id,
                    line.GlAccountId,
                    line.CostCenterId,
                    line.Debit,
                    line.Credit));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadJournalAsync(entry.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<JournalEntryView>> ListJournalEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.JournalEntries.AsNoTracking()
            .OrderByDescending(value => value.PostingDate)
            .Take(200)
            .ToListAsync(cancellationToken);
        var result = new List<JournalEntryView>();
        foreach (var row in rows)
        {
            result.Add(await LoadJournalAsync(row.Id, cancellationToken));
        }

        return result;
    }

    public async Task<JournalReconciliationSummary> GetJournalReconciliationSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var totals = await _db.JournalLines.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Debit = group.Sum(value => value.Debit),
                Credit = group.Sum(value => value.Credit),
            })
            .FirstOrDefaultAsync(cancellationToken);
        var debit = totals?.Debit ?? 0m;
        var credit = totals?.Credit ?? 0m;
        var entryCount = await _db.JournalEntries.CountAsync(cancellationToken);
        return new JournalReconciliationSummary(
            debit,
            credit,
            debit - credit,
            entryCount,
            Math.Abs(debit - credit) < 0.01m);
    }

    // ——— P4 ———

    public async Task<BomView> CreateBomAsync(
        CreateBomCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 30);
        if (await _db.BillOfMaterials.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0401",
                "Ya existe una BOM con ese código.");
        }

        EnsureLines(command.Components.Count, "AAA-STU-0403", "La lista de materiales requiere al menos un componente.");
        if (command.Components.Any(value => value.ComponentMaterialId == command.ParentMaterialId))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0404",
                "Un componente no puede ser el mismo material que el producto.");
        }

        var units = await LoadMaterialUnitsAsync(
            command.Components.Select(value => value.ComponentMaterialId)
                .Append(command.ParentMaterialId)
                .ToArray(),
            "AAA-STU-0405",
            cancellationToken);
        var bom = new BillOfMaterials(code, command.ParentMaterialId, command.Description);
        _db.BillOfMaterials.Add(bom);
        foreach (var component in command.Components)
        {
            _db.BomComponents.Add(
                new BomComponent(
                    bom.Id,
                    component.ComponentMaterialId,
                    component.Quantity,
                    UnitOrDefault(component.UnitSnapshot, component.ComponentMaterialId, units)));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadBomAsync(bom.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<BomView>> ListBomsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.BillOfMaterials.AsNoTracking()
            .OrderBy(value => value.Code)
            .Take(200)
            .ToListAsync(cancellationToken);
        var result = new List<BomView>();
        foreach (var row in rows)
        {
            result.Add(await LoadBomAsync(row.Id, cancellationToken));
        }

        return result;
    }

    public async Task<ProductionRouteView> CreateProductionRouteAsync(
        CreateProductionRouteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 30);
        if (await _db.ProductionRoutes.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0402",
                "Ya existe una ruta con ese código.");
        }

        EnsureLines(command.Operations.Count, "AAA-STU-0406", "La ruta requiere al menos una operación.");
        if (command.Operations.Select(value => value.Sequence).Distinct().Count() != command.Operations.Count)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0407",
                "Las operaciones no pueden repetir secuencia.");
        }

        await EnsureExistsAsync(
            _db.Materials,
            command.MaterialId,
            "AAA-STU-0405",
            "Material no encontrado.",
            cancellationToken);
        var route = new ProductionRoute(code, command.MaterialId, command.Name);
        _db.ProductionRoutes.Add(route);
        foreach (var op in command.Operations)
        {
            _db.RouteOperations.Add(
                new RouteOperation(
                    route.Id,
                    op.Sequence,
                    op.WorkCenter,
                    op.SetupMinutes,
                    op.RunMinutes));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadRouteAsync(route.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProductionRouteView>> ListProductionRoutesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ProductionRoutes.AsNoTracking()
            .OrderBy(value => value.Code)
            .Take(200)
            .ToListAsync(cancellationToken);
        var result = new List<ProductionRouteView>();
        foreach (var row in rows)
        {
            result.Add(await LoadRouteAsync(row.Id, cancellationToken));
        }

        return result;
    }

    public async Task<ProductionOrderView> CreateProductionOrderAsync(
        CreateProductionOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var units = await LoadMaterialUnitsAsync(
            [command.MaterialId],
            "AAA-STU-0405",
            cancellationToken);
        if (command.BomId is { } bomId
            && !await _db.BillOfMaterials.AnyAsync(
                value => value.Id == bomId && value.ParentMaterialId == command.MaterialId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0408",
                "La lista de materiales no existe o es de otro producto.");
        }

        if (command.RouteId is { } routeId
            && !await _db.ProductionRoutes.AnyAsync(
                value => value.Id == routeId && value.MaterialId == command.MaterialId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0409",
                "La ruta no existe o es de otro producto.");
        }

        var number = await TakeNumberAsync("STU_OF", "OF-", cancellationToken);
        var order = new ProductionOrder(
            number,
            command.MaterialId,
            command.BomId,
            command.RouteId,
            command.Quantity,
            UnitOrDefault(command.UnitSnapshot, command.MaterialId, units),
            _clock.UtcNow);
        _db.ProductionOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        return ToProductionOrder(order);
    }

    public async Task<IReadOnlyCollection<ProductionOrderView>> ListProductionOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ProductionOrders.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToProductionOrder).ToArray();
    }

    public async Task<ProductionOrderView> ReleaseProductionOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.ProductionOrders.SingleOrDefaultAsync(
            value => value.Id == orderId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0411",
                "Orden de fabricación no encontrada.");
        try
        {
            order.Release();
        }
        catch (InvalidOperationException exception)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0412",
                exception.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToProductionOrder(order);
    }

    public async Task<ProductionOrderView> CompleteProductionOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.ProductionOrders.SingleOrDefaultAsync(
            value => value.Id == orderId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0413",
                "Orden de fabricación no encontrada.");
        try
        {
            order.Complete();
        }
        catch (InvalidOperationException exception)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0414",
                exception.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToProductionOrder(order);
    }

    public async Task<MrpTriggerResult> TriggerMrpAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOptionalExistsAsync(
            _db.Warehouses,
            warehouseId,
            "AAA-STU-0410",
            "Almacén no encontrado.",
            cancellationToken);
        try
        {
            var reqs = await _appendix.GenerateMrpAsync(warehouseId, cancellationToken);
            return new MrpTriggerResult(
                "appendix",
                reqs.Count == 0 ? "No hay necesidades: el stock cubre los puntos de pedido." : null,
                reqs.Select(value => value.Id).ToArray());
        }
        catch (LogisticsException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateException)
        {
            LogMrpFailure(_logger, exception);
            return new MrpTriggerResult(
                "stub",
                "No se pudo calcular el MRP. Revise el registro del servidor.",
                null);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Studio MRP generation failed.")]
    private static partial void LogMrpFailure(ILogger logger, Exception exception);

    // ——— P5 ———

    public async Task<InspectionPlanView> CreateInspectionPlanAsync(
        CreateInspectionPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 30);
        if (await _db.InspectionPlans.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0501",
                "Ya existe un plan de inspección con ese código.");
        }

        await EnsureExistsAsync(
            _db.Materials,
            command.MaterialId,
            "AAA-STU-0504",
            "Material no encontrado.",
            cancellationToken);
        var plan = new InspectionPlan(code, command.MaterialId, command.Name);
        _db.InspectionPlans.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);
        return ToInspectionPlan(plan);
    }

    public async Task<IReadOnlyCollection<InspectionPlanView>> ListInspectionPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.InspectionPlans.AsNoTracking()
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToInspectionPlan).ToArray();
    }

    public async Task<QualityHoldView> CreateQualityHoldAsync(
        CreateQualityHoldCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var lot = await _db.Lots.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == command.LotId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0505",
                "Lote no encontrado.");
        if (command.MaterialId is { } materialId && materialId != lot.MaterialId)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0506",
                "El lote pertenece a otro material.");
        }

        if (await _db.QualityHolds.AnyAsync(
                value => value.LotId == lot.Id && value.Status == QualityHoldStatus.Held,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0507",
                "Ese lote ya tiene un bloqueo de calidad activo.");
        }

        var hold = new QualityHold(
            lot.Id,
            lot.MaterialId,
            command.Reason,
            _clock.UtcNow);
        _db.QualityHolds.Add(hold);
        var updated = await ApplyLotStockStatusAsync(lot.Id, StockStatus.Quality, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return ToQualityHold(hold, lot.Code, updated);
    }

    public async Task<IReadOnlyCollection<QualityHoldView>> ListQualityHoldsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from hold in _db.QualityHolds.AsNoTracking()
                join lot in _db.Lots.AsNoTracking() on hold.LotId equals lot.Id
                orderby hold.CreatedUtc descending
                select new { Hold = hold, LotCode = lot.Code })
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(row => ToQualityHold(row.Hold, row.LotCode)).ToArray();
    }

    public async Task<QualityHoldView> ReleaseQualityHoldAsync(
        Guid holdId,
        CancellationToken cancellationToken = default)
    {
        var hold = await _db.QualityHolds.SingleOrDefaultAsync(
            value => value.Id == holdId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0502",
                "Bloqueo de calidad no encontrado.");
        EnsureHoldIsActive(hold);
        hold.Release();
        var lotCode = await ResolveLotCodeAsync(hold.LotId, cancellationToken);
        var updated = await ApplyLotStockStatusAsync(hold.LotId, StockStatus.Available, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return ToQualityHold(hold, lotCode, updated);
    }

    public async Task<QualityHoldView> RejectQualityHoldAsync(
        Guid holdId,
        CancellationToken cancellationToken = default)
    {
        var hold = await _db.QualityHolds.SingleOrDefaultAsync(
            value => value.Id == holdId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0503",
                "Bloqueo de calidad no encontrado.");
        EnsureHoldIsActive(hold);
        hold.Reject();
        var lotCode = await ResolveLotCodeAsync(hold.LotId, cancellationToken);
        var updated = await ApplyLotStockStatusAsync(hold.LotId, StockStatus.Blocked, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return ToQualityHold(hold, lotCode, updated);
    }

    private async Task<string> ResolveLotCodeAsync(Guid lotId, CancellationToken cancellationToken)
    {
        var code = await _db.Lots.AsNoTracking()
            .Where(value => value.Id == lotId)
            .Select(value => value.Code)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(code) ? lotId.ToString("D")[..8] + "…" : code;
    }

    private async Task<int> ApplyLotStockStatusAsync(
        Guid lotId,
        StockStatus status,
        CancellationToken cancellationToken)
    {
        if (lotId == Guid.Empty)
        {
            return 0;
        }

        var balances = await _db.StockBalances
            .Where(value => value.LotId == lotId && value.Quantity > 0)
            .ToListAsync(cancellationToken);
        foreach (var balance in balances)
        {
            balance.SetStatus(status);
        }

        return balances.Count;
    }

    public async Task<QualityCertificateView> CreateQualityCertificateAsync(
        CreateQualityCertificateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureExistsAsync(
            _db.Materials,
            command.MaterialId,
            "AAA-STU-0504",
            "Material no encontrado.",
            cancellationToken);
        if (command.LotId is { } lotId
            && !await _db.Lots.AnyAsync(
                value => value.Id == lotId && value.MaterialId == command.MaterialId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0508",
                "El lote no existe o es de otro material.");
        }

        var number = await TakeNumberAsync("STU_QC", "QC-", cancellationToken);
        var cert = new QualityCertificate(
            number,
            command.MaterialId,
            command.LotId,
            command.Result,
            _clock.UtcNow);
        _db.QualityCertificates.Add(cert);
        await _db.SaveChangesAsync(cancellationToken);
        return ToQualityCert(cert);
    }

    public async Task<IReadOnlyCollection<QualityCertificateView>> ListQualityCertificatesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.QualityCertificates.AsNoTracking()
            .OrderByDescending(value => value.IssuedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToQualityCert).ToArray();
    }

    // ——— P6 ———

    public async Task<PlantView> CreatePlantAsync(
        CreatePlantCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 30);
        if (await _db.Plants.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0601",
                "Ya existe una planta con ese código.");
        }

        await EnsureExistsAsync(
            _db.Companies,
            command.CompanyId,
            "AAA-STU-0603",
            "Sociedad no encontrada.",
            cancellationToken);
        var plant = new Plant(command.CompanyId, code, command.Name);
        _db.Plants.Add(plant);
        await _db.SaveChangesAsync(cancellationToken);
        return ToPlant(plant);
    }

    public async Task<IReadOnlyCollection<PlantView>> ListPlantsAsync(
        CancellationToken cancellationToken = default)
    {
        var scoped = await ResolveScopedOrgAsync(cancellationToken);
        var query = _db.Plants.AsNoTracking().AsQueryable();
        if (scoped.PlantId is Guid plantId)
        {
            query = query.Where(value => value.Id == plantId);
        }
        else if (scoped.CompanyId is Guid companyId)
        {
            query = query.Where(value => value.CompanyId == companyId);
        }

        var rows = await query
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToPlant).ToArray();
    }

    public async Task<PlantWarehouseView> LinkPlantWarehouseAsync(
        LinkPlantWarehouseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await _db.PlantWarehouses.AnyAsync(
                value => value.PlantId == command.PlantId
                    && value.WarehouseId == command.WarehouseId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0602",
                "El almacén ya está vinculado a la planta.");
        }

        await EnsureExistsAsync(
            _db.Plants,
            command.PlantId,
            "AAA-STU-0604",
            "Planta no encontrada.",
            cancellationToken);
        await EnsureExistsAsync(
            _db.Warehouses,
            command.WarehouseId,
            "AAA-STU-0605",
            "Almacén no encontrado.",
            cancellationToken);
        var link = new PlantWarehouse(command.PlantId, command.WarehouseId);
        _db.PlantWarehouses.Add(link);
        await _db.SaveChangesAsync(cancellationToken);
        return new PlantWarehouseView(link.Id, link.PlantId, link.WarehouseId);
    }

    public async Task<IReadOnlyCollection<PlantWarehouseView>> ListPlantWarehousesAsync(
        CancellationToken cancellationToken = default)
    {
        var scoped = await ResolveScopedOrgAsync(cancellationToken);
        var query = _db.PlantWarehouses.AsNoTracking().AsQueryable();
        if (scoped.PlantId is Guid plantId)
        {
            query = query.Where(value => value.PlantId == plantId);
        }
        else if (scoped.CompanyId is Guid companyId)
        {
            var plantIds = _db.Plants.AsNoTracking()
                .Where(value => value.CompanyId == companyId)
                .Select(value => value.Id);
            query = query.Where(value => plantIds.Contains(value.PlantId));
        }

        var rows = await query
            .OrderBy(value => value.PlantId)
            .ToListAsync(cancellationToken);
        return rows.Select(value => new PlantWarehouseView(
            value.Id,
            value.PlantId,
            value.WarehouseId)).ToArray();
    }

    public async Task<UserOrgContextView> GetUserOrgContextAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _db.UserOrgContexts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        return ctx is null
            ? new UserOrgContextView(Guid.Empty, userId, null, null)
            : ToUserOrg(ctx);
    }

    public async Task<UserOrgContextView> SetUserOrgContextAsync(
        SetUserOrgContextCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await _db.Users.AnyAsync(value => value.Id == command.UserId, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0606",
                "Usuario no encontrado.");
        }

        await EnsureOptionalExistsAsync(
            _db.Companies,
            command.CompanyId,
            "AAA-STU-0603",
            "Sociedad no encontrada.",
            cancellationToken);
        if (command.PlantId is { } plantId)
        {
            var plant = await _db.Plants.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == plantId, cancellationToken)
                ?? throw new LogisticsException(
                    LogisticsErrorKind.NotFound,
                    "AAA-STU-0604",
                    "Planta no encontrada.");
            if (command.CompanyId is { } companyId && plant.CompanyId != companyId)
            {
                throw new LogisticsException(
                    LogisticsErrorKind.Validation,
                    "AAA-STU-0607",
                    "La planta pertenece a otra sociedad.");
            }
        }

        var ctx = await _db.UserOrgContexts.SingleOrDefaultAsync(
            value => value.UserId == command.UserId,
            cancellationToken);
        if (ctx is null)
        {
            ctx = new UserOrgContext(command.UserId, command.CompanyId, command.PlantId);
            _db.UserOrgContexts.Add(ctx);
        }
        else
        {
            ctx.Set(command.CompanyId, command.PlantId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToUserOrg(ctx);
    }

    // ——— P7 ———

    public async Task<ApprovalRuleView> CreateApprovalRuleAsync(
        CreateApprovalRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var kind = NormalizeApprovalKind(command.DocumentKind);
        var role = command.RequiredRole?.Trim() ?? string.Empty;
        if (!AppRoles.InternalAssignable.Contains(role, StringComparer.Ordinal))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0705",
                "El rango aprobador no existe.");
        }

        var rule = new ApprovalRule(
            kind,
            command.MinAmount,
            role,
            command.Name);
        _db.ApprovalRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return ToApprovalRule(rule);
    }

    public async Task<IReadOnlyCollection<ApprovalRuleView>> ListApprovalRulesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ApprovalRules.AsNoTracking()
            .OrderBy(value => value.DocumentKind)
            .ToListAsync(cancellationToken);
        return rows.Select(ToApprovalRule).ToArray();
    }

    public async Task<ApprovalRequestView> CreateApprovalRequestAsync(
        CreateApprovalRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var kind = NormalizeApprovalKind(command.DocumentKind);
        var amount = await ResolveDocumentAmountAsync(
            kind,
            command.DocumentId,
            cancellationToken);
        if (await _db.ApprovalRequests.AnyAsync(
                value => value.DocumentKind == kind
                    && value.DocumentId == command.DocumentId
                    && value.Status == ApprovalRequestStatus.Pending,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0706",
                "Ya hay una solicitud pendiente para ese documento.");
        }

        var rules = await _db.ApprovalRules.AsNoTracking()
            .Where(value => value.IsActive
                && value.DocumentKind == kind
                && value.MinAmount <= amount)
            .ToListAsync(cancellationToken);
        var rule = rules.OrderByDescending(value => value.MinAmount).FirstOrDefault();
        var request = new ApprovalRequest(
            kind,
            command.DocumentId,
            amount,
            command.Summary,
            _clock.UtcNow,
            command.RequestedByUserId,
            rule?.RequiredRole);
        _db.ApprovalRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        return ToApprovalRequest(request);
    }

    public async Task<IReadOnlyCollection<ApprovalRequestView>> ListApprovalRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ApprovalRequests.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToApprovalRequest).ToArray();
    }

    public async Task<ApprovalRequestView> ApproveRequestAsync(
        Guid requestId,
        DecideApprovalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = await LoadPendingApprovalAsync(requestId, cancellationToken);
        if (request.RequestedByUserId is { } requester && requester == command.UserId)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Forbidden,
                "AAA-STU-0703",
                "No puede aprobar su propia solicitud (principio de cuatro ojos).");
        }

        EnsureApproverRole(request, command);
        request.Approve(command.UserId, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return ToApprovalRequest(request);
    }

    public async Task<ApprovalRequestView> RejectRequestAsync(
        Guid requestId,
        DecideApprovalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = await LoadPendingApprovalAsync(requestId, cancellationToken);
        var isRequester = request.RequestedByUserId is { } requester && requester == command.UserId;
        if (!isRequester)
        {
            EnsureApproverRole(request, command);
        }

        request.Reject(command.UserId, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return ToApprovalRequest(request);
    }

    // ——— P8 ———

    public async Task<IntegrationConnectorView> CreateIntegrationConnectorAsync(
        CreateIntegrationConnectorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 40);
        if (await _db.IntegrationConnectors.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0801",
                "Ya existe un conector con ese código.");
        }

        var connector = new IntegrationConnector(
            code,
            command.Name,
            command.Kind,
            command.Version);
        _db.IntegrationConnectors.Add(connector);
        await _db.SaveChangesAsync(cancellationToken);
        return ToConnector(connector);
    }

    public async Task<IReadOnlyCollection<IntegrationConnectorView>> ListIntegrationConnectorsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.IntegrationConnectors.AsNoTracking()
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToConnector).ToArray();
    }

    public async Task<IntegrationInboxView> ReceiveIntegrationInboxAsync(
        ReceiveIntegrationInboxCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureOptionalExistsAsync(
            _db.IntegrationConnectors,
            command.ConnectorId,
            "AAA-STU-0802",
            "Conector no encontrado.",
            cancellationToken);
        var message = new IntegrationInboxMessage(
            command.ConnectorId,
            command.MessageType,
            command.Payload,
            _clock.UtcNow);
        _db.IntegrationInboxMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
        return ToInbox(message);
    }

    public async Task<IReadOnlyCollection<IntegrationInboxView>> ListIntegrationInboxAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.IntegrationInboxMessages.AsNoTracking()
            .OrderByDescending(value => value.ReceivedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToInbox).ToArray();
    }

    public async Task<IReadOnlyCollection<IntegrationOutboxView>> ListIntegrationOutboxAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.MensajesOutbox.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(value => new IntegrationOutboxView(
            value.Id,
            value.MessageType,
            value.IdempotencyKey,
            value.Status.ToString(),
            value.AttemptCount,
            value.CreatedUtc)).ToArray();
    }

    public async Task<IReadOnlyCollection<SapFieldMappingView>> ListIntegrationMappingsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _sapIntegration.ListMappingsAsync(cancellationToken);
        return rows;
    }

    public Task<SapFieldMappingView> UpsertIntegrationMappingAsync(
        string systemCode,
        string entityType,
        string externalKey,
        Guid internalId,
        CancellationToken cancellationToken = default) =>
        _sapIntegration.UpsertMappingAsync(
            systemCode,
            entityType,
            externalKey,
            internalId,
            cancellationToken);

    // ——— P9 ———

    public async Task<IReadOnlyCollection<ValuedStockRow>> GetValuedStockReportAsync(
        CancellationToken cancellationToken = default)
    {
        var scopedWarehouses = await ResolveScopedWarehouseIdsAsync(cancellationToken);
        var balancesQuery = _db.StockBalances.AsNoTracking()
            .Where(value => value.Quantity > 0);
        if (scopedWarehouses is not null)
        {
            balancesQuery = balancesQuery.Where(value => scopedWarehouses.Contains(value.WarehouseId));
        }

        var balances = await balancesQuery
            .Take(2000)
            .ToListAsync(cancellationToken);
        var valuations = await _db.MaterialValuations.AsNoTracking()
            .ToDictionaryAsync(
                value => (value.MaterialId, value.WarehouseId),
                cancellationToken);
        var rows = new List<ValuedStockRow>();
        foreach (var balance in balances)
        {
            valuations.TryGetValue((balance.MaterialId, balance.WarehouseId), out var valuation);
            if (valuation is null)
            {
                valuations.TryGetValue((balance.MaterialId, null), out valuation);
            }

            var unit = valuation?.UnitValue ?? 0m;
            var currency = valuation?.Currency ?? "EUR";
            rows.Add(new ValuedStockRow(
                balance.MaterialId,
                balance.WarehouseId,
                balance.Quantity,
                unit,
                Math.Round(balance.Quantity * unit, 4, MidpointRounding.AwayFromZero),
                currency));
        }

        return rows;
    }

    public async Task<IReadOnlyCollection<AgingRow>> GetAgingReportAsync(
        CancellationToken cancellationToken = default)
    {
        var terms = await _db.CustomerCommercialTerms.AsNoTracking()
            .Where(value => value.OpenReceivables > 0)
            .Take(500)
            .ToListAsync(cancellationToken);
        var customerIds = terms.Select(value => value.CustomerId).Distinct().ToArray();
        var customers = await _db.Customers.AsNoTracking()
            .Where(value => customerIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var rows = new List<AgingRow>();
        foreach (var term in terms)
        {
            customers.TryGetValue(term.CustomerId, out var customer);
            var bucket = term.PaymentTermDays <= 30
                ? 30
                : term.PaymentTermDays <= 60
                    ? 60
                    : 90;
            rows.Add(new AgingRow(
                term.CustomerId,
                customer?.Name ?? "(cliente)",
                term.OpenReceivables,
                bucket,
                term.Currency));
        }

        return rows;
    }

    public async Task<OtifReportRow?> GetOtifReportPassthroughAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _logistics.ReportOtifAsync(cancellationToken);
        }
        catch (LogisticsException)
        {
            return null;
        }
    }

    // ——— P10 ———

    public async Task<EmployeeProfileView> CreateEmployeeProfileAsync(
        CreateEmployeeProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await _db.Users.AnyAsync(value => value.Id == command.UserId, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-1003",
                "Usuario no encontrado.");
        }

        var number = UnitOfMeasure.NormalizeCode(command.EmployeeNumber, 30);
        if (await _db.EmployeeProfiles.AnyAsync(
                value => value.EmployeeNumber == number || value.UserId == command.UserId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-1001",
                "Ya existe un perfil de empleado para ese usuario o número.");
        }

        var profile = new EmployeeProfile(
            command.UserId,
            number,
            command.DisplayName,
            command.Department,
            command.HireDate);
        _db.EmployeeProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);
        return ToEmployee(profile);
    }

    public async Task<EmployeeProfileView> UpdateEmployeeProfileAsync(
        Guid employeeId,
        UpdateEmployeeProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var profile = await _db.EmployeeProfiles.SingleOrDefaultAsync(
            value => value.Id == employeeId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-1002",
                "Perfil de empleado no encontrado.");
        profile.Update(command.DisplayName, command.Department, command.HireDate);
        if (command.IsActive)
        {
            profile.Activate();
        }
        else
        {
            profile.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToEmployee(profile);
    }

    public async Task<IReadOnlyCollection<EmployeeProfileView>> ListEmployeeProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.EmployeeProfiles.AsNoTracking()
            .OrderBy(value => value.EmployeeNumber)
            .ToListAsync(cancellationToken);
        return rows.Select(ToEmployee).ToArray();
    }

    // ——— P11 ———

    public async Task<DomainEventLogView> AppendDomainEventAsync(
        AppendDomainEventCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var log = new DomainEventLog(
            command.EventName,
            command.AggregateType,
            command.AggregateId,
            command.Payload,
            _clock.UtcNow);
        _db.DomainEventLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDomainEvent(log);
    }

    public async Task<IReadOnlyCollection<DomainEventLogView>> ListDomainEventsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.DomainEventLogs.AsNoTracking()
            .OrderByDescending(value => value.OccurredUtc)
            .Take(500)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomainEvent).ToArray();
    }

    public async Task<PluginRegistrationView> RegisterPluginAsync(
        RegisterPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 40);
        if (await _db.PluginRegistrations.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-1101",
                "Ya existe un plugin con ese código.");
        }

        var plugin = new PluginRegistration(
            code,
            command.Name,
            command.HookPoint,
            command.Description);
        _db.PluginRegistrations.Add(plugin);
        await _db.SaveChangesAsync(cancellationToken);
        return ToPlugin(plugin);
    }

    public async Task<IReadOnlyCollection<PluginRegistrationView>> ListPluginsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PluginRegistrations.AsNoTracking()
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToPlugin).ToArray();
    }

    public async Task<PluginInvokeView> InvokePluginAsync(
        Guid pluginId,
        string parametersJson,
        CancellationToken cancellationToken = default)
    {
        var plugin = await _db.PluginRegistrations.SingleOrDefaultAsync(
            value => value.Id == pluginId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-1102",
                "Plugin no encontrado.");
        if (!plugin.IsEnabled)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-1103",
                "El plugin está deshabilitado.");
        }

        var payload = string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson.Trim();
        if (payload.Length > 16_000)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-1104",
                "Los parámetros del plugin superan el tamaño máximo.");
        }

        var outcome = plugin.HookPoint.ToUpperInvariant() switch
        {
            "BEFORESAVE" or "BEFORE_SAVE" => "before-save-ok",
            "AFTERSAVE" or "AFTER_SAVE" => "after-save-ok",
            "VALIDATE" => "validate-ok",
            "NOTIFY" => "notify-ok",
            _ => $"hook-{plugin.HookPoint.ToLowerInvariant()}-ok",
        };
        var resultPayload =
            $"{{\"plugin\":\"{plugin.Code}\",\"hook\":\"{plugin.HookPoint}\",\"outcome\":\"{outcome}\",\"params\":{payload}}}";
        var log = new DomainEventLog(
            $"Plugin.Invoked.{plugin.Code}",
            "Plugin",
            plugin.Id,
            resultPayload,
            _clock.UtcNow);
        _db.DomainEventLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Plugin {PluginCode} invoked at hook {HookPoint} → {Outcome}",
            plugin.Code,
            plugin.HookPoint,
            outcome);
        return new PluginInvokeView(
            plugin.Id,
            plugin.Code,
            plugin.HookPoint,
            outcome,
            log.Id,
            resultPayload);
    }

    public async Task<ConfigTransportView> CreateConfigTransportAsync(
        CreateConfigTransportCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 40);
        var env = UnitOfMeasure.NormalizeCode(command.Environment, 20);
        if (await _db.ConfigTransports.AnyAsync(
                value => value.Code == code && value.Environment == env,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-1102",
                "Ya existe transporte de config para código/entorno.");
        }

        var transport = new ConfigTransport(
            code,
            env,
            command.PayloadJson,
            _clock.UtcNow);
        _db.ConfigTransports.Add(transport);
        await _db.SaveChangesAsync(cancellationToken);
        return ToConfigTransport(transport);
    }

    public async Task<IReadOnlyCollection<ConfigTransportView>> ListConfigTransportsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ConfigTransports.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToConfigTransport).ToArray();
    }

    // ——— Helpers ———

    private async Task<string> TakeNumberAsync(
        string documentType,
        string prefix,
        CancellationToken cancellationToken)
    {
        var range = await _db.DocumentNumberRanges.SingleOrDefaultAsync(
            value => value.DocumentType == documentType,
            cancellationToken);
        if (range is null)
        {
            range = new DocumentNumberRange(documentType, prefix);
            _db.DocumentNumberRanges.Add(range);
        }

        return range.TakeNext();
    }

    private async Task<ApprovalRequest> LoadPendingApprovalAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await _db.ApprovalRequests.SingleOrDefaultAsync(
            value => value.Id == requestId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0701",
                "Solicitud de aprobación no encontrada.");
        if (request.Status != ApprovalRequestStatus.Pending)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0702",
                "La solicitud ya fue decidida.");
        }

        return request;
    }

    private static string NormalizeApprovalKind(string? documentKind)
    {
        var kind = (documentKind ?? string.Empty).Trim().ToUpperInvariant();
        if (!ApprovalDocumentKinds.All.Contains(kind, StringComparer.Ordinal))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-STU-0707",
                "Tipo de documento no admitido para aprobación (pedido de compra, pedido de venta o factura de proveedor).");
        }

        return kind;
    }

    private async Task<decimal> ResolveDocumentAmountAsync(
        string kind,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        decimal? amount = kind switch
        {
            ApprovalDocumentKinds.PurchaseOrder =>
                await _db.PurchaseOrders.AnyAsync(value => value.Id == documentId, cancellationToken)
                    ? await _db.PurchaseOrderLines
                        .Where(value => value.PurchaseOrderId == documentId)
                        .SumAsync(value => value.RequestedQuantity * value.PurchasePrice, cancellationToken)
                    : null,
            ApprovalDocumentKinds.SalesOrder =>
                await _db.SalesOrders.AnyAsync(value => value.Id == documentId, cancellationToken)
                    ? await _db.SalesOrderLines
                        .Where(value => value.SalesOrderId == documentId)
                        .SumAsync(value => value.RequestedQuantity * value.CatalogPriceSnapshot, cancellationToken)
                    : null,
            ApprovalDocumentKinds.SupplierInvoice =>
                await _db.SupplierInvoiceStubs
                    .Where(value => value.Id == documentId)
                    .Select(value => (decimal?)value.Amount)
                    .SingleOrDefaultAsync(cancellationToken),
            _ => null,
        };
        return amount
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-STU-0708",
                "Documento no encontrado.");
    }

    private static void EnsureApproverRole(ApprovalRequest request, DecideApprovalCommand command)
    {
        if (string.IsNullOrEmpty(request.RequiredRole)
            || command.Roles.Contains(AppRoles.Admin, StringComparer.Ordinal)
            || command.Roles.Contains(request.RequiredRole, StringComparer.Ordinal))
        {
            return;
        }

        throw new LogisticsException(
            LogisticsErrorKind.Forbidden,
            "AAA-STU-0704",
            $"Esta solicitud la debe decidir el rango {request.RequiredRole}.");
    }

    private static async Task EnsureExistsAsync<TEntity>(
        IQueryable<TEntity> source,
        Guid id,
        string code,
        string message,
        CancellationToken cancellationToken)
        where TEntity : Entity
    {
        if (id == Guid.Empty
            || !await source.AnyAsync(value => value.Id == id, cancellationToken))
        {
            throw new LogisticsException(LogisticsErrorKind.NotFound, code, message);
        }
    }

    private static async Task EnsureOptionalExistsAsync<TEntity>(
        IQueryable<TEntity> source,
        Guid? id,
        string code,
        string message,
        CancellationToken cancellationToken)
        where TEntity : Entity
    {
        if (id is { } value)
        {
            await EnsureExistsAsync(source, value, code, message, cancellationToken);
        }
    }

    private async Task<Dictionary<Guid, string>> LoadMaterialUnitsAsync(
        IReadOnlyCollection<Guid> materialIds,
        string code,
        CancellationToken cancellationToken)
    {
        var ids = materialIds.Distinct().ToArray();
        var units = await (
                from material in _db.Materials.AsNoTracking()
                join unit in _db.UnitsOfMeasure.AsNoTracking() on material.UnitId equals unit.Id
                where ids.Contains(material.Id)
                select new { material.Id, unit.Code })
            .ToDictionaryAsync(value => value.Id, value => value.Code, cancellationToken);
        if (ids.Length == 0 || units.Count != ids.Length)
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                code,
                "Algún material indicado no existe.");
        }

        return units;
    }

    private static string UnitOrDefault(string? unitSnapshot, Guid materialId, Dictionary<Guid, string> units) =>
        string.IsNullOrWhiteSpace(unitSnapshot) ? units[materialId] : unitSnapshot;

    private static void EnsureHoldIsActive(QualityHold hold)
    {
        if (hold.Status != QualityHoldStatus.Held)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-STU-0509",
                "El bloqueo de calidad ya está liberado o rechazado.");
        }
    }

    private static void EnsureLines(int count, string code, string message)
    {
        if (count == 0)
        {
            throw new LogisticsException(LogisticsErrorKind.Validation, code, message);
        }
    }

    private async Task<RmaView> LoadRmaAsync(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _db.RmaDocuments.AsNoTracking()
            .SingleAsync(value => value.Id == id, cancellationToken);
        return (await MapRmasAsync([doc], cancellationToken)).Single();
    }

    private async Task<IReadOnlyCollection<RmaView>> MapRmasAsync(
        IReadOnlyList<RmaDocument> docs,
        CancellationToken cancellationToken)
    {
        if (docs.Count == 0)
        {
            return [];
        }

        var ids = docs.Select(value => value.Id).ToArray();
        var lines = await _db.RmaLines.AsNoTracking()
            .Where(value => ids.Contains(value.RmaId))
            .ToListAsync(cancellationToken);
        var byRma = lines.GroupBy(value => value.RmaId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        return docs.Select(doc =>
        {
            byRma.TryGetValue(doc.Id, out var rmaLines);
            return new RmaView(
                doc.Id,
                doc.Number,
                doc.CustomerId,
                doc.SalesOrderId,
                doc.Reason,
                doc.Status.ToString(),
                doc.CreatedUtc,
                (rmaLines ?? []).Select(value => new RmaLineView(
                    value.Id,
                    value.MaterialId,
                    value.Quantity,
                    value.UnitSnapshot)).ToArray());
        }).ToArray();
    }

    private async Task<FrameworkContractView> LoadFrameworkContractAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var contract = await _db.FrameworkContracts.AsNoTracking()
            .SingleAsync(value => value.Id == id, cancellationToken);
        var lines = await _db.FrameworkContractLines.AsNoTracking()
            .Where(value => value.ContractId == id)
            .ToListAsync(cancellationToken);
        return new FrameworkContractView(
            contract.Id,
            contract.Number,
            contract.SupplierId,
            contract.ValidFrom,
            contract.ValidTo,
            contract.Currency,
            contract.Status.ToString(),
            contract.CreatedUtc,
            lines.Select(value => new FrameworkContractLineView(
                value.Id,
                value.MaterialId,
                value.TargetQty,
                value.UnitPrice,
                value.UnitSnapshot)).ToArray());
    }

    private async Task<RfqView> LoadRfqAsync(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _db.RfqDocuments.AsNoTracking()
            .SingleAsync(value => value.Id == id, cancellationToken);
        var lines = await _db.RfqLines.AsNoTracking()
            .Where(value => value.RfqId == id)
            .ToListAsync(cancellationToken);
        return new RfqView(
            doc.Id,
            doc.Number,
            doc.Title,
            doc.Status.ToString(),
            doc.CreatedUtc,
            doc.DueDate,
            lines.Select(value => new RfqLineView(
                value.Id,
                value.MaterialId,
                value.Quantity,
                value.UnitSnapshot)).ToArray());
    }

    private async Task<JournalEntryView> LoadJournalAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entry = await _db.JournalEntries.AsNoTracking()
            .SingleAsync(value => value.Id == id, cancellationToken);
        var lines = await _db.JournalLines.AsNoTracking()
            .Where(value => value.JournalEntryId == id)
            .ToListAsync(cancellationToken);
        return new JournalEntryView(
            entry.Id,
            entry.Number,
            entry.Source,
            entry.Description,
            entry.PostingDate,
            entry.CreatedUtc,
            lines.Select(value => new JournalLineView(
                value.Id,
                value.GlAccountId,
                value.CostCenterId,
                value.Debit,
                value.Credit)).ToArray());
    }

    private async Task<BomView> LoadBomAsync(Guid id, CancellationToken cancellationToken)
    {
        var bom = await _db.BillOfMaterials.AsNoTracking()
            .SingleAsync(value => value.Id == id, cancellationToken);
        var components = await _db.BomComponents.AsNoTracking()
            .Where(value => value.BomId == id)
            .ToListAsync(cancellationToken);
        return new BomView(
            bom.Id,
            bom.Code,
            bom.ParentMaterialId,
            bom.Description,
            bom.IsActive,
            components.Select(value => new BomComponentView(
                value.Id,
                value.ComponentMaterialId,
                value.Quantity,
                value.UnitSnapshot)).ToArray());
    }

    private async Task<ProductionRouteView> LoadRouteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var route = await _db.ProductionRoutes.AsNoTracking()
            .SingleAsync(value => value.Id == id, cancellationToken);
        var ops = await _db.RouteOperations.AsNoTracking()
            .Where(value => value.RouteId == id)
            .OrderBy(value => value.Sequence)
            .ToListAsync(cancellationToken);
        return new ProductionRouteView(
            route.Id,
            route.Code,
            route.MaterialId,
            route.Name,
            route.IsActive,
            ops.Select(value => new RouteOperationView(
                value.Id,
                value.Sequence,
                value.WorkCenter,
                value.SetupMinutes,
                value.RunMinutes)).ToArray());
    }

    private async Task<(Guid? CompanyId, Guid? PlantId)> ResolveScopedOrgAsync(
        CancellationToken cancellationToken)
    {
        if (_requestContext.UserId is not Guid userId)
        {
            return (null, null);
        }

        var ctx = await _db.UserOrgContexts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        return ctx is null ? (null, null) : (ctx.CompanyId, ctx.PlantId);
    }

    private async Task<Guid?> ResolveScopedCompanyIdAsync(CancellationToken cancellationToken)
    {
        var scoped = await ResolveScopedOrgAsync(cancellationToken);
        return scoped.CompanyId;
    }

    private async Task<HashSet<Guid>?> ResolveScopedWarehouseIdsAsync(
        CancellationToken cancellationToken)
    {
        var scoped = await ResolveScopedOrgAsync(cancellationToken);
        if (scoped.PlantId is null && scoped.CompanyId is null)
        {
            return null;
        }

        IQueryable<PlantWarehouse> query = _db.PlantWarehouses.AsNoTracking();
        if (scoped.PlantId is Guid plantId)
        {
            query = query.Where(value => value.PlantId == plantId);
        }
        else if (scoped.CompanyId is Guid companyId)
        {
            var plantIds = _db.Plants.AsNoTracking()
                .Where(value => value.CompanyId == companyId)
                .Select(value => value.Id);
            query = query.Where(value => plantIds.Contains(value.PlantId));
        }

        var ids = await query.Select(value => value.WarehouseId).Distinct().ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private static TaxCodeView ToTax(TaxCode value) =>
        new(value.Id, value.Code, value.Name, value.RatePercent, value.CountryCode, value.IsActive);

    private static PriceConditionView ToPriceCondition(PriceCondition value) =>
        new(
            value.Id,
            value.Code,
            value.Name,
            value.PercentOff,
            value.AmountOff,
            value.MaterialId,
            value.CustomerId,
            value.ValidFrom,
            value.ValidTo,
            value.IsActive);

    private static ThreeWayMatchView ToThreeWayMatch(ThreeWayMatchRecord value) =>
        new(
            value.Id,
            value.PurchaseOrderId,
            value.GoodsReceiptId,
            value.SupplierInvoiceId,
            value.PoAmount,
            value.GrAmount,
            value.InvoiceAmount,
            value.Matched,
            value.Notes,
            value.CreatedUtc);

    private static MaterialValuationView ToValuation(MaterialValuation value) =>
        new(
            value.Id,
            value.MaterialId,
            value.WarehouseId,
            value.Method.ToString(),
            value.StandardPrice,
            value.MovingAveragePrice,
            value.UnitValue,
            value.Currency);

    private static SupplierInvoiceStubView ToSupplierInvoice(SupplierInvoiceStub value) =>
        new(
            value.Id,
            value.Number,
            value.SupplierId,
            value.PurchaseOrderId,
            value.Amount,
            value.Currency,
            value.InvoiceDate);

    private static GlAccountView ToGl(GlAccount value) =>
        new(value.Id, value.Code, value.Name, value.AccountType.ToString(), value.IsActive);

    private static CostCenterView ToCostCenter(CostCenter value) =>
        new(value.Id, value.Code, value.Name, value.CompanyId, value.IsActive);

    private static ProductionOrderView ToProductionOrder(ProductionOrder value) =>
        new(
            value.Id,
            value.Number,
            value.MaterialId,
            value.BomId,
            value.RouteId,
            value.Quantity,
            value.UnitSnapshot,
            value.Status.ToString(),
            value.CreatedUtc);

    private static InspectionPlanView ToInspectionPlan(InspectionPlan value) =>
        new(value.Id, value.Code, value.MaterialId, value.Name, value.IsActive);

    private static QualityHoldView ToQualityHold(
        QualityHold value,
        string lotCode,
        int stockBalancesUpdated = 0) =>
        new(
            value.Id,
            value.LotId,
            lotCode,
            value.MaterialId,
            value.Reason,
            value.Status.ToString(),
            value.CreatedUtc,
            stockBalancesUpdated);

    private static QualityCertificateView ToQualityCert(QualityCertificate value) =>
        new(value.Id, value.Number, value.MaterialId, value.LotId, value.Result, value.IssuedUtc);

    private static PlantView ToPlant(Plant value) =>
        new(value.Id, value.CompanyId, value.Code, value.Name, value.IsActive);

    private static UserOrgContextView ToUserOrg(UserOrgContext value) =>
        new(value.Id, value.UserId, value.CompanyId, value.PlantId);

    private static ApprovalRuleView ToApprovalRule(ApprovalRule value) =>
        new(
            value.Id,
            value.DocumentKind,
            value.MinAmount,
            value.RequiredRole,
            value.Name,
            value.IsActive);

    private static ApprovalRequestView ToApprovalRequest(ApprovalRequest value) =>
        new(
            value.Id,
            value.DocumentKind,
            value.DocumentId,
            value.Amount,
            value.Summary,
            value.Status.ToString(),
            value.CreatedUtc,
            value.DecidedByUserId,
            value.DecidedUtc,
            value.RequestedByUserId,
            value.RequiredRole);

    private static IntegrationConnectorView ToConnector(IntegrationConnector value) =>
        new(value.Id, value.Code, value.Name, value.Kind, value.Version, value.IsActive);

    private static IntegrationInboxView ToInbox(IntegrationInboxMessage value) =>
        new(value.Id, value.ConnectorId, value.MessageType, value.Processed, value.ReceivedUtc);

    private static EmployeeProfileView ToEmployee(EmployeeProfile value) =>
        new(
            value.Id,
            value.UserId,
            value.EmployeeNumber,
            value.DisplayName,
            value.Department,
            value.HireDate,
            value.IsActive);

    private static DomainEventLogView ToDomainEvent(DomainEventLog value) =>
        new(value.Id, value.EventName, value.AggregateType, value.AggregateId, value.OccurredUtc);

    private static PluginRegistrationView ToPlugin(PluginRegistration value) =>
        new(value.Id, value.Code, value.Name, value.HookPoint, value.IsEnabled);

    private static ConfigTransportView ToConfigTransport(ConfigTransport value) =>
        new(value.Id, value.Code, value.Environment, value.CreatedUtc);
}