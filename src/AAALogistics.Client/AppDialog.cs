using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AAALogistics.Client.Services;

namespace AAALogistics.Client;

internal static class AppDialog
{
    public static Task ShowMessageAsync(
        Window owner,
        string title,
        string message)
    {
        var close = new Button
        {
            Content = "Cerrar",
            Classes = { "primary" },
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 96,
        };
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrandTheme.Brush(BrandTheme.Text),
        };
        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16,
        };
        panel.Children.Add(text);
        panel.Children.Add(close);
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = BrandTheme.Brush(BrandTheme.Background),
            Foreground = BrandTheme.Brush(BrandTheme.Text),
            Content = panel,
        };
        close.Click += (_, _) => dialog.Close();
        return dialog.ShowDialog(owner);
    }

    public static async Task<string?> PromptAsync(
        Window owner,
        string title,
        string message,
        string defaultValue = "")
    {
        string? result = null;
        var input = new TextBox
        {
            Text = defaultValue,
            PlaceholderText = "Motivo",
            MinWidth = 320,
        };
        var ok = new Button
        {
            Content = "Aceptar",
            Classes = { "primary" },
            MinWidth = 96,
        };
        var cancel = new Button
        {
            Content = "Cancelar",
            Classes = { "secondary" },
            MinWidth = 96,
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 12,
        };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(input);
        panel.Children.Add(buttons);
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = BrandTheme.Brush(BrandTheme.Background),
            Content = panel,
        };
        ok.Click += (_, _) =>
        {
            result = input.Text?.Trim();
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
