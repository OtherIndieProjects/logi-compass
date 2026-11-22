using System.IO.Compression;
using System.Text;
using AAALogistics.Application.Logistics;
using AAALogistics.Infrastructure.Exports;

namespace AAALogistics.Integration.Tests;

public sealed class ExcelExportTests
{
    [Fact]
    public void Materials_Workbook_Neutralizes_Formula_Text()
    {
        var exporter = new ExcelExportService();
        var bytes = exporter.CreateMaterialsWorkbook(
        [
            new MaterialView(
                Guid.CreateVersion7(),
                "=CMD",
                "+name",
                "+desc",
                Guid.CreateVersion7(),
                1.5m,
                "EUR",
                true,
                "@ext",
                false),
        ]);

        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
        var xml = ReadWorkbookXml(bytes);
        Assert.Contains("quotePrefix=\"1\"", xml, StringComparison.Ordinal);
        Assert.Contains(">=CMD</", xml, StringComparison.Ordinal);
        Assert.Contains(">+name<", xml, StringComparison.Ordinal);
        Assert.Contains(">+desc<", xml, StringComparison.Ordinal);
        Assert.Contains(">@ext<", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<x:f>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<x:f ", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Stock_Workbook_Keeps_Quantities_Numeric()
    {
        var exporter = new ExcelExportService();
        var bytes = exporter.CreateStockWorkbook(
        [
            new StockView(
                Guid.CreateVersion7(),
                "-NEG",
                Guid.CreateVersion7(),
                "MAIN",
                3.5m,
                1m),
        ]);

        var xml = ReadWorkbookXml(bytes);
        Assert.Contains("quotePrefix=\"1\"", xml, StringComparison.Ordinal);
        Assert.Contains(">-NEG<", xml, StringComparison.Ordinal);
        Assert.Contains(">3.5<", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<x:f>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<x:f ", xml, StringComparison.Ordinal);
    }

    private static string ReadWorkbookXml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var builder = new StringBuilder();
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            builder.Append(reader.ReadToEnd());
        }

        return builder.ToString();
    }
}
