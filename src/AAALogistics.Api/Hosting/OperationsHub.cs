using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AAALogistics.Api.Hosting;

/// <summary>
/// Real-time ops notifications (ADR 0025). Clients subscribe to warehouse channels.
/// </summary>
[Authorize(Policy = AuthorizationPolicyNames.InternalApi)]
public sealed class OperationsHub : Hub
{
    public Task SubscribeWarehouse(string warehouseId)
    {
        if (Context.User?.HasClaim(JwtClaimNames.Permission, AppPermissions.StockRead) != true)
        {
            throw new HubException("Sin permiso para consultar stock.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, WarehouseGroup(warehouseId));
    }

    public Task UnsubscribeWarehouse(string warehouseId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, WarehouseGroup(warehouseId));

    public static string WarehouseGroup(string warehouseId) =>
        Guid.TryParse(warehouseId, out var id)
            ? $"wh:{id:D}"
            : $"wh:{warehouseId.Trim()}";

    public static string WarehouseGroup(Guid warehouseId) =>
        $"wh:{warehouseId:D}";
}

public sealed class SignalROperationsRealtime(
    IHubContext<OperationsHub> hub) : IOperationsRealtime
{
    private readonly IHubContext<OperationsHub> _hub =
        hub ?? throw new ArgumentNullException(nameof(hub));

    public Task NotifyStockChangedAsync(
        Guid warehouseId,
        string message,
        CancellationToken cancellationToken = default) =>
        _hub.Clients
            .Group(OperationsHub.WarehouseGroup(warehouseId))
            .SendAsync("stockChanged", message, cancellationToken);
}

public sealed class NoOpOperationsRealtime : IOperationsRealtime
{
    public Task NotifyStockChangedAsync(
        Guid warehouseId,
        string message,
        CancellationToken cancellationToken = default)
    {
        _ = warehouseId;
        _ = message;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
