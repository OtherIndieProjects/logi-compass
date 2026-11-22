using AAALogistics.Application.Exports;

namespace AAALogistics.Application.Tests;

public sealed class ExcelCellTextTests
{
    [Theory]
    [InlineData("=cmd", "'=cmd")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@user", "'@user")]
    [InlineData("  =cmd", "'  =cmd")]
    [InlineData("ABC", "ABC")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Neutralize_Prefixes_Formula_Leads(string? input, string expected)
    {
        Assert.Equal(expected, ExcelCellText.Neutralize(input));
    }
}
