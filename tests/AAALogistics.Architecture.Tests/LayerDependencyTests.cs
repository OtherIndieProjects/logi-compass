using NetArchTest.Rules;

namespace AAALogistics.Architecture.Tests;

public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly Domain = typeof(Domain.Common.Entity).Assembly;
    private static readonly System.Reflection.Assembly Application = typeof(Application.ApplicationServiceCollectionExtensions).Assembly;
    private static readonly System.Reflection.Assembly Infrastructure = typeof(Infrastructure.InfrastructureServiceCollectionExtensions).Assembly;
    private static readonly System.Reflection.Assembly Api = typeof(Api.Hosting.ApiEndpointRouteBuilderExtensions).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Outer_Layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AAALogistics.Application",
                "AAALogistics.Infrastructure",
                "AAALogistics.Integrations",
                "AAALogistics.Api",
                "AAALogistics.Host",
                "AAALogistics.Client",
                "AAALogistics.Portal")
            .GetResult();

        Assert.True(result.IsSuccessful, Format(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Ui()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AAALogistics.Infrastructure",
                "AAALogistics.Integrations",
                "AAALogistics.Api",
                "AAALogistics.Host",
                "AAALogistics.Client",
                "AAALogistics.Portal")
            .GetResult();

        Assert.True(result.IsSuccessful, Format(result));
    }

    [Fact]
    public void Api_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types.InAssembly(Api)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AAALogistics.Infrastructure",
                "AAALogistics.Integrations",
                "AAALogistics.Host",
                "AAALogistics.Client",
                "AAALogistics.Portal")
            .GetResult();

        Assert.True(result.IsSuccessful, Format(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api_Or_Ui()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AAALogistics.Api",
                "AAALogistics.Host",
                "AAALogistics.Client",
                "AAALogistics.Portal")
            .GetResult();

        Assert.True(result.IsSuccessful, Format(result));
    }

    [Fact]
    public void Domain_Defines_Admin_Table_Blacklist_Precondition()
    {
        Assert.Contains(
            "stock_balances",
            AAALogistics.Domain.Security.AdminTableBlacklist.Tables);
        Assert.Contains(
            "audit_log_entries",
            AAALogistics.Domain.Security.AdminTableBlacklist.Tables);
        Assert.Contains(
            "refresh_tokens",
            AAALogistics.Domain.Security.AdminTableBlacklist.Tables);
    }

    [Fact]
    public void Api_Has_No_Generic_Table_Editor_Endpoints()
    {
        var suspicious = Types.InAssembly(Api)
            .That()
            .HaveNameMatching(".*TableEditor.*|.*GenericAdmin.*|.*RawSql.*")
            .GetTypes()
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(suspicious);
    }

    private static string Format(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
