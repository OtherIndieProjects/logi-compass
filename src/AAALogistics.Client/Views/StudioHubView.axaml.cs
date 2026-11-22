using Avalonia.Controls;
using Avalonia.Input;
using AAALogistics.Client.Services;

namespace AAALogistics.Client.Views;

internal partial class StudioHubView : UserControl
{
    private readonly LogisticsApiClient? _client;

    public event Action<string>? NavigateRequested;

    public StudioHubView()
        : this(null)
    {
    }

    public StudioHubView(LogisticsApiClient? client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.StudioHub);
        OfficeUi.SetTextColumns(
            Grid,
            ("Fase", "Phase"),
            ("Módulo", "Title"),
            ("Estado", "StatusEs"),
            ("Resumen", "Summary"));
        Refresh();
    }

    public void Refresh()
    {
        StatusText.Text = string.Empty;
        IEnumerable<StudioModule> source = StudioModuleRegistry.All;
        if (_client is not null)
        {
            source = StudioModuleRegistry.VisibleFor(_client.Permissions);
        }

        var rows = source
            .Select(m => new
            {
                m.Phase,
                m.Title,
                StatusEs = m.Enabled ? "Operativo" : "Pendiente de fase",
                m.Summary,
                m.NavTag,
                m.Enabled,
            })
            .ToList();
        Grid.ItemsSource = rows;
        EmptyHint.IsVisible = rows.Count == 0;
        Grid.IsVisible = rows.Count > 0;
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Grid.SelectedItem is null)
        {
            return;
        }

        var tag = Grid.SelectedItem.GetType().GetProperty("NavTag")?.GetValue(Grid.SelectedItem) as string;
        var enabled = Grid.SelectedItem.GetType().GetProperty("Enabled")?.GetValue(Grid.SelectedItem) as bool?;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (enabled != true && !string.Equals(tag, "studio", StringComparison.Ordinal))
        {
            StatusText.Text =
                "Este módulo aún no está habilitado en esta instalación. Consulte la ayuda (i) del mapa Studio.";
            return;
        }

        if (_client is not null
            && !string.Equals(tag, "studio", StringComparison.Ordinal)
            && !StudioModuleRegistry.IsAvailable(tag, _client.Permissions))
        {
            StatusText.Text = "No tiene permiso para abrir este módulo.";
            return;
        }

        NavigateRequested?.Invoke(tag);
    }
}
