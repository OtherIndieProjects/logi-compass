using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Sales;

public enum PrepLockMode
{
    Exclusive = 0,
    Open = 1,
}

public sealed class OrderPrepSession : Entity
{
    private OrderPrepSession()
    {
    }

    public OrderPrepSession(
        Guid salesOrderId,
        Guid userId,
        string userName,
        PrepLockMode mode,
        DateTimeOffset joinedUtc)
    {
        SalesOrderId = salesOrderId == Guid.Empty
            ? throw new ArgumentException("Pedido obligatorio.", nameof(salesOrderId))
            : salesOrderId;
        UserId = userId == Guid.Empty
            ? throw new ArgumentException("Usuario obligatorio.", nameof(userId))
            : userId;
        UserName = UnitOfMeasure.Required(userName, nameof(userName), 100);
        Mode = mode;
        JoinedUtc = joinedUtc;
        IsActive = true;
    }

    public Guid SalesOrderId { get; private set; }

    public Guid UserId { get; private set; }

    public string UserName { get; private set; } = string.Empty;

    public PrepLockMode Mode { get; private set; }

    public DateTimeOffset JoinedUtc { get; private set; }

    public DateTimeOffset? LeftUtc { get; private set; }

    public bool IsActive { get; private set; }

    public void Leave(DateTimeOffset utc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        LeftUtc = utc;
    }
}
