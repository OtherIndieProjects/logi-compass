namespace AAALogistics.Domain.Common;

/// <summary>
/// Marks an entity whose audit snapshot contains data protected with a
/// per-person encryption key.
/// </summary>
public interface IPersonDataEntity
{
    Guid PersonId { get; }
}
