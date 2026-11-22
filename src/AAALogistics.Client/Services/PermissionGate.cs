namespace AAALogistics.Client.Services;

internal sealed class PermissionGate
{
    private readonly HashSet<string> _permissions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _roles = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Permissions => _permissions;

    public IReadOnlyCollection<string> Roles => _roles;

    public bool IsAdmin => _roles.Contains("Admin");

    public void Replace(
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        _roles.Clear();
        _permissions.Clear();
        foreach (var role in roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                _roles.Add(role.Trim());
            }
        }

        foreach (var permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                _permissions.Add(permission.Trim());
            }
        }
    }

    public bool Has(string permission) =>
        IsAdmin || _permissions.Contains(permission);

    public bool HasAny(params string[] permissions) =>
        permissions.Any(Has);

    public void Clear()
    {
        _roles.Clear();
        _permissions.Clear();
    }
}
