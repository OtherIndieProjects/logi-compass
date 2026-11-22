using Avalonia.Controls;
using AAALogistics.Client.Services;

namespace AAALogistics.Client.Views;

internal static class UiHelpers
{
    public static Window? FindWindow(Control control) =>
        TopLevel.GetTopLevel(control) as Window;

    public static async Task ShowErrorAsync(Control control, Exception exception)
    {
        var owner = FindWindow(control);
        if (owner is null)
        {
            return;
        }

        await AppDialog.ShowMessageAsync(owner, BrandTheme.ShortName, ToUserMessage(exception));
    }

    /// <summary>Mensaje humano ES para novatos; mapea códigos AAA frecuentes de stock/fiscal.</summary>
    public static string ToUserMessage(Exception exception)
    {
        if (exception is TaskCanceledException)
        {
            return "El servidor no respondió a tiempo. Inténtelo de nuevo.";
        }

        if (exception is HttpRequestException)
        {
            return "No se pudo contactar con el servidor. Compruebe que el Servidor esté en marcha.";
        }

        if (exception is not ApiException api)
        {
            return exception.Message;
        }

        var mapped = MapApiCode(api.Code);
        if (mapped is not null)
        {
            return mapped;
        }

        // Mensajes de servidor ya en ES: mostrarlos sin el código técnico delante.
        var text = api.Message?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return "Ha ocurrido un error. Revise los datos e inténtelo de nuevo.";
        }

        if (!string.IsNullOrWhiteSpace(api.Code)
            && text.StartsWith(api.Code, StringComparison.OrdinalIgnoreCase))
        {
            text = text[api.Code.Length..].TrimStart(' ', ':', '-');
        }

        return text;
    }

    private static string? MapApiCode(string? code) => code switch
    {
        "AAA-AUTH-FORBIDDEN" or "AAA-AUTH-0003" =>
            "No tiene permiso para esta acción. Pida a un administrador que le asigne el rango adecuado en Cuentas.",
        "AAA-DELIVERY-0001" or "AAA-SALES-0005" =>
            "Ese pedido de venta no existe o ya no está disponible.",
        "AAA-DELIVERY-0002" =>
            "El pedido debe estar Preparado (o parcialmente entregado) antes de contabilizar la entrega. Termine la preparación primero.",
        "AAA-DELIVERY-0005" =>
            "No hay stock disponible suficiente para entregar. Revise Inventario o reduzca la cantidad.",
        "AAA-DELIVERY-0006" =>
            "Falta el lote o la caducidad en un material que los exige. Indique el lote correcto.",
        "AAA-DELIVERY-0007" or "AAA-DELIVERY-0008" =>
            "Falta confirmar la recogida (picking) de alguna línea. Vaya a Preparación y confirme las líneas antes de entregar.",
        "AAA-DELIVERY-0010" =>
            "El almacén de entrega no coincide con el de preparación. Use el mismo almacén o pida ayuda a un administrador.",
        "AAA-INVENTORY-0001" =>
            "Ya existe un almacén con ese código. Elija otro código.",
        "AAA-INVENTORY-0002" =>
            "El almacén indicado no existe. Créelo en Maestros o Ubicaciones.",
        "AAA-INVENTORY-0003" =>
            "La cantidad debe ser mayor que cero.",
        "AAA-INVENTORY-0005" =>
            "Uno o más almacenes no existen. Revise los códigos elegidos.",
        "AAA-INVENTORY-0006" =>
            "Indique al menos una línea para contabilizar.",
        "AAA-INVENTORY-0007" or "AAA-STOCK-0011" =>
            "Elija una ubicación (hueco) antes de continuar.",
        "AAA-STOCK-0010" =>
            "No se pudo cambiar el estado del stock. Revise material, almacén y ubicación.",
        "AAA-SALES-0002" or "AAA-SALES-0003" =>
            "El cliente no existe o no está activo. Revíselo en Maestros → Clientes.",
        "AAA-SALES-0004" =>
            "El almacén indicado no existe.",
        "AAA-SALES-0010" or "AAA-SALES-0011" or "AAA-SALES-0012" =>
            "No se puede cambiar el estado de este pedido (quizá ya está cerrado, cancelado o entregado).",
        "AAA-SALES-0013" =>
            "Indique el motivo de cancelación.",
        "AAA-INV-0001" =>
            "Cliente no encontrado. Selecciónelo de nuevo en el desplegable.",
        "AAA-INV-0002" =>
            "La factura necesita al menos una línea con material e importe.",
        "AAA-INV-0003" =>
            "No se encontró esa factura. Actualice la lista e inténtelo de nuevo.",
        "AAA-INV-0004" =>
            "Esa factura ya tiene una rectificativa. No se puede rectificar otra vez.",
        "AAA-INV-0005" =>
            "Indique el motivo de la rectificativa (abono).",
        "AAA-INV-SERIES" =>
            "Revise la serie de factura (no puede estar vacía).",
        "AAA-FAC-0012" =>
            "Indique la razón social / nombre de empresa antes de guardar.",
        "AAA-FAC-0013" =>
            "Indique el CIF/NIF del emisor antes de guardar.",
        _ => null,
    };

    public static async Task ShowInfoAsync(Control control, string message)
    {
        var owner = FindWindow(control);
        if (owner is null)
        {
            return;
        }

        await AppDialog.ShowMessageAsync(owner, BrandTheme.ShortName, message);
    }

    public static bool TryParseGuid(string? text, out Guid value) =>
        Guid.TryParse(text?.Trim(), out value);

    /// <summary>
    /// Accepts "2,5", "2.5", "1.234,5" and "1,234.5": the right-most separator is the decimal one,
    /// so a Spanish "2,5" is never read as 25.
    /// </summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        var trimmed = text?.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var normalized = trimmed.LastIndexOf(',') > trimmed.LastIndexOf('.')
            ? trimmed.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
            : trimmed.Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(
            normalized,
            System.Globalization.NumberStyles.AllowLeadingSign
                | System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }

    public static async Task SaveBytesAsync(
        Control control,
        string suggestedName,
        byte[] bytes)
    {
        var top = TopLevel.GetTopLevel(control);
        if (top?.StorageProvider is null)
        {
            await ShowInfoAsync(control, "No hay diálogo de guardado disponible.");
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                SuggestedFileName = suggestedName,
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Archivo")
                    {
                        Patterns = ["*.*"],
                    },
                ],
            });
        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(bytes);
    }
}
