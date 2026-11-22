using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Persistence;

/// <summary>
/// Fail-closed check: the runtime connection must not be a superuser and must
/// not hold BYPASSRLS, otherwise PostgreSQL RLS is ineffective.
/// </summary>
public static class DatabaseRoleGuard
{
    public const string UnsafeRoleErrorCode = "AAA-DB-ROLE-UNSAFE";

    public static async Task EnsureSafeRoleAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT r.rolsuper, r.rolbypassrls
            FROM pg_roles r
            WHERE r.rolname = current_user
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"{UnsafeRoleErrorCode}: no se pudo resolver el rol de la conexión.");
        }

        var isSuper = reader.GetBoolean(0);
        var bypassRls = reader.GetBoolean(1);
        if (isSuper || bypassRls)
        {
            throw new InvalidOperationException(
                $"{UnsafeRoleErrorCode}: la conexión actual es superusuario o BYPASSRLS "
                + $"({connection.DataSource}/{connection.Database}). "
                + "Use aaa_app (NOSUPERUSER NOBYPASSRLS).");
        }
    }
}
