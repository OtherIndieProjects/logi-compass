namespace AAALogistics.Domain.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Gerente = "Gerente";
    public const string Compras = "Compras";
    public const string Ventas = "Ventas";
    public const string Almacen = "Almacen";
    public const string Finanzas = "Finanzas";
    public const string Calidad = "Calidad";
    public const string Customer = "Cliente";

    // Legado (solo migración / IsLegacy); no se asignan en UI ni seed All.
    public const string GerenteLogistica = "GerenteLogistica";
    public const string CoordinadorLogistica = "CoordinadorLogistica";
    public const string Administrativo = "Administrativo";
    public const string AsistenteLogistica = "AsistenteLogistica";
    public const string OperarioLogistica = "OperarioLogistica";
    public const string Worker = "Trabajador";

    public static IReadOnlyList<string> All { get; } =
    [
        Admin,
        Gerente,
        Compras,
        Ventas,
        Almacen,
        Finanzas,
        Calidad,
        Customer,
    ];

    public static IReadOnlyList<string> InternalAssignable { get; } =
    [
        Admin,
        Gerente,
        Compras,
        Ventas,
        Almacen,
        Finanzas,
        Calidad,
    ];

    public static IReadOnlyList<string> LegacyInternal { get; } =
    [
        GerenteLogistica,
        CoordinadorLogistica,
        Administrativo,
        AsistenteLogistica,
        OperarioLogistica,
        Worker,
    ];

    public static bool IsInternal(string role) =>
        InternalAssignable.Contains(role, StringComparer.Ordinal)
        || LegacyInternal.Contains(role, StringComparer.Ordinal);

    public static bool IsCustomer(string role) =>
        string.Equals(role, Customer, StringComparison.Ordinal);

    public static bool IsLegacy(string role) =>
        LegacyInternal.Contains(role, StringComparer.Ordinal);

    /// <summary>Mapea un rango legado (o actual) a uno o más rangos SAP.</summary>
    public static IReadOnlyList<string> MapLegacyToSap(string role) =>
        role switch
        {
            Admin => [Admin],
            Gerente or GerenteLogistica => [Gerente],
            Compras => [Compras],
            Ventas => [Ventas],
            Almacen => [Almacen],
            Finanzas => [Finanzas],
            Calidad => [Calidad],
            Customer => [Customer],
            CoordinadorLogistica => [Almacen, Compras],
            Administrativo or AsistenteLogistica or OperarioLogistica or Worker => [Almacen],
            _ => [],
        };

    public static IReadOnlyList<string> MapLegacyRolesToSap(IEnumerable<string> roles)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            foreach (var mapped in MapLegacyToSap(role))
            {
                set.Add(mapped);
            }
        }

        return set.Order(StringComparer.Ordinal).ToArray();
    }
}
