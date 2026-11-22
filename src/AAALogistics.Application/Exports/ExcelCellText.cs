namespace AAALogistics.Application.Exports;

public static class ExcelCellText
{
    public static string Neutralize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var trimmed = value.AsSpan().TrimStart();
        if (trimmed.Length > 0 && IsFormulaLead(trimmed[0]))
        {
            return "'" + value;
        }

        return value;
    }

    private static bool IsFormulaLead(char value) =>
        value is '=' or '+' or '-' or '@';
}
