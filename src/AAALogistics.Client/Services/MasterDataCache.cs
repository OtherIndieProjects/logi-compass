namespace AAALogistics.Client.Services;

internal sealed class MasterDataCache
{
    private readonly LogisticsApiClient _client;

    public MasterDataCache(LogisticsApiClient client)
    {
        _client = client;
    }

    public IReadOnlyList<UnitRow> Units { get; private set; } = [];

    public IReadOnlyList<MaterialRow> Materials { get; private set; } = [];

    public IReadOnlyList<WarehouseRow> Warehouses { get; private set; } = [];

    public IReadOnlyList<PartyRow> Suppliers { get; private set; } = [];

    public IReadOnlyList<PartyRow> Customers { get; private set; } = [];

    public DateTimeOffset? LoadedAt { get; private set; }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var unitsTask = SafeList(() => _client.GetUnitsAsync(cancellationToken));
        var materialsTask = SafeList(() => _client.GetMaterialsAsync(cancellationToken));
        var warehousesTask = SafeList(() => _client.GetWarehousesAsync(cancellationToken));
        var suppliersTask = SafeList(() => _client.GetSuppliersAsync(cancellationToken));
        var customersTask = SafeList(() => _client.GetCustomersAsync(cancellationToken));
        await Task.WhenAll(
            unitsTask,
            materialsTask,
            warehousesTask,
            suppliersTask,
            customersTask);
        Units = unitsTask.Result;
        Materials = materialsTask.Result;
        Warehouses = warehousesTask.Result;
        Suppliers = suppliersTask.Result;
        Customers = customersTask.Result;
        var unitCodes = Units.ToDictionary(unit => unit.Id, unit => unit.Code);
        foreach (var material in Materials)
        {
            material.UnitCode = unitCodes.TryGetValue(material.UnitId, out var code)
                ? code
                : string.Empty;
        }

        LoadedAt = DateTimeOffset.Now;
    }

    private static async Task<IReadOnlyList<T>> SafeList<T>(
        Func<Task<IReadOnlyList<T>>> load)
    {
        try
        {
            return await load();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            return [];
        }
    }
}
