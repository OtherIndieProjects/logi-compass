using System.Globalization;
using System.Text;
using AAALogistics.Domain.Billing;

namespace AAALogistics.Infrastructure.Documents;

/// <summary>
/// Layout comercial compartido (factura, presupuesto, SO, PO, albarán).
/// Grid con bordes de celda reales (operadores PDF re/m/l/S), tipografía jerárquica.
/// </summary>
public sealed class CommercialDocumentModel
{
    public string DocumentTitle { get; init; } = "DOCUMENTO";

    public string DocumentNumber { get; init; } = string.Empty;

    /// <summary>Bloque emisor: primera línea = razón social; resto = CIF/dirección/contacto.</summary>
    public string IssuerBlock { get; init; } = string.Empty;

    public IReadOnlyList<string> MetaLines { get; init; } = [];

    public string PartyHeading { get; init; } = "Cliente";

    public IReadOnlyList<string> PartyLines { get; init; } = [];

    public IReadOnlyList<string> PaymentLines { get; init; } = [];

    public IReadOnlyList<CommercialLineRow> Lines { get; init; } = [];

    public IReadOnlyList<string> TotalLines { get; init; } = [];

    public IReadOnlyList<string> FooterLines { get; init; } = [];

    public string? SignerName { get; init; }

    public string? SignerTitle { get; init; }

    public string? FooterImageNote { get; init; }

    /// <summary>Payload Verifactu / SIF para dibujar QR escaneable en el pie.</summary>
    public string? QrPayload { get; init; }

    /// <summary>Nombre comercial del producto (marca UI), distinto del emisor fiscal.</summary>
    public string? BrandDisplayName { get; init; }
}

public sealed record CommercialLineRow(
    string Concept,
    string Quantity,
    string UnitPrice,
    string TaxPercent,
    string Amount);

public static class CommercialPdf
{
    private const double PageLeft = 40;
    private const double PageRight = 572;
    private const double PageWidth = PageRight - PageLeft;

    // Column widths for line grid (sum = PageWidth).
    private static readonly double[] LineCols = [248, 52, 78, 52, 102];

    public static string PackIssuerBlock(IssuerFiscalProfile? issuer) =>
        string.Join('\n', IssuerHeaderLines(issuer));

    public static IReadOnlyList<string> IssuerHeaderLines(IssuerFiscalProfile? issuer)
    {
        if (issuer is null)
        {
            return ["Empresa no configurada", "Configure Datos de mi empresa en Facturas"];
        }

        var lines = new List<string>
        {
            issuer.LegalName,
            $"CIF/NIF {issuer.TaxId}",
            $"{issuer.AddressLine}, {issuer.PostalCode} {issuer.City} ({issuer.CountryCode})",
        };
        var contact = string.Join(
            " · ",
            new[] { issuer.Phone, issuer.Email, issuer.Website }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(contact))
        {
            lines.Add(contact);
        }

        return lines;
    }

    public static IReadOnlyList<string> PaymentBlock(IssuerFiscalProfile? issuer)
    {
        if (issuer is null)
        {
            return [];
        }

        var payment = new List<string>();
        if (!string.IsNullOrWhiteSpace(issuer.Iban))
        {
            payment.Add($"IBAN {issuer.Iban}");
        }

        if (!string.IsNullOrWhiteSpace(issuer.BankName))
        {
            payment.Add($"Banco {issuer.BankName}");
        }

        if (!string.IsNullOrWhiteSpace(issuer.PaymentMethod))
        {
            payment.Add($"Forma de pago: {issuer.PaymentMethod}");
        }

        if (issuer.PaymentDueDays > 0)
        {
            payment.Add($"Plazo: {issuer.PaymentDueDays} días");
        }

        return payment;
    }

    public static CommercialDocumentModel Create(
        IssuerFiscalProfile? issuer,
        string documentTitle,
        string documentNumber,
        IEnumerable<string> metaLines,
        string partyHeading,
        IEnumerable<string> partyLines,
        IEnumerable<CommercialLineRow> lines,
        IEnumerable<string> totalLines,
        IEnumerable<string> footerLines,
        string? footerImageNote = null,
        bool includePayment = true,
        string? qrPayload = null,
        string? brandDisplayName = null) =>
        new()
        {
            DocumentTitle = documentTitle,
            DocumentNumber = documentNumber,
            IssuerBlock = PackIssuerBlock(issuer),
            MetaLines = metaLines.ToArray(),
            PartyHeading = partyHeading,
            PartyLines = partyLines.ToArray(),
            PaymentLines = includePayment ? PaymentBlock(issuer) : [],
            Lines = lines.ToArray(),
            TotalLines = totalLines.ToArray(),
            FooterLines = footerLines.ToArray(),
            SignerName = string.IsNullOrWhiteSpace(issuer?.SignerName)
                ? issuer?.LegalName
                : issuer.SignerName,
            SignerTitle = string.IsNullOrWhiteSpace(issuer?.SignerTitle)
                ? "Administrador"
                : issuer.SignerTitle,
            FooterImageNote = footerImageNote,
            QrPayload = qrPayload,
            BrandDisplayName = string.IsNullOrWhiteSpace(brandDisplayName)
                ? null
                : brandDisplayName.Trim(),
        };

    public static byte[] Render(CommercialDocumentModel model, byte[]? pngBytes = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        var g = new PdfCanvas();
        var yTop = 780.0;

        // Top brand bar (+ optional product brand name).
        g.FillRect(PageLeft, yTop - 4, PageWidth, 6, gray: 0.22);
        yTop -= 14;
        if (!string.IsNullOrWhiteSpace(model.BrandDisplayName))
        {
            g.TextLeft(PageLeft, yTop, 8, bold: true, Truncate(model.BrandDisplayName!, 48));
            yTop -= 14;
        }
        else
        {
            yTop -= 4;
        }

        var issuerParts = model.IssuerBlock.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var issuerTitle = issuerParts.Length > 0 ? issuerParts[0] : "Empresa";
        var issuerRest = issuerParts.Skip(1).Take(4).ToArray();
        var meta = model.MetaLines.Take(5).ToArray();

        const double gap = 10;
        var half = (PageWidth - gap) / 2;
        var leftBoxH = Math.Max(54, 16 + issuerRest.Length * 11 + 10);
        var rightBoxH = Math.Max(54, 16 + 14 + meta.Length * 11 + 10);
        var headerH = Math.Max(leftBoxH, rightBoxH);
        var headerBottom = yTop - headerH;

        // Emisor box (left).
        g.StrokeRect(PageLeft, headerBottom, half, headerH);
        g.FillRect(PageLeft, yTop - 16, half, 16, gray: 0.90);
        g.StrokeRect(PageLeft, yTop - 16, half, 16);
        g.TextLeft(PageLeft + 6, yTop - 12, 9, bold: true, Truncate(issuerTitle, 42));
        var iy = yTop - 28;
        foreach (var line in issuerRest)
        {
            g.TextLeft(PageLeft + 6, iy, 8, bold: false, Truncate(line, 48));
            iy -= 11;
        }

        // Documento box (right).
        var rx = PageLeft + half + gap;
        g.StrokeRect(rx, headerBottom, half, headerH);
        g.FillRect(rx, yTop - 16, half, 16, gray: 0.22);
        g.TextLeft(rx + 6, yTop - 12, 10, bold: true, Truncate(model.DocumentTitle, 28), grayText: false, white: true);
        g.TextLeft(rx + 6, yTop - 30, 11, bold: true, Truncate(model.DocumentNumber, 34));
        var my = yTop - 44;
        foreach (var line in meta)
        {
            g.TextLeft(rx + 6, my, 8, bold: false, Truncate(line, 40));
            my -= 11;
        }

        var y = headerBottom - 14;

        // Party + payment boxes.
        var party = model.PartyLines.Take(5).ToArray();
        var pay = model.PaymentLines.Take(5).ToArray();
        var partyH = Math.Max(40, 18 + party.Length * 11 + 8);
        var payH = Math.Max(40, 18 + Math.Max(1, pay.Length) * 11 + 8);
        var blockH = Math.Max(partyH, payH);
        var blockBottom = y - blockH;

        g.StrokeRect(PageLeft, blockBottom, half, blockH);
        g.FillRect(PageLeft, y - 14, half, 14, gray: 0.90);
        g.StrokeRect(PageLeft, y - 14, half, 14);
        g.TextLeft(PageLeft + 6, y - 11, 9, bold: true, Truncate(model.PartyHeading, 40));
        var py = y - 26;
        foreach (var line in party)
        {
            g.TextLeft(PageLeft + 6, py, 8, bold: false, Truncate(line, 48));
            py -= 11;
        }

        g.StrokeRect(rx, blockBottom, half, blockH);
        g.FillRect(rx, y - 14, half, 14, gray: 0.90);
        g.StrokeRect(rx, y - 14, half, 14);
        g.TextLeft(rx + 6, y - 11, 9, bold: true, pay.Length > 0 ? "Datos de pago" : "Referencia");
        var payY = y - 26;
        if (pay.Length == 0)
        {
            g.TextLeft(rx + 6, payY, 8, bold: false, "—");
        }
        else
        {
            foreach (var line in pay)
            {
                g.TextLeft(rx + 6, payY, 8, bold: false, Truncate(line, 42));
                payY -= 11;
            }
        }

        y = blockBottom - 16;

        // Line items grid with real cell borders.
        string[] headers = ["Concepto", "Cant.", "Precio", "% IVA", "Importe"];
        bool[] rightAlign = [false, true, true, true, true];
        const double headerRowH = 16;
        const double bodyRowH = 15;
        var rows = model.Lines.Take(26).ToArray();
        var tableH = headerRowH + rows.Length * bodyRowH;
        var tableBottom = y - tableH;

        // Header fill + outer border.
        g.FillRect(PageLeft, y - headerRowH, PageWidth, headerRowH, gray: 0.85);
        DrawTableGrid(g, PageLeft, tableBottom, PageWidth, tableH, LineCols, bodyRowH, rows.Length);

        // Header text.
        DrawRowCells(
            g,
            PageLeft,
            y - headerRowH,
            headerRowH,
            LineCols,
            headers,
            rightAlign,
            bold: true,
            size: 8);

        // Body rows.
        var rowTop = y - headerRowH;
        for (var i = 0; i < rows.Length; i++)
        {
            rowTop -= bodyRowH;
            var row = rows[i];
            DrawRowCells(
                g,
                PageLeft,
                rowTop,
                bodyRowH,
                LineCols,
                [
                    Truncate(row.Concept, 42),
                    Truncate(row.Quantity, 10),
                    Truncate(row.UnitPrice, 12),
                    Truncate(row.TaxPercent, 8),
                    Truncate(row.Amount, 14),
                ],
                rightAlign,
                bold: false,
                size: 8);
        }

        if (model.Lines.Count > rows.Length)
        {
            y = tableBottom - 12;
            g.TextLeft(PageLeft, y, 8, bold: false, "… (más líneas omitidas en esta página)");
            y -= 10;
        }
        else
        {
            y = tableBottom - 12;
        }

        // Totals box (right-aligned block).
        var totals = model.TotalLines.Take(6).ToArray();
        if (totals.Length > 0)
        {
            const double totalsW = 220;
            var totalsH = 10 + totals.Length * 13;
            var totalsLeft = PageRight - totalsW;
            var totalsBottom = y - totalsH;
            g.StrokeRect(totalsLeft, totalsBottom, totalsW, totalsH);
            g.FillRect(totalsLeft, y - 12, totalsW, 12, gray: 0.90);
            g.StrokeRect(totalsLeft, y - 12, totalsW, 12);
            g.TextLeft(totalsLeft + 6, y - 9, 8, bold: true, "Totales");
            var ty = y - 24;
            foreach (var total in totals)
            {
                var bold = total.Contains("TOTAL", StringComparison.OrdinalIgnoreCase);
                g.TextRight(PageRight - 6, ty, 9, bold, Truncate(total, 36));
                ty -= 13;
            }

            y = totalsBottom - 14;
        }

        // Footer / legal.
        foreach (var footer in model.FooterLines.Take(6))
        {
            g.TextLeft(PageLeft, y, 7, bold: false, Truncate(footer, 100));
            y -= 10;
        }

        if (!string.IsNullOrWhiteSpace(model.SignerName) || !string.IsNullOrWhiteSpace(model.SignerTitle))
        {
            y -= 4;
            const double signW = 220;
            var signH = 36;
            var signBottom = Math.Max(48, y - signH);
            g.StrokeRect(PageLeft, signBottom, signW, signH);
            g.TextLeft(PageLeft + 6, signBottom + signH - 12, 8, bold: true, "Firma");
            g.TextLeft(
                PageLeft + 6,
                signBottom + 10,
                8,
                bold: false,
                Truncate($"{model.SignerName} · {model.SignerTitle}", 40));
            y = signBottom - 8;
        }

        if (!string.IsNullOrWhiteSpace(model.FooterImageNote))
        {
            g.TextLeft(PageLeft, Math.Max(40, y), 7, bold: false, Truncate(model.FooterImageNote!, 90));
        }

        if (!string.IsNullOrWhiteSpace(model.QrPayload))
        {
            DrawQrModules(g, model.QrPayload!, PageRight - 78, 36, moduleSize: 1.6);
            g.TextLeft(PageRight - 78, 30, 6, bold: false, "QR Verifactu");
        }

        // Bottom bar.
        g.FillRect(PageLeft, 24, PageWidth, 3, gray: 0.22);

        return MinimalPdf.FromContentStream(g.ToString(), pngBytes, includeBoldFont: true);
    }

    private static void DrawQrModules(
        PdfCanvas g,
        string payload,
        double left,
        double bottom,
        double moduleSize)
    {
        try
        {
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(
                payload,
                QRCoder.QRCodeGenerator.ECCLevel.M);
            var matrix = data.ModuleMatrix;
            var size = matrix.Count;
            for (var row = 0; row < size; row++)
            {
                for (var col = 0; col < size; col++)
                {
                    if (!matrix[row][col])
                    {
                        continue;
                    }

                    var x = left + col * moduleSize;
                    var y = bottom + (size - 1 - row) * moduleSize;
                    g.FillRect(x, y, moduleSize, moduleSize, gray: 0.05);
                }
            }
        }
        catch (Exception)
        {
            g.TextLeft(left, bottom + 20, 7, bold: false, "QR no disponible");
        }
    }

    private static void DrawTableGrid(
        PdfCanvas g,
        double left,
        double bottom,
        double width,
        double height,
        double[] cols,
        double bodyH,
        int bodyRows)
    {
        g.StrokeRect(left, bottom, width, height);
        // Header separator.
        var headerY = bottom + bodyRows * bodyH;
        g.HLine(left, left + width, headerY);
        // Horizontal body lines.
        for (var i = 1; i < bodyRows; i++)
        {
            var hy = bottom + i * bodyH;
            g.HLine(left, left + width, hy);
        }

        // Vertical column lines.
        var x = left;
        for (var c = 0; c < cols.Length - 1; c++)
        {
            x += cols[c];
            g.VLine(x, bottom, bottom + height);
        }
    }

    private static void DrawRowCells(
        PdfCanvas g,
        double left,
        double rowBottom,
        double rowH,
        double[] cols,
        string[] values,
        bool[] rightAlign,
        bool bold,
        double size)
    {
        var x = left;
        var textY = rowBottom + (rowH - size) / 2 + 1;
        for (var i = 0; i < cols.Length && i < values.Length; i++)
        {
            const double pad = 4;
            if (rightAlign[i])
            {
                g.TextRight(x + cols[i] - pad, textY, size, bold, values[i]);
            }
            else
            {
                g.TextLeft(x + pad, textY, size, bold, values[i]);
            }

            x += cols[i];
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..Math.Max(0, max - 1)] + "…";
}

/// <summary>Informes y auditoría: título + tabla con celdas reales.</summary>
public static class ReportPdf
{
    private const double PageLeft = 40;
    private const double PageRight = 572;
    private const double PageWidth = PageRight - PageLeft;

    public static byte[] Render(
        string title,
        IEnumerable<string> metaLines,
        IReadOnlyList<string> columnHeaders,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<bool> rightAlign,
        IEnumerable<IReadOnlyList<string>> rows,
        IEnumerable<string>? footerLines = null,
        byte[]? pngBytes = null)
    {
        if (columnHeaders.Count == 0)
        {
            throw new ArgumentException("Se requieren columnas.", nameof(columnHeaders));
        }

        if (columnHeaders.Count != columnWidths.Count || columnHeaders.Count != rightAlign.Count)
        {
            throw new ArgumentException("Cabeceras, anchos y alineación deben coincidir.");
        }

        var widthSum = columnWidths.Sum();
        if (Math.Abs(widthSum - PageWidth) > 0.5)
        {
            throw new ArgumentException(
                $"La suma de anchos debe ser {PageWidth} (es {widthSum}).",
                nameof(columnWidths));
        }

        var g = new PdfCanvas();
        var y = 780.0;
        g.FillRect(PageLeft, y - 4, PageWidth, 6, gray: 0.22);
        y -= 22;
        g.TextLeft(PageLeft, y, 14, bold: true, Truncate(title, 60));
        y -= 14;
        foreach (var meta in metaLines.Take(4))
        {
            g.TextLeft(PageLeft, y, 8, bold: false, Truncate(meta, 100));
            y -= 11;
        }

        y -= 6;

        const double headerH = 16;
        const double bodyH = 14;
        var data = rows.Take(36).ToArray();
        var cols = columnWidths.ToArray();
        var align = rightAlign.ToArray();
        var tableH = headerH + data.Length * bodyH;
        var tableBottom = y - tableH;

        g.FillRect(PageLeft, y - headerH, PageWidth, headerH, gray: 0.85);
        DrawGrid(g, PageLeft, tableBottom, PageWidth, tableH, cols, bodyH, data.Length);
        DrawCells(g, PageLeft, y - headerH, headerH, cols, columnHeaders.ToArray(), align, bold: true, size: 8);

        var rowTop = y - headerH;
        foreach (var row in data)
        {
            rowTop -= bodyH;
            var cells = new string[cols.Length];
            for (var i = 0; i < cols.Length; i++)
            {
                cells[i] = i < row.Count ? Truncate(row[i], MaxChars(cols[i])) : string.Empty;
            }

            DrawCells(g, PageLeft, rowTop, bodyH, cols, cells, align, bold: false, size: 7);
        }

        y = tableBottom - 14;
        foreach (var footer in (footerLines ?? []).Take(6))
        {
            g.TextLeft(PageLeft, y, 8, bold: true, Truncate(footer, 100));
            y -= 11;
        }

        g.FillRect(PageLeft, 24, PageWidth, 3, gray: 0.22);
        return MinimalPdf.FromContentStream(g.ToString(), pngBytes, includeBoldFont: true);
    }

    private static int MaxChars(double colWidth) =>
        Math.Max(4, (int)(colWidth / 4.2));

    private static void DrawGrid(
        PdfCanvas g,
        double left,
        double bottom,
        double width,
        double height,
        double[] cols,
        double bodyH,
        int bodyRows)
    {
        g.StrokeRect(left, bottom, width, height);
        g.HLine(left, left + width, bottom + bodyRows * bodyH);
        for (var i = 1; i < bodyRows; i++)
        {
            g.HLine(left, left + width, bottom + i * bodyH);
        }

        var x = left;
        for (var c = 0; c < cols.Length - 1; c++)
        {
            x += cols[c];
            g.VLine(x, bottom, bottom + height);
        }
    }

    private static void DrawCells(
        PdfCanvas g,
        double left,
        double rowBottom,
        double rowH,
        double[] cols,
        string[] values,
        bool[] rightAlign,
        bool bold,
        double size)
    {
        var x = left;
        var textY = rowBottom + (rowH - size) / 2 + 1;
        for (var i = 0; i < cols.Length && i < values.Length; i++)
        {
            const double pad = 3;
            if (rightAlign[i])
            {
                g.TextRight(x + cols[i] - pad, textY, size, bold, values[i]);
            }
            else
            {
                g.TextLeft(x + pad, textY, size, bold, values[i]);
            }

            x += cols[i];
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..Math.Max(0, max - 1)] + "…";
}

/// <summary>Canvas PDF de una página: texto + líneas + rectángulos (bordes de celda reales).</summary>
internal sealed class PdfCanvas
{
    private readonly StringBuilder _content = new();

    public override string ToString() => _content.ToString();

    public void FillRect(double x, double y, double w, double h, double gray)
    {
        _content.AppendLine(CultureInfo.InvariantCulture, $"{gray:0.###} g");
        _content.AppendLine(CultureInfo.InvariantCulture, $"{x:0.##} {y:0.##} {w:0.##} {h:0.##} re f");
        _content.AppendLine("0 g");
    }

    public void StrokeRect(double x, double y, double w, double h)
    {
        _content.AppendLine("0.35 G");
        _content.AppendLine("0.6 w");
        _content.AppendLine(CultureInfo.InvariantCulture, $"{x:0.##} {y:0.##} {w:0.##} {h:0.##} re S");
        _content.AppendLine("0 G");
        _content.AppendLine("1 w");
    }

    public void HLine(double x1, double x2, double y)
    {
        _content.AppendLine("0.45 G");
        _content.AppendLine("0.5 w");
        _content.AppendLine(CultureInfo.InvariantCulture, $"{x1:0.##} {y:0.##} m {x2:0.##} {y:0.##} l S");
        _content.AppendLine("0 G");
        _content.AppendLine("1 w");
    }

    public void VLine(double x, double y1, double y2)
    {
        _content.AppendLine("0.45 G");
        _content.AppendLine("0.5 w");
        _content.AppendLine(CultureInfo.InvariantCulture, $"{x:0.##} {y1:0.##} m {x:0.##} {y2:0.##} l S");
        _content.AppendLine("0 G");
        _content.AppendLine("1 w");
    }

    public void TextLeft(
        double x,
        double y,
        double size,
        bool bold,
        string text,
        bool grayText = false,
        bool white = false)
    {
        if (white)
        {
            _content.AppendLine("1 g");
        }
        else if (grayText)
        {
            _content.AppendLine("0.35 g");
        }

        WriteText(x, y, size, bold, text);
        if (white || grayText)
        {
            _content.AppendLine("0 g");
        }
    }

    public void TextRight(double xRight, double y, double size, bool bold, string text)
    {
        var width = EstimateWidth(text, size);
        WriteText(xRight - width, y, size, bold, text);
    }

    private void WriteText(double x, double y, double size, bool bold, string text)
    {
        var font = bold ? "/F2" : "/F1";
        _content.AppendLine("BT");
        _content.AppendLine(CultureInfo.InvariantCulture, $"{font} {size:0.##} Tf");
        _content.AppendLine(CultureInfo.InvariantCulture, $"{x:0.##} {y:0.##} Td");
        _content.AppendLine($"({Escape(text)}) Tj");
        _content.AppendLine("ET");
    }

    /// <summary>Aprox. Helvetica: ~0.5em por carácter (suficiente para alinear importes).</summary>
    private static double EstimateWidth(string text, double size) =>
        text.Length * size * 0.50;

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
