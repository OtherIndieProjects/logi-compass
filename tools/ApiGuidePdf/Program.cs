using System.Globalization;
using System.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("Uso: ApiGuidePdf <salida.pdf> [copia.pdf ...]");
    return 2;
}

var bytes = ApiGuideDocument.Render();
foreach (var path in args)
{
    var full = Path.GetFullPath(path);
    var dir = Path.GetDirectoryName(full);
    if (!string.IsNullOrEmpty(dir))
    {
        Directory.CreateDirectory(dir);
    }

    await File.WriteAllBytesAsync(full, bytes);
    Console.WriteLine($"PDF escrito: {full} ({bytes.Length} bytes)");
}

return 0;

internal static class ApiGuideDocument
{
    public static byte[] Render()
    {
        var pages = new[]
        {
            Page(
                "LOGI COMPASS · GUIA API v1",
                [
                    "AAA Logistics Studio — API interna Minimal",
                    "Fecha: 2026-11-22 (Europe/Madrid)",
                    "",
                    "Base URL (This PC): http://127.0.0.1:5088",
                    "OpenAPI JSON:     /openapi/v1.json",
                    "UI Scalar:        /scalar",
                    "Prefijo API:      /api/v1/internal",
                    "Portal B2B:       http://127.0.0.1:5089/api/v1/portal",
                    "",
                    "Auth: POST /auth/login -> Bearer JWT",
                    "Refresh: POST /auth/refresh · Logout: POST /auth/logout",
                    "Yo: GET /me (permisos RBAC)",
                    "",
                    "Errores: application/problem+json + code AAA-... + traceId",
                    "401 auth · 403 permiso · 404 · 409 · 429 rate-limit",
                    "",
                    "Documentacion Markdown (GitHub-ready):",
                    "  docs/es/api-guia.md",
                    "  docs/es/crear-app-logi-compass.md",
                ]),
            Page(
                "MAPA DE DOMINIOS",
                [
                    "Maestros: /units /materials /warehouses /suppliers /customers",
                    "          /locations · /document-number-ranges",
                    "",
                    "Compras MM: /purchase-orders (+ pdf, cancel, close)",
                    "             /goods-receipts/post · GET /goods-receipts · pdf",
                    "",
                    "Ventas SD:  /quotes (send/accept/reject/pdf)",
                    "             /sales-orders (confirm/cancel/close/reject/pdf)",
                    "             prep: .../prep /picks /mark-prepared",
                    "             /deliveries/post · GET /deliveries · pdf · packing",
                    "             /handling-units · /sales-returns/post",
                    "",
                    "Inventario: /stock · movements · transfers · putaway",
                    "             adjustments · counts · /rf/scan",
                    "             hub SignalR /hubs/operations",
                    "",
                    "Facturas:   /issuer-fiscal (identidad empresa)",
                    "             /invoices · pdf · rectify · QR Verifactu LocalChain",
                    "",
                    "Oficina:    /carriers · /cmr · pdf",
                    "Informes:   /reports/orders-by-status|otif|stock (+ pdf)",
                    "Auditoria:  /audit/entries · export · verify · checkpoints",
                ]),
            Page(
                "FLUJOS RAPIDOS",
                [
                    "1) Login -> GET /stock",
                    "2) Quote -> accept -> SO -> confirm -> prep -> deliveries/post",
                    "3) PO -> goods-receipts/post -> GET .../pdf",
                    "4) issuer-fiscal -> invoices -> pdf (CIF + razon social)",
                    "",
                    "PGI solo si entrega en Preparado / Parcialmente entregado.",
                    "Postings criticos: idempotencia + permisos RBAC.",
                    "",
                    "APP PERSONALIZADA (estilo GestionAlmacenKngloo)",
                    "- Misma API; UI propia; no duplicar stock en otro servidor.",
                    "- Pantallas: login, materiales, stock, prep, albaran, GR.",
                    "- Listar por numero/codigo; no pedir GUID al operario.",
                    "- Probar primero en Scalar (/scalar) con Bearer.",
                    "",
                    "COMO ABRIR SCALAR / OPENAPI",
                    "1. Iniciar Servidor (LogisticSolution).",
                    "2. Navegador -> http://127.0.0.1:5088/scalar",
                    "3. Authorize con accessToken del login.",
                    "4. JSON crudo -> /openapi/v1.json",
                    "",
                    "Instalacion local: Desktop\\LogisticSolution · Scalar en :5088/scalar.",
                ]),
        };

        return MultiPagePdf.Build(pages);
    }

    private static string Page(string title, IReadOnlyList<string> lines)
    {
        var g = new StringBuilder();
        g.Append("BT /F2 16 Tf 40 760 Td (")
            .Append(Escape(title))
            .AppendLine(") Tj");
        g.AppendLine("0 -22 Td /F1 10 Tf");
        foreach (var line in lines)
        {
            g.Append('(')
                .Append(Escape(line))
                .AppendLine(") Tj 0 -13 Td");
        }

        g.AppendLine("ET");
        g.AppendLine("0.22 g 40 24 532 3 re f");
        return g.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}

internal static class MultiPagePdf
{
    public static byte[] Build(IReadOnlyList<string> pageContents)
    {
        var contentIds = new List<int>();
        var pageIds = new List<int>();
        var next = 3;
        foreach (var _ in pageContents)
        {
            contentIds.Add(next++);
            pageIds.Add(next++);
        }

        var fontRegular = next++;
        var fontBold = next;

        var ms = new MemoryStream();
        var offsets = new Dictionary<int, int>();

        void Write(byte[] data) => ms.Write(data);

        void BeginObj(int id)
        {
            offsets[id] = (int)ms.Length;
        }

        Write(Encoding.ASCII.GetBytes("%PDF-1.4\n"));

        BeginObj(1);
        Write(Encoding.ASCII.GetBytes("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n"));

        var kids = string.Join(" ", pageIds.Select(id => $"{id} 0 R"));
        BeginObj(2);
        Write(Encoding.ASCII.GetBytes(
            $"2 0 obj<< /Type /Pages /Kids [{kids}] /Count {pageIds.Count} >>endobj\n"));

        for (var i = 0; i < pageContents.Count; i++)
        {
            var contentId = contentIds[i];
            var pageId = pageIds[i];
            var stream = Encoding.ASCII.GetBytes(pageContents[i]);
            BeginObj(contentId);
            Write(Encoding.ASCII.GetBytes(
                $"{contentId} 0 obj<< /Length {stream.Length} >>stream\n"));
            Write(stream);
            Write(Encoding.ASCII.GetBytes("\nendstream\nendobj\n"));

            BeginObj(pageId);
            Write(Encoding.ASCII.GetBytes(
                $"{pageId} 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + $"/Contents {contentId} 0 R "
                + $"/Resources << /Font << /F1 {fontRegular} 0 R /F2 {fontBold} 0 R >> >> >>endobj\n"));
        }

        BeginObj(fontRegular);
        Write(Encoding.ASCII.GetBytes(
            $"{fontRegular} 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n"));
        BeginObj(fontBold);
        Write(Encoding.ASCII.GetBytes(
            $"{fontBold} 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>endobj\n"));

        var xref = (int)ms.Length;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {fontBold + 1}\n");
        sb.Append("0000000000 65535 f \n");
        for (var id = 1; id <= fontBold; id++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{offsets[id]:D10} 00000 n \n");
        }

        sb.Append(CultureInfo.InvariantCulture, $"trailer<< /Size {fontBold + 1} /Root 1 0 R >>\n");
        sb.Append("startxref\n");
        sb.Append(xref);
        sb.Append("\n%%EOF\n");
        Write(Encoding.ASCII.GetBytes(sb.ToString()));
        return ms.ToArray();
    }
}
