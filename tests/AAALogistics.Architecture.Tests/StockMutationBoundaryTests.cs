using System.Text.RegularExpressions;

namespace AAALogistics.Architecture.Tests;

public class StockMutationBoundaryTests
{
    private static readonly Regex ApplyCall =
        new(@"\.\s*Apply\s*\(", RegexOptions.Compiled);

    [Fact]
    public void StockBalance_Apply_Call_Sites_Only_Appear_In_PostingService()
    {
        var infrastructureRoot = LocateInfrastructureSource();
        Assert.True(
            Directory.Exists(infrastructureRoot),
            $"No se encontró el código fuente de Infrastructure en {infrastructureRoot}");

        var offenders = new List<string>();
        foreach (var path in Directory.EnumerateFiles(
                     infrastructureRoot,
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            if (fileName.Equals("PostingService.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!ApplyCall.IsMatch(line))
                {
                    continue;
                }

                // Domain StockBalance.Apply is the only Apply used in Infrastructure
                // for quantity mutation; exclude false positives on other verbs if any.
                offenders.Add($"{fileName}:{index + 1}: {line.Trim()}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "StockBalance.Apply solo puede llamarse desde PostingService.cs. Violaciones:\n"
            + string.Join(Environment.NewLine, offenders));
    }

    private static string LocateInfrastructureSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src",
                "AAALogistics.Infrastructure");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "AAALogistics.Infrastructure"));
    }
}
