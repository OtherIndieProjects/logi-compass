using AAALogistics.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace AAALogistics.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>, IPersonDataEntity
{
    public Guid? CustomerId { get; set; }

    public Guid? CompanyId { get; set; }

    public string? SuspensionReason { get; set; }

    public DateTimeOffset? SuspendedUtc { get; set; }

    Guid IPersonDataEntity.PersonId => Id;
}
