using System.Collections.Frozen;
using Microsoft.AspNetCore.Identity;

namespace AAALogistics.Infrastructure.Identity;

/// <summary>
/// NIST 800-63B: reject passwords present in a common-password denylist.
/// </summary>
public sealed class CommonPasswordValidator<TUser> : IPasswordValidator<TUser>
    where TUser : class
{
    private static readonly FrozenSet<string> Denied = new[]
    {
        "password", "password123", "password1234", "123456789012",
        "qwertyuiopas", "adminadmin12", "letmeinletmein", "welcome12345",
        "changeme1234", "aaalogistics1", "administrador", "contraseña12",
        "passwordpassword", "iloveyou1234", "abc123abc123",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public Task<IdentityResult> ValidateAsync(
        UserManager<TUser> manager,
        TUser user,
        string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        if (Denied.Contains(password) || Denied.Contains(password.Trim()))
        {
            return Task.FromResult(
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "PasswordTooCommon",
                        Description =
                            "La contraseña está en la lista de contraseñas comunes y no se admite.",
                    }));
        }

        return Task.FromResult(IdentityResult.Success);
    }
}
