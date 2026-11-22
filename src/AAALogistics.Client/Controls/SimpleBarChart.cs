using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AAALogistics.Client.Services;

namespace AAALogistics.Client.Controls;

/// <summary>
/// Lightweight vertical bar chart using Avalonia primitives (no LiveCharts dependency).
/// </summary>
internal sealed class SimpleBarChart : UserControl
{
    private readonly Grid _plot;
    private readonly TextBlock _empty;

    public SimpleBarChart()
    {
        _plot = new Grid
        {
            Height = 120,
            ColumnDefinitions = new ColumnDefinitions(),
        };
        _empty = new TextBlock
        {
            Classes = { "muted" },
            Text = "Sin datos",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsVisible = false,
        };
        var host = new Grid { Height = 132 };
        host.Children.Add(_plot);
        host.Children.Add(_empty);
        Content = host;
    }

    public void SetSeries(IReadOnlyList<(string Label, double Value, string? ColorHex)> items)
    {
        _plot.Children.Clear();
        _plot.ColumnDefinitions.Clear();

        if (items.Count == 0 || items.All(item => item.Value <= 0))
        {
            _empty.IsVisible = true;
            _plot.IsVisible = false;
            return;
        }

        _empty.IsVisible = false;
        _plot.IsVisible = true;

        var max = Math.Max(1, items.Max(item => item.Value));
        for (var index = 0; index < items.Count; index++)
        {
            _plot.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var item = items[index];
            var ratio = item.Value / max;
            var barHeight = Math.Max(4, Math.Round(96 * ratio));
            var color = string.IsNullOrWhiteSpace(item.ColorHex)
                ? BrandTheme.Accent
                : item.ColorHex!;

            var column = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
                Spacing = 4,
                Margin = new Avalonia.Thickness(4, 0),
            };
            Grid.SetColumn(column, index);

            var valueLabel = new TextBlock
            {
                Text = FormatValue(item.Value),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = BrandTheme.Brush(BrandTheme.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var bar = new Border
            {
                Height = barHeight,
                Background = BrandTheme.Brush(color),
                CornerRadius = new Avalonia.CornerRadius(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 18,
            };

            var label = new TextBlock
            {
                Text = item.Label,
                Classes = { "muted" },
                FontSize = 10,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 72,
            };

            column.Children.Add(valueLabel);
            column.Children.Add(bar);
            column.Children.Add(label);
            _plot.Children.Add(column);
        }
    }

    private static string FormatValue(double value) =>
        Math.Abs(value % 1) < 0.001
            ? ((int)Math.Round(value)).ToString()
            : value.ToString("0.#");
}
