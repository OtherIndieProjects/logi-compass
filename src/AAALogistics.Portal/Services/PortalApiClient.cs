using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AAALogistics.Contracts;

namespace AAALogistics.Portal.Services;

/// <summary>
/// Sesión del circuito Blazor en el servidor del portal. El JWT no se escribe
/// en la respuesta, en cookies ni en el HTML. El BFF con cookie HttpOnly,
/// Secure y SameSite sigue pendiente del spike S6.
/// </summary>
public sealed class PortalSession
{
    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; private set; }

    public string? UserName { get; private set; }

    public Guid? CustomerId { get; private set; }

    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(AccessToken);

    public void Remember(
        string userName,
        string accessToken,
        Guid? customerId = null,
        string? refreshToken = null)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException(
                "El token de acceso es obligatorio.",
                nameof(accessToken));
        }

        UserName = userName;
        AccessToken = accessToken;
        CustomerId = customerId;
        RefreshToken = refreshToken;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        UserName = null;
        CustomerId = null;
    }
}

public sealed class PortalApiClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory =
        httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    public async Task<IReadOnlyList<PortalCatalogItemResponse>> GetCatalogAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync(
            ApiRoutes.PortalCatalog,
            cancellationToken);
        return await ReadAsync<List<PortalCatalogItemResponse>>(
            response,
            cancellationToken) ?? [];
    }

    public async Task<PowChallengeResponse?> GetPowChallengeAsync(
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            ApiRoutes.PortalRegistrationPowChallenge,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PowChallengeResponse>(
            JsonOptions,
            cancellationToken);
    }

    public async Task<RegistrationSubmittedResponse> RegisterAsync(
        SubmitRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            ApiRoutes.PortalRegistration,
            request,
            JsonOptions,
            cancellationToken);
        return await ReadAsync<RegistrationSubmittedResponse>(
            response,
            cancellationToken);
    }

    public async Task<LoginResponse> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            ApiRoutes.PortalLogin,
            new LoginRequest(userName, password),
            JsonOptions,
            cancellationToken);
        return await ReadAsync<LoginResponse>(response, cancellationToken);
    }

    public async Task PingAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync(
            ApiRoutes.PortalPing,
            cancellationToken);
        await ReadAsync<JsonElement>(response, cancellationToken);
    }

    public async Task LogoutAsync(
        string accessToken,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.PostAsJsonAsync(
            ApiRoutes.PortalLogout,
            new LogoutRequest(refreshToken),
            JsonOptions,
            cancellationToken);
        _ = response.StatusCode;
    }

    public async Task<LoginResponse> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            ApiRoutes.PortalRefresh,
            new RefreshRequest(refreshToken),
            JsonOptions,
            cancellationToken);
        return await ReadAsync<LoginResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<PortalSalesOrderRow>> GetSalesOrdersAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync(
            ApiRoutes.PortalSalesOrders,
            cancellationToken);
        var rows = await ReadAsync<List<PortalSalesOrderRow>>(
            response,
            cancellationToken);
        return rows ?? [];
    }

    public async Task<IReadOnlyList<PortalWarehouseResponse>> GetWarehousesAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync(
            ApiRoutes.PortalWarehouses,
            cancellationToken);
        var rows = await ReadAsync<List<PortalWarehouseResponse>>(
            response,
            cancellationToken);
        return rows ?? [];
    }

    public async Task<PortalAtpResponse> GetAtpAsync(
        string accessToken,
        Guid materialId,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        var url =
            $"{ApiRoutes.PortalAtp}?materialId={materialId:D}&warehouseId={warehouseId:D}";
        using var response = await client.GetAsync(url, cancellationToken);
        return await ReadAsync<PortalAtpResponse>(response, cancellationToken);
    }

    public async Task<PortalSalesOrderRow> CreateSalesOrderAsync(
        string accessToken,
        CreateSalesOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.PostAsJsonAsync(
            ApiRoutes.PortalSalesOrders,
            request,
            JsonOptions,
            cancellationToken);
        return await ReadAsync<PortalSalesOrderRow>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceResponse>> GetInvoicesAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync(
            ApiRoutes.PortalInvoices,
            cancellationToken);
        var rows = await ReadAsync<List<InvoiceResponse>>(
            response,
            cancellationToken);
        return rows ?? [];
    }

    public async Task<byte[]> DownloadInvoicePdfAsync(
        string accessToken,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync(
            ApiRoutes.PortalInvoicePdf(invoiceId),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PortalApiException(
                string.IsNullOrWhiteSpace(body)
                    ? $"Error HTTP {(int)response.StatusCode}"
                    : body);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private HttpClient CreateClient() =>
        _httpClientFactory.CreateClient("portal-api");

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<T>(
                JsonOptions,
                cancellationToken);
            if (payload is null)
            {
                throw new PortalApiException("La respuesta del servidor está vacía.");
            }

            return payload;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new PortalApiException(
            TryReadTitle(body)
            ?? $"El servidor respondió {(int)response.StatusCode}.");
    }

    private static string? TryReadTitle(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

public sealed class PortalApiException : Exception
{
    public PortalApiException()
    {
    }

    public PortalApiException(string message)
        : base(message)
    {
    }

    public PortalApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
