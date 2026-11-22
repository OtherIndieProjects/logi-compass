using AAALogistics.Domain.Common;
using AAALogistics.Domain.Security;

namespace AAALogistics.Domain.Tests;

public class EntityTests
{
    private sealed class SampleEntity : Entity;

    [Fact]
    public void New_Entity_Gets_Uuid_Version_7()
    {
        var entity = new SampleEntity();
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(7, entity.Id.Version);
    }

    [Fact]
    public void App_Roles_Are_Stable_And_Unique()
    {
        Assert.Equal(
            [
                AppRoles.Admin,
                AppRoles.Gerente,
                AppRoles.Compras,
                AppRoles.Ventas,
                AppRoles.Almacen,
                AppRoles.Finanzas,
                AppRoles.Calidad,
                AppRoles.Customer,
            ],
            AppRoles.All);
        Assert.Equal(
            AppRoles.All.Count,
            AppRoles.All.Distinct(StringComparer.Ordinal).Count());
    }
}
