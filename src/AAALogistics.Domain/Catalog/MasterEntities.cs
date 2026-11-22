using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Catalog;

public sealed class UnitOfMeasure : Entity
{
    private UnitOfMeasure()
    {
    }

    public UnitOfMeasure(string code, string name, int decimalScale)
    {
        Code = NormalizeCode(code, 16);
        Name = Required(name, nameof(name), 100);
        if (decimalScale is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decimalScale),
                decimalScale,
                "La escala decimal debe estar entre 0 y 6.");
        }

        DecimalScale = decimalScale;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int DecimalScale { get; private set; }

    public void Update(string name, int decimalScale)
    {
        Name = Required(name, nameof(name), 100);
        if (decimalScale is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decimalScale),
                decimalScale,
                "La escala decimal debe estar entre 0 y 6.");
        }

        DecimalScale = decimalScale;
    }

    internal static string NormalizeCode(string value, int maxLength)
    {
        var code = Required(value, nameof(value), maxLength);
        return code.ToUpperInvariant();
    }

    internal static string Required(
        string value,
        string parameterName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "El valor es obligatorio.",
                parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"El valor supera {maxLength} caracteres.",
                parameterName);
        }

        return normalized;
    }
}

public sealed class Material : Entity
{
    private Material()
    {
    }

    public Material(
        string code,
        string displayName,
        Guid unitId,
        decimal catalogPrice,
        string currency,
        string? externalId = null,
        bool requiresLot = false,
        string? description = null)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 40);
        DisplayName = UnitOfMeasure.Required(
            displayName,
            nameof(displayName),
            120);
        Description = UnitOfMeasure.Required(
            string.IsNullOrWhiteSpace(description) ? displayName : description,
            nameof(description),
            250);
        UnitId = unitId == Guid.Empty
            ? throw new ArgumentException(
                "La unidad es obligatoria.",
                nameof(unitId))
            : unitId;
        SetPrice(catalogPrice, currency);
        ExternalId = NormalizeOptional(externalId, 100);
        IsActive = true;
        RequiresLot = requiresLot;
    }

    public string Code { get; private set; } = string.Empty;

    /// <summary>Visible short name shown in Maestros (Código + Nombre).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid UnitId { get; private set; }

    public decimal CatalogPrice { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public string? ExternalId { get; private set; }

    /// <summary>When true, goods receipt requires a lot code and picking uses FEFO.</summary>
    public bool RequiresLot { get; private set; }

    public void Update(
        string displayName,
        decimal catalogPrice,
        string currency,
        bool isActive,
        string? externalId,
        string? description = null)
    {
        DisplayName = UnitOfMeasure.Required(
            displayName,
            nameof(displayName),
            120);
        Description = UnitOfMeasure.Required(
            string.IsNullOrWhiteSpace(description) ? displayName : description,
            nameof(description),
            250);
        SetPrice(catalogPrice, currency);
        IsActive = isActive;
        ExternalId = NormalizeOptional(externalId, 100);
    }

    public void SetRequiresLot(bool requiresLot) => RequiresLot = requiresLot;

    public void Deactivate() => IsActive = false;

    private void SetPrice(decimal catalogPrice, string currency)
    {
        if (catalogPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(catalogPrice),
                catalogPrice,
                "El precio no puede ser negativo.");
        }

        CatalogPrice = catalogPrice;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        if (Currency.Length != 3)
        {
            throw new ArgumentException(
                "La moneda debe usar un código ISO de tres letras.",
                nameof(currency));
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"El valor supera {maxLength} caracteres.",
                nameof(value));
        }

        return normalized;
    }
}

public sealed class Warehouse : Entity
{
    private Warehouse()
    {
    }

    public Warehouse(
        string code,
        string name,
        AAALogistics.Domain.Warehouse.WarehouseSiteKind siteKind =
            AAALogistics.Domain.Warehouse.WarehouseSiteKind.Generic)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        Name = UnitOfMeasure.Required(name, nameof(name), 150);
        SiteKind = siteKind;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public AAALogistics.Domain.Warehouse.WarehouseSiteKind SiteKind { get; private set; }

    public void Update(
        string name,
        AAALogistics.Domain.Warehouse.WarehouseSiteKind? siteKind = null)
    {
        Name = UnitOfMeasure.Required(name, nameof(name), 150);
        if (siteKind is { } kind)
        {
            SiteKind = kind;
        }
    }
}

public sealed class Customer : Entity, IPersonDataEntity
{
    private Customer()
    {
    }

    public Customer(
        string name,
        string taxId,
        string addressLine1,
        string city,
        string postalCode,
        string countryCode,
        string? addressLine2 = null,
        string? region = null,
        string? externalId = null)
    {
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        TaxId = UnitOfMeasure.NormalizeCode(taxId, 40);
        AddressLine1 = UnitOfMeasure.Required(
            addressLine1,
            nameof(addressLine1),
            200);
        AddressLine2 = Optional(addressLine2, 200);
        City = UnitOfMeasure.Required(city, nameof(city), 100);
        Region = Optional(region, 100);
        PostalCode = UnitOfMeasure.Required(
            postalCode,
            nameof(postalCode),
            30);
        CountryCode = UnitOfMeasure.NormalizeCode(countryCode, 2);
        ExternalId = Optional(externalId, 100);
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string TaxId { get; private set; } = string.Empty;

    public string AddressLine1 { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string? Region { get; private set; }

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public string? ExternalId { get; private set; }

    public bool IsActive { get; private set; }

    Guid IPersonDataEntity.PersonId => Id;

    public void Update(
        string name,
        string addressLine1,
        string city,
        string postalCode,
        string countryCode,
        bool isActive,
        string? addressLine2 = null,
        string? region = null,
        string? externalId = null)
    {
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        AddressLine1 = UnitOfMeasure.Required(
            addressLine1,
            nameof(addressLine1),
            200);
        AddressLine2 = Optional(addressLine2, 200);
        City = UnitOfMeasure.Required(city, nameof(city), 100);
        Region = Optional(region, 100);
        PostalCode = UnitOfMeasure.Required(
            postalCode,
            nameof(postalCode),
            30);
        CountryCode = UnitOfMeasure.NormalizeCode(countryCode, 2);
        ExternalId = Optional(externalId, 100);
        IsActive = isActive;
    }

    public void Deactivate() => IsActive = false;

    private static string? Optional(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : UnitOfMeasure.Required(value, nameof(value), maxLength);
}

public sealed class Supplier : Entity, IPersonDataEntity
{
    private Supplier()
    {
    }

    public Supplier(
        string name,
        string taxId,
        string addressLine1,
        string city,
        string postalCode,
        string countryCode,
        string? addressLine2 = null,
        string? region = null,
        string? externalId = null,
        int? leadTimeDays = null)
    {
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        TaxId = UnitOfMeasure.NormalizeCode(taxId, 40);
        AddressLine1 = UnitOfMeasure.Required(
            addressLine1,
            nameof(addressLine1),
            200);
        AddressLine2 = Optional(addressLine2, 200);
        City = UnitOfMeasure.Required(city, nameof(city), 100);
        Region = Optional(region, 100);
        PostalCode = UnitOfMeasure.Required(
            postalCode,
            nameof(postalCode),
            30);
        CountryCode = UnitOfMeasure.NormalizeCode(countryCode, 2);
        ExternalId = Optional(externalId, 100);
        if (leadTimeDays is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leadTimeDays),
                leadTimeDays,
                "El plazo de suministro no puede ser negativo.");
        }

        LeadTimeDays = leadTimeDays;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string TaxId { get; private set; } = string.Empty;

    public string AddressLine1 { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string? Region { get; private set; }

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public string? ExternalId { get; private set; }

    /// <summary>Optional supplier lead time in calendar days (informational).</summary>
    public int? LeadTimeDays { get; private set; }

    public bool IsActive { get; private set; }

    Guid IPersonDataEntity.PersonId => Id;

    public void Update(
        string name,
        string addressLine1,
        string city,
        string postalCode,
        string countryCode,
        bool isActive,
        string? addressLine2 = null,
        string? region = null,
        string? externalId = null,
        int? leadTimeDays = null)
    {
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        AddressLine1 = UnitOfMeasure.Required(
            addressLine1,
            nameof(addressLine1),
            200);
        AddressLine2 = Optional(addressLine2, 200);
        City = UnitOfMeasure.Required(city, nameof(city), 100);
        Region = Optional(region, 100);
        PostalCode = UnitOfMeasure.Required(
            postalCode,
            nameof(postalCode),
            30);
        CountryCode = UnitOfMeasure.NormalizeCode(countryCode, 2);
        ExternalId = Optional(externalId, 100);
        IsActive = isActive;
        SetLeadTimeDays(leadTimeDays);
    }

    public void SetLeadTimeDays(int? leadTimeDays)
    {
        if (leadTimeDays is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leadTimeDays),
                leadTimeDays,
                "El plazo de suministro no puede ser negativo.");
        }

        LeadTimeDays = leadTimeDays;
    }

    public void Deactivate() => IsActive = false;

    private static string? Optional(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : UnitOfMeasure.Required(value, nameof(value), maxLength);
}

public sealed class DocumentNumberRange
{
    private DocumentNumberRange()
    {
    }

    internal DocumentNumberRange(string documentType, string prefix)
    {
        DocumentType = UnitOfMeasure.NormalizeCode(documentType, 40);
        Prefix = UnitOfMeasure.NormalizeCode(prefix, 12);
        NextValue = 1;
    }

    public string DocumentType { get; private set; } = string.Empty;

    public string Prefix { get; private set; } = string.Empty;

    public long NextValue { get; private set; }

    internal string TakeNext()
    {
        var number = $"{Prefix}-{NextValue:D8}";
        NextValue++;
        return number;
    }

    /// <summary>Admin: ajusta prefijo y próximo número (no baja el contador por debajo del actual).</summary>
    internal void Reconfigure(string prefix, long nextValue)
    {
        Prefix = UnitOfMeasure.NormalizeCode(prefix, 12);
        if (nextValue < NextValue)
        {
            throw new InvalidOperationException(
                "El próximo número no puede ser inferior al ya consumido.");
        }

        NextValue = nextValue;
    }
}
