namespace AAALogistics.Infrastructure.Configuration;

public sealed class SapOptions
{
    public const string SectionName = "Sap";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string? BearerToken { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public string MaterialsPath { get; set; } =
        "/sap/opu/odata/sap/API_PRODUCT_SRV/A_Product";

    public string DeliveryPushPath { get; set; } =
        "/sap/opu/odata/sap/API_OUTBOUND_DELIVERY_SRV/A_OutbDeliveryHeader";

    public string DefaultUnitCode { get; set; } = "UN";

    public int OutboxPollSeconds { get; set; } = 15;

    public void ApplyEnvironmentOverrides()
    {
        var enabled = Environment.GetEnvironmentVariable("AAA_SAP_ENABLED");
        if (!string.IsNullOrWhiteSpace(enabled)
            && bool.TryParse(enabled, out var flag))
        {
            Enabled = flag;
        }

        var baseUrl = Environment.GetEnvironmentVariable("AAA_SAP_BASE_URL");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            BaseUrl = baseUrl.Trim();
        }

        var user = Environment.GetEnvironmentVariable("AAA_SAP_USERNAME");
        if (!string.IsNullOrWhiteSpace(user))
        {
            UserName = user.Trim();
        }

        var password = Environment.GetEnvironmentVariable("AAA_SAP_PASSWORD");
        if (!string.IsNullOrEmpty(password))
        {
            Password = password;
        }

        var token = Environment.GetEnvironmentVariable("AAA_SAP_BEARER_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            BearerToken = token.Trim();
        }
    }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(BaseUrl)
            || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "Sap:Enabled=true requiere Sap:BaseUrl absoluto válido.");
        }

        if (TimeoutSeconds is < 5 or > 300)
        {
            throw new InvalidOperationException(
                "Sap:TimeoutSeconds debe estar entre 5 y 300.");
        }
    }
}
