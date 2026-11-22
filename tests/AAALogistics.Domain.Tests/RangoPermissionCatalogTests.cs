using AAALogistics.Domain.Security;

namespace AAALogistics.Domain.Tests;

public sealed class RangoPermissionCatalogTests
{
    [Fact]
    public void Cliente_Is_Exclusive_From_Internal_Assignable()
    {
        Assert.False(AppRoles.IsInternal(AppRoles.Customer));
        Assert.True(AppRoles.IsCustomer(AppRoles.Customer));
        Assert.Contains(AppRoles.Gerente, AppRoles.InternalAssignable);
        Assert.Contains(AppRoles.Compras, AppRoles.InternalAssignable);
        Assert.Contains(AppRoles.Almacen, AppRoles.InternalAssignable);
        Assert.DoesNotContain(AppRoles.Customer, AppRoles.InternalAssignable);
        Assert.DoesNotContain(AppRoles.AsistenteLogistica, AppRoles.InternalAssignable);
        Assert.DoesNotContain(AppRoles.Worker, AppRoles.All);
    }

    [Theory]
    [InlineData(AppRoles.Gerente)]
    [InlineData(AppRoles.Compras)]
    [InlineData(AppRoles.Ventas)]
    [InlineData(AppRoles.Almacen)]
    [InlineData(AppRoles.Finanzas)]
    [InlineData(AppRoles.Calidad)]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.Worker)]
    public void Sap_And_Legacy_Internal_Ranges_Are_Internal(string role) =>
        Assert.True(AppRoles.IsInternal(role));

    [Fact]
    public void Union_Of_Ventas_And_Almacen_Includes_Both_Sets()
    {
        var permissions = RangoPermissionCatalog.UnionForRoles(
            [AppRoles.Ventas, AppRoles.Almacen]);

        Assert.Contains(AppPermissions.InvoiceIssue, permissions);
        Assert.Contains(AppPermissions.SalesOrderPrepare, permissions);
        Assert.Contains(AppPermissions.WarehouseManage, permissions);
        Assert.DoesNotContain(AppPermissions.PurchaseOrderCreate, permissions);
    }

    [Fact]
    public void Gerente_Has_Internal_Broad_Except_CreateOwn_And_Terminate()
    {
        var permissions = RangoPermissionCatalog.ForRole(AppRoles.Gerente);
        Assert.Contains(AppPermissions.InvoiceConfig, permissions);
        Assert.Contains(AppPermissions.UserManageRanges, permissions);
        Assert.DoesNotContain(AppPermissions.SalesOrderCreateOwn, permissions);
        Assert.DoesNotContain(AppPermissions.UserTerminate, permissions);
    }

    [Fact]
    public void Almacen_Has_Wh_Loc_Stock_Not_Purchases_Or_Sales_Mutators()
    {
        var permissions = RangoPermissionCatalog.ForRole(AppRoles.Almacen);
        Assert.Contains(AppPermissions.LocationManage, permissions);
        Assert.Contains(AppPermissions.WarehouseManage, permissions);
        Assert.Contains(AppPermissions.StockAdjust, permissions);
        Assert.Contains(AppPermissions.CmrManage, permissions);
        Assert.Contains(AppPermissions.SalesOrderPrepare, permissions);
        Assert.Contains(AppPermissions.SalesOrderRead, permissions);
        Assert.DoesNotContain(AppPermissions.PurchaseOrderCreate, permissions);
        Assert.DoesNotContain(AppPermissions.SalesOrderCreate, permissions);
        Assert.DoesNotContain(AppPermissions.InvoiceIssue, permissions);
    }

    [Fact]
    public void Compras_Has_Po_And_Gr_Not_Sales()
    {
        var permissions = RangoPermissionCatalog.ForRole(AppRoles.Compras);
        Assert.Contains(AppPermissions.PurchaseOrderCreate, permissions);
        Assert.Contains(AppPermissions.GoodsReceiptPost, permissions);
        Assert.Contains(AppPermissions.SupplierManage, permissions);
        Assert.DoesNotContain(AppPermissions.SalesOrderCreate, permissions);
        Assert.DoesNotContain(AppPermissions.LocationManage, permissions);
    }

    [Fact]
    public void Finanzas_Has_Invoice_Config_And_Audit_Not_Stock_Mutators()
    {
        var permissions = RangoPermissionCatalog.ForRole(AppRoles.Finanzas);
        Assert.Contains(AppPermissions.InvoiceConfig, permissions);
        Assert.Contains(AppPermissions.InvoiceIssue, permissions);
        Assert.Contains(AppPermissions.StockRead, permissions);
        Assert.Contains(AppPermissions.AuditVerify, permissions);
        Assert.DoesNotContain(AppPermissions.StockAdjust, permissions);
        Assert.DoesNotContain(AppPermissions.BackupManage, permissions);
    }

    [Fact]
    public void Calidad_Has_Qm_Stock_Adjust_And_Audit_Not_Transfer()
    {
        var permissions = RangoPermissionCatalog.ForRole(AppRoles.Calidad);
        Assert.Contains(AppPermissions.MaterialManage, permissions);
        Assert.Contains(AppPermissions.StockAdjust, permissions);
        Assert.Contains(AppPermissions.AuditVerify, permissions);
        Assert.DoesNotContain(AppPermissions.StockTransfer, permissions);
        Assert.DoesNotContain(AppPermissions.PurchaseOrderCreate, permissions);
        Assert.DoesNotContain(AppPermissions.BackupManage, permissions);
    }

    [Fact]
    public void Almacen_And_Compras_Do_Not_Have_Audit_Verify()
    {
        Assert.DoesNotContain(
            AppPermissions.AuditVerify,
            RangoPermissionCatalog.ForRole(AppRoles.Almacen));
        Assert.DoesNotContain(
            AppPermissions.AuditVerify,
            RangoPermissionCatalog.ForRole(AppRoles.Compras));
        Assert.DoesNotContain(
            AppPermissions.AuditVerify,
            RangoPermissionCatalog.ForRole(AppRoles.Ventas));
        Assert.DoesNotContain(
            AppPermissions.AuditVerify,
            RangoPermissionCatalog.ForRole(AppRoles.Customer));
    }

    [Fact]
    public void Ventas_Has_Create_Prep_Invoice_Not_Warehouse()
    {
        var permissions = RangoPermissionCatalog.ForRole(AppRoles.Ventas);
        Assert.Contains(AppPermissions.SalesOrderCreate, permissions);
        Assert.Contains(AppPermissions.SalesOrderPrepare, permissions);
        Assert.Contains(AppPermissions.InvoiceIssue, permissions);
        Assert.Contains(AppPermissions.ReturnManage, permissions);
        Assert.DoesNotContain(AppPermissions.WarehouseManage, permissions);
        Assert.DoesNotContain(AppPermissions.StockAdjust, permissions);
    }

    [Fact]
    public void Admin_And_Gerente_Have_Return_Manage()
    {
        Assert.Contains(
            AppPermissions.ReturnManage,
            RangoPermissionCatalog.ForRole(AppRoles.Admin));
        Assert.Contains(
            AppPermissions.ReturnManage,
            RangoPermissionCatalog.ForRole(AppRoles.Gerente));
    }

    [Fact]
    public void Legacy_Coordinador_Maps_To_Almacen_And_Compras()
    {
        var mapped = AppRoles.MapLegacyToSap(AppRoles.CoordinadorLogistica);
        Assert.Equal(
            [AppRoles.Almacen, AppRoles.Compras],
            mapped.Order(StringComparer.Ordinal).ToArray());
    }
}
