using System.Security.Claims;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Security;

public sealed class HttpRequestContext(IHttpContextAccessor httpContextAccessor)
    : IRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor =
        httpContextAccessor
        ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    public Guid? UserId =>
        ParseClaim(JwtClaimNames.Subject);

    public Guid? CustomerId =>
        ParseClaim(JwtClaimNames.CustomerId);

    public Guid? CompanyId =>
        ParseClaim(JwtClaimNames.CompanyId);

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private Guid? ParseClaim(string claimType)
    {
        var value = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(claimType);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}

public sealed class CustomerRlsSession(
    ApplicationDbContext dbContext,
    IRequestContext requestContext) : ICustomerRlsSession
{
    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    public async Task ApplyAsync(
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "SET LOCAL app.customer_id requiere una transacción.");
        }

        var customerId = _requestContext.CustomerId?.ToString("D")
            ?? string.Empty;
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.customer_id', {customerId}, true)",
            cancellationToken);
    }
}
