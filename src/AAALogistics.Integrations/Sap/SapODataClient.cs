using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AAALogistics.Application.Logistics;

namespace AAALogistics.Integrations.Sap;

/// <summary>
/// Opt-in SAP OData façade (ADR 0025 + RF-INT-001). Mock when disabled;
/// HttpClient-backed client when Enabled.
/// </summary>
public sealed class MockSapODataClient : ISapIntegrationClient
{
    public bool IsEnabled => false;

    public string Mode => "Mock";

    public Task<IReadOnlyList<SapMaterialDto>> ListMaterialsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SapMaterialDto> rows =
        [
            new("MAT-1000", "Material demo SAP"),
            new("MAT-2000", "Embalaje demo SAP"),
        ];
        return Task.FromResult(rows);
    }

    public Task PushDeliveryAsync(
        Guid deliveryId,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        _ = deliveryId;
        _ = payloadJson;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class SapODataClient : ISapIntegrationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _materialsPath;
    private readonly string _deliveryPushPath;

    public SapODataClient(
        HttpClient http,
        string materialsPath,
        string deliveryPushPath)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _materialsPath = string.IsNullOrWhiteSpace(materialsPath)
            ? throw new ArgumentException("MaterialsPath vacío.")
            : materialsPath.Trim();
        _deliveryPushPath = string.IsNullOrWhiteSpace(deliveryPushPath)
            ? throw new ArgumentException("DeliveryPushPath vacío.")
            : deliveryPushPath.Trim();
    }

    public bool IsEnabled => true;

    public string Mode => "OData";

    public async Task<IReadOnlyList<SapMaterialDto>> ListMaterialsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(_materialsPath, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-SAP-0001",
                $"SAP materials HTTP {(int)response.StatusCode}: {Truncate(body)}");
        }

        return ParseMaterials(body);
    }

    public async Task PushDeliveryAsync(
        Guid deliveryId,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        _ = deliveryId;
        using var content = new StringContent(
            payloadJson,
            Encoding.UTF8,
            "application/json");
        using var response = await _http.PostAsync(
            _deliveryPushPath,
            content,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-SAP-0002",
                $"SAP delivery push HTTP {(int)response.StatusCode}: {Truncate(body)}");
        }
    }

    public static IReadOnlyList<SapMaterialDto> ParseMaterials(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        JsonElement rows;
        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            rows = value;
        }
        else if (root.TryGetProperty("d", out var d)
            && d.TryGetProperty("results", out var results)
            && results.ValueKind == JsonValueKind.Array)
        {
            rows = results;
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            rows = root;
        }
        else
        {
            return Array.Empty<SapMaterialDto>();
        }

        var list = new List<SapMaterialDto>();
        foreach (var row in rows.EnumerateArray())
        {
            var number = ReadString(row, "Product", "Material", "MaterialNumber", "product", "material");
            var description = ReadString(
                row,
                "ProductDescription",
                "Description",
                "MaterialDescription",
                "description");
            if (string.IsNullOrWhiteSpace(number))
            {
                continue;
            }

            list.Add(new SapMaterialDto(
                number.Trim(),
                string.IsNullOrWhiteSpace(description) ? number.Trim() : description.Trim()));
        }

        return list;
    }

    public static void ConfigureAuth(
        HttpClient client,
        Uri baseAddress,
        string? bearerToken,
        string? userName,
        string? password,
        TimeSpan timeout)
    {
        client.BaseAddress = baseAddress;
        client.Timeout = timeout;
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
            return;
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var raw = $"{userName}:{password ?? string.Empty}";
            var bytes = Encoding.UTF8.GetBytes(raw);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }
    }

    private static string? ReadString(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var prop)
                && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    private static string Truncate(string value) =>
        value.Length <= 400 ? value : value[..400];
}
