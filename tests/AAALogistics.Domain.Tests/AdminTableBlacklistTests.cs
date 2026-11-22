using AAALogistics.Domain.Security;

namespace AAALogistics.Domain.Tests;

public sealed class AdminTableBlacklistTests
{
    [Fact]
    public void Stock_And_Audit_Tables_Are_Forbidden()
    {
        Assert.True(AdminTableBlacklist.IsForbidden("stock_movements"));
        Assert.True(AdminTableBlacklist.IsForbidden("stock_balances"));
        Assert.True(AdminTableBlacklist.IsForbidden("stock_reservations"));
        Assert.True(AdminTableBlacklist.IsForbidden("invoices"));
        Assert.True(AdminTableBlacklist.IsForbidden("invoice_lines"));
        Assert.True(AdminTableBlacklist.IsForbidden("handling_units"));
        Assert.True(AdminTableBlacklist.IsForbidden("lots"));
        Assert.True(AdminTableBlacklist.IsForbidden("audit_log_entries"));
        Assert.True(AdminTableBlacklist.IsForbidden("refresh_tokens"));
        Assert.False(AdminTableBlacklist.IsForbidden("materials"));
    }
}
