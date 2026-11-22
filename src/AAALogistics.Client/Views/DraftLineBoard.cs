using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace AAALogistics.Client.Views;

/// <summary>Borrador de líneas multi-ítem para crear Quote/SO/PO/factura.</summary>
internal sealed class DraftLineBoard
{
    private readonly List<DraftLine> _lines = [];

    public IReadOnlyList<DraftLine> Lines => _lines;

    public int Count => _lines.Count;

    public void Clear() => _lines.Clear();

    public void Replace(IEnumerable<DraftLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
    }

    public void Add(DraftLine line) => _lines.Add(line);

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _lines.Count)
        {
            return false;
        }

        _lines.RemoveAt(index);
        return true;
    }

    public void Bind(DataGrid grid)
    {
        OfficeUi.SetTextColumns(
            grid,
            ("Concepto", nameof(DraftLine.Label)),
            ("Cant.", nameof(DraftLine.Quantity)),
            ("Precio", nameof(DraftLine.UnitPrice)));
        grid.ItemsSource = null;
        grid.ItemsSource = _lines.ToList();
    }

    public sealed record DraftLine(
        Guid MaterialId,
        string Label,
        decimal Quantity,
        decimal UnitPrice,
        string? Description = null);
}
