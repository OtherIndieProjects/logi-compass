using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace AAALogistics.Infrastructure.Identity;

internal static class AccountGuards
{
    /// <summary>Calls without an authenticated caller (CLI, first boot, tests) are trusted local operations.</summary>
    public static async Task<bool> CallerIsAdminAsync(
        UserManager<ApplicationUser> userManager,
        Guid? callerId)
    {
        if (callerId is not Guid id)
        {
            return true;
        }

        var caller = await userManager.FindByIdAsync(id.ToString("D"));
        return caller is not null
            && await userManager.IsInRoleAsync(caller, AppRoles.Admin);
    }

    public static async Task EnsureCallerCanManageAdminAsync(
        UserManager<ApplicationUser> userManager,
        Guid? callerId,
        ApplicationUser target,
        string message)
    {
        if (await userManager.IsInRoleAsync(target, AppRoles.Admin)
            && !await CallerIsAdminAsync(userManager, callerId))
        {
            throw new InvalidOperationException(message);
        }
    }

    public static async Task EnsureNotLastActiveAdminAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser target)
    {
        if (!await userManager.IsInRoleAsync(target, AppRoles.Admin))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        var otherActive = admins.Count(user =>
            user.Id != target.Id
            && user.SuspendedUtc is null
            && (user.LockoutEnd is null || user.LockoutEnd <= now));
        if (otherActive == 0)
        {
            throw new InvalidOperationException(
                "Debe quedar al menos otro administrador activo.");
        }
    }
}
