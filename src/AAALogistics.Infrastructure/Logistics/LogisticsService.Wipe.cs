using AAALogistics.Application.Logistics;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Logistics;

public sealed partial class LogisticsService
{
    /// <summary>
    /// Vacía maestros y datos operativos de logística. Conserva usuarios Identity,
    /// roles, cadena de auditoría y migraciones. Exige confirm = YES.
    /// </summary>
    public async Task<WipeCatalogResult> WipeCatalogAsync(
        string confirm,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(confirm, "YES", StringComparison.Ordinal))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-WIPE-0001",
                "wipe-catalog exige --confirm YES.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Desvincular usuarios de clientes antes de borrar maestros.
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE identity_users
            SET customer_id = NULL, company_id = NULL
            WHERE customer_id IS NOT NULL OR company_id IS NOT NULL
            """,
            cancellationToken);

        // Orden hoja → raíz (ExecuteDelete no dispara la cadena de auditoría).
        var deleted = 0;
        deleted += await _dbContext.PrepPickLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.OrderPrepSessions.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.WaveLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Waves.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.DeliveryLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Deliveries.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.GoodsReceiptLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.GoodsReceipts.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.SalesOrderLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.SalesOrders.IgnoreQueryFilters()
            .ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.QuoteLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Quotes.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.PurchaseOrderLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.PurchaseOrders.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.PurchaseRequisitions.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.InventoryCountLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.InventoryCounts.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.StockReservations.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.StockBalances.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.StockMovements.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.InvoiceLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Invoices.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.InvoiceSeries.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.IssuerFiscalProfiles.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.CmrDocuments.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.HandlingUnits.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Lots.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.MaterialDeletionRequests.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.TransportStops.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.TransportRoutes.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.RateCardLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.RateCards.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Carriers.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.IdempotencyRecords.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.DocumentNumberRanges.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.CustomerCommercialTerms.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.MensajesOutbox.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.MapeosIntegracion.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.IntercambiosExternos.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.LegalAcceptances.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.RegistrationRequests.ExecuteDeleteAsync(cancellationToken);

        // Studio (hojas)
        deleted += await _dbContext.RmaLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.RmaDocuments.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.RfqQuotes.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.RfqLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.RfqDocuments.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.FrameworkContractLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.FrameworkContracts.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.ThreeWayMatchRecords.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.SupplierInvoiceStubs.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.MaterialValuations.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.JournalLines.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.JournalEntries.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.BomComponents.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.BillOfMaterials.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.RouteOperations.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.ProductionRoutes.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.ProductionOrders.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.InspectionCharacteristics.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.InspectionPlans.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.QualityHolds.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.QualityCertificates.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.PlantWarehouses.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.UserOrgContexts.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.ApprovalRequests.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.ApprovalRules.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.IntegrationInboxMessages.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.IntegrationConnectors.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.EmployeeProfiles.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.DomainEventLogs.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.PluginRegistrations.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.ConfigTransports.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.PriceConditions.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.TaxCodes.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.GlAccounts.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.CostCenters.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Plants.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.ExchangeRates.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Companies.ExecuteDeleteAsync(cancellationToken);

        // Maestros
        deleted += await _dbContext.LocationNodes.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Warehouses.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.UnitConversions.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Materials.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.UnitsOfMeasure.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Suppliers.ExecuteDeleteAsync(cancellationToken);
        deleted += await _dbContext.Customers.IgnoreQueryFilters()
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new WipeCatalogResult(
            deleted,
            "Catálogo y datos operativos vaciados. Usuarios Identity conservados. Cree maestros desde cero.");
    }
}
