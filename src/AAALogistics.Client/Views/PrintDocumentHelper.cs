using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AAALogistics.Client.Services;

namespace AAALogistics.Client.Views;

/// <summary>
/// Diálogo Imprimir distinto de «PDF…»: opciones de impresora Windows / abrir PDF.
/// </summary>
internal static class PrintDocumentHelper
{
    public enum PrintChoice
    {
        Cancel,
        PrintToPrinter,
        OpenPdf,
    }

    public static async Task ShowAsync(
        Control control,
        byte[] pdfBytes,
        string title,
        string suggestedFileName)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
        {
            await UiHelpers.ShowInfoAsync(control, "El documento PDF está vacío.");
            return;
        }

        var owner = UiHelpers.FindWindow(control);
        if (owner is null)
        {
            return;
        }

        var safeName = string.IsNullOrWhiteSpace(suggestedFileName)
            ? "documento.pdf"
            : suggestedFileName.Trim();
        if (!safeName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".pdf";
        }

        var path = Path.Combine(Path.GetTempPath(), "logi-compass-print", safeName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, pdfBytes);

        var choice = await ShowOptionsAsync(owner, title);
        switch (choice)
        {
            case PrintChoice.PrintToPrinter:
                await PrintOrFallbackAsync(control, path);
                break;
            case PrintChoice.OpenPdf:
                OpenWithDefaultViewer(path);
                await UiHelpers.ShowInfoAsync(
                    control,
                    $"PDF abierto: {path}");
                break;
            default:
                break;
        }
    }

    private static async Task<PrintChoice> ShowOptionsAsync(Window owner, string title)
    {
        var result = PrintChoice.Cancel;
        var printBtn = new Button
        {
            Content = "Imprimir en impresora…",
            Classes = { "primary" },
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var openBtn = new Button
        {
            Content = "Abrir PDF",
            Classes = { "secondary" },
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var cancelBtn = new Button
        {
            Content = "Cancelar",
            Classes = { "secondary" },
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 12,
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Elija cómo imprimir el documento. «Imprimir en impresora…» abre el diálogo de impresión de Windows (o el visor PDF si la cola no responde).",
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrandTheme.Brush(BrandTheme.Text),
        });
        panel.Children.Add(printBtn);
        panel.Children.Add(openBtn);
        panel.Children.Add(cancelBtn);

        var dialog = new Window
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Imprimir" : title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = BrandTheme.Brush(BrandTheme.Background),
            Foreground = BrandTheme.Brush(BrandTheme.Text),
            Content = panel,
        };

        printBtn.Click += (_, _) =>
        {
            result = PrintChoice.PrintToPrinter;
            dialog.Close();
        };
        openBtn.Click += (_, _) =>
        {
            result = PrintChoice.OpenPdf;
            dialog.Close();
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
        return result;
    }

    private static async Task PrintOrFallbackAsync(Control control, string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var printed = TryWindowsPrint(path);
                if (printed)
                {
                    await UiHelpers.ShowInfoAsync(
                        control,
                        "Documento enviado a la impresora de Windows (o abierto en el visor para imprimir).");
                    return;
                }
            }

            OpenWithDefaultViewer(path);
            await UiHelpers.ShowInfoAsync(
                control,
                "No se pudo encolar en la impresora. Se abrió el PDF en el visor predeterminado: use Archivo → Imprimir.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            try
            {
                OpenWithDefaultViewer(path);
                await UiHelpers.ShowInfoAsync(
                    control,
                    $"Impresión fallida ({exception.Message}). PDF abierto: {path}");
            }
            catch (Exception openEx) when (openEx is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
            {
                await UiHelpers.ShowErrorAsync(control, openEx);
            }
        }
    }

    private static bool TryWindowsPrint(string path)
    {
        try
        {
            _ = Process.Start(
                new ProcessStartInfo
                {
                    FileName = path,
                    Verb = "print",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            // Shell "print" often returns null even when the job was accepted.
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Verb "print" may be missing for some PDF associations.
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,ShellExec_RunDLL \"{path}\",print",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            return process is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void OpenWithDefaultViewer(string path)
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
    }
}
