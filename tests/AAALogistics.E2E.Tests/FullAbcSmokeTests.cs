namespace AAALogistics.E2E.Tests;

/// <summary>
/// Smoke de presencia documental Full ABC (ADR/runbook/catálogo).
/// NO valida comportamiento runtime: eso vive en Domain/Integration.
/// </summary>
public sealed class FullAbcSmokeTests
{
    [Fact]
    public void TierA_Didactic_Docs_Present_Only()
    {
        Assert.True(
            File.Exists(RepoPath("docs", "adr", "0023-alcance-completo-didactico.md")));
    }

    [Fact]
    public void TierB_Appendix_Adr0024_Present_Only() =>
        Assert.True(File.Exists(RepoPath("docs", "adr", "0024-apendice-lab-in.md")));

    [Fact]
    public void TierC_Industrial_Docs_Honesty_Markers_Present_Only()
    {
        Assert.True(File.Exists(RepoPath("docs", "adr", "0025-industrial-in.md")));
        Assert.True(File.Exists(RepoPath("docs", "es", "threat-model-c.md")));
        Assert.True(File.Exists(RepoPath("docs", "es", "runbooks", "ha-postgres.md")));
        Assert.True(File.Exists(RepoPath("docs", "es", "guias", "31-wcag-portal.md")));
        Assert.True(File.Exists(RepoPath("docs", "es", "guias", "12-integraciones-sap.md")));
        var ha = File.ReadAllText(RepoPath("docs", "es", "runbooks", "ha-postgres.md"));
        Assert.Contains("AAA_DISABLE_INSTANCE_LOCK", ha, StringComparison.Ordinal);
        Assert.DoesNotContain("AAA_DISABLE_SINGLE_INSTANCE_LOCK", ha, StringComparison.Ordinal);
        var catalog = File.ReadAllText(RepoPath("docs", "es", "requisitos.md"));
        Assert.Contains("RF-FAC-010", catalog, StringComparison.Ordinal);
        Assert.Contains("| Parcial |", catalog, StringComparison.Ordinal);
        Assert.Contains("RF-OPS-002", catalog, StringComparison.Ordinal);
        Assert.Contains("RF-REG-010", catalog, StringComparison.Ordinal);
    }

    private static string RepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
            && !File.Exists(Path.Combine(dir.FullName, "AAALogistics.slnx"))
            && !File.Exists(Path.Combine(dir.FullName, "AAALogistics.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine([dir!.FullName, .. segments]);
    }
}
