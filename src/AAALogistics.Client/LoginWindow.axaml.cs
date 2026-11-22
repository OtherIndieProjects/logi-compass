using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using AAALogistics.Client.Services;

namespace AAALogistics.Client;

internal partial class LoginWindow : Window
{
    private readonly LogisticsApiClient _client = new();

    public LoginWindow()
    {
        InitializeComponent();
        Title = BrandTheme.ShortName;
        TitleText.Text = BrandTheme.ShortName;
        TitleText.Foreground = BrandTheme.Brush(BrandTheme.PrimaryDark);
        SubtitleText.Text = BrandTheme.Tagline;
        SubtitleText.Foreground = BrandTheme.Brush(BrandTheme.TextSecondary);
        Background = BrandTheme.Brush(BrandTheme.Background);
        LoginButton.Background = BrandTheme.Brush(BrandTheme.Primary);
        LoginButton.Foreground = Brushes.White;
        StatusText.Foreground = BrandTheme.Brush(BrandTheme.Error);
    }

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        StatusText.Text = string.Empty;
        try
        {
            var userName = UserNameBox.Text?.Trim() ?? string.Empty;
            var password = PasswordBox.Text ?? string.Empty;
            if (userName.Length == 0 || password.Length == 0)
            {
                await AppDialog.ShowMessageAsync(
                    this,
                    BrandTheme.ShortName,
                    "Indique usuario y contraseña.");
                return;
            }

            await _client.LoginAsync(userName, password);
            OpenMain(userName);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            var message = exception is TaskCanceledException
                ? "El servidor no respondió a tiempo."
                : exception.Message;
            StatusText.Text = message;
            await AppDialog.ShowMessageAsync(this, "No se pudo entrar", message);
        }
        finally
        {
            if (IsVisible)
            {
                LoginButton.IsEnabled = true;
            }
        }
    }

    private void OpenMain(string userName)
    {
        var main = new MainWindow(_client, userName);
        if (global::Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = main;
        }

        main.Show();
        Close();
    }
}
