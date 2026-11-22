using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;

namespace AAALogistics.Client.Views;

internal partial class RegistrationView : UserControl
{
    private readonly LogisticsApiClient _client;
    private IReadOnlyList<RegistrationRequestRow> _rows = [];

    public RegistrationView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Registration);
        OfficeUi.BindHelp(ApproveHelpHost, OfficeHelpTexts.Keys.FieldRegistrationApprove);
        OfficeUi.SetTextColumns(
            Grid,
            ("Empresa", "CompanyName"),
            ("Contacto", "ContactName"),
            ("Correo", "Email"),
            ("Estado", "StatusEs"));
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        try
        {
            ApproveButton.IsVisible = _client.Permissions.Has(
                AppPermissionNames.RegistrationApprove);
            RejectButton.IsVisible = _client.Permissions.Has(
                AppPermissionNames.RegistrationReject);

            _rows = await _client.GetRegistrationRequestsAsync();
            var projected = _rows.Select(row => new
            {
                row.CompanyName,
                row.ContactName,
                row.Email,
                StatusEs = OfficeUi.StatusToEs(row.Status),
            }).ToList();
            Grid.ItemsSource = projected;
            EmptyHint.IsVisible = projected.Count == 0;
            Grid.IsVisible = projected.Count > 0;
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnApproveClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var row))
        {
            return;
        }

        try
        {
            var decision = await _client.ApproveRegistrationAsync(owner, row.Id);
            var password = string.IsNullOrWhiteSpace(decision.TemporaryPassword)
                ? string.Empty
                : $" Contraseña temporal: {decision.TemporaryPassword}";
            await UiHelpers.ShowInfoAsync(
                this,
                $"Solicitud aprobada ({OfficeUi.StatusToEs(decision.Status)}).{password}");
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRejectClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var row))
        {
            return;
        }

        var reason = ReasonBox.Text?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            StatusText.Text = "Indique motivo de rechazo.";
            return;
        }

        try
        {
            await _client.RejectRegistrationAsync(owner, row.Id, reason);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private bool TrySelected(out RegistrationRequestRow row)
    {
        row = null!;
        if (Grid.SelectedIndex < 0 || Grid.SelectedIndex >= _rows.Count)
        {
            StatusText.Text = "Seleccione una solicitud.";
            return false;
        }

        row = _rows[Grid.SelectedIndex];
        return true;
    }
}
