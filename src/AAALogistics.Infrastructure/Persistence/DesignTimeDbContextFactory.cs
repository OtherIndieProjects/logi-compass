using AAALogistics.Application.Logistics;
using AAALogistics.Infrastructure.Auditing;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Persistence;

internal sealed class DesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Database=aaalogistics_design;Username=aaa_app;Password=design")
            .UseSnakeCaseNamingConvention()
            .Options;
        var request = new DesignTimeRequestContext();
        var clock = new SystemClock();
        var keyPath = Path.Combine(
            Path.GetTempPath(),
            "aaa-logistics-design",
            "pii-master.key");
        var encryption = new PersonDataEncryptionService(
            new PiiMasterKeyProvider(
                Options.Create(new AuditOptions { PiiMasterKeyPath = keyPath })),
            clock);
        var audit = new AuditChainService(encryption, request, clock);
        return new ApplicationDbContext(options, audit, request);
    }

    private sealed class DesignTimeRequestContext : IRequestContext
    {
        public Guid? UserId => null;

        public Guid? CustomerId => null;

        public Guid? CompanyId => null;

        public string? IpAddress => null;
    }
}
