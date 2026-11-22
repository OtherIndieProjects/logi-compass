using AAALogistics.Domain.Studio;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext
{
    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
    public DbSet<PriceCondition> PriceConditions => Set<PriceCondition>();
    public DbSet<RmaDocument> RmaDocuments => Set<RmaDocument>();
    public DbSet<RmaLine> RmaLines => Set<RmaLine>();
    public DbSet<FrameworkContract> FrameworkContracts => Set<FrameworkContract>();
    public DbSet<FrameworkContractLine> FrameworkContractLines => Set<FrameworkContractLine>();
    public DbSet<RfqDocument> RfqDocuments => Set<RfqDocument>();
    public DbSet<RfqLine> RfqLines => Set<RfqLine>();
    public DbSet<RfqQuote> RfqQuotes => Set<RfqQuote>();
    public DbSet<MaterialValuation> MaterialValuations => Set<MaterialValuation>();
    public DbSet<ThreeWayMatchRecord> ThreeWayMatchRecords => Set<ThreeWayMatchRecord>();
    public DbSet<SupplierInvoiceStub> SupplierInvoiceStubs => Set<SupplierInvoiceStub>();
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<BillOfMaterials> BillOfMaterials => Set<BillOfMaterials>();
    public DbSet<BomComponent> BomComponents => Set<BomComponent>();
    public DbSet<ProductionRoute> ProductionRoutes => Set<ProductionRoute>();
    public DbSet<RouteOperation> RouteOperations => Set<RouteOperation>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<InspectionPlan> InspectionPlans => Set<InspectionPlan>();
    public DbSet<InspectionCharacteristic> InspectionCharacteristics => Set<InspectionCharacteristic>();
    public DbSet<QualityHold> QualityHolds => Set<QualityHold>();
    public DbSet<QualityCertificate> QualityCertificates => Set<QualityCertificate>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<PlantWarehouse> PlantWarehouses => Set<PlantWarehouse>();
    public DbSet<UserOrgContext> UserOrgContexts => Set<UserOrgContext>();
    public DbSet<ApprovalRule> ApprovalRules => Set<ApprovalRule>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<IntegrationConnector> IntegrationConnectors => Set<IntegrationConnector>();
    public DbSet<IntegrationInboxMessage> IntegrationInboxMessages => Set<IntegrationInboxMessage>();
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<DomainEventLog> DomainEventLogs => Set<DomainEventLog>();
    public DbSet<PluginRegistration> PluginRegistrations => Set<PluginRegistration>();
    public DbSet<ConfigTransport> ConfigTransports => Set<ConfigTransport>();

    private static void ConfigureStudio(ModelBuilder builder)
    {
        builder.Entity<TaxCode>(entity =>
        {
            entity.ToTable("tax_codes");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(16);
            entity.Property(value => value.Name).HasMaxLength(100);
            entity.Property(value => value.CountryCode).HasMaxLength(2);
            entity.Property(value => value.RatePercent).HasPrecision(8, 4);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<PriceCondition>(entity =>
        {
            entity.ToTable("price_conditions");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(30);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.Property(value => value.PercentOff).HasPrecision(8, 4);
            entity.Property(value => value.AmountOff).HasPrecision(18, 4);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<RmaDocument>(entity =>
        {
            entity.ToTable("rma_documents");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Number).HasMaxLength(40);
            entity.Property(value => value.Reason).HasMaxLength(400);
            entity.Property(value => value.Status).HasConversion<int>();
            entity.HasIndex(value => value.Number).IsUnique();
        });

        builder.Entity<RmaLine>(entity =>
        {
            entity.ToTable("rma_lines");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Quantity).HasPrecision(18, 6);
            entity.Property(value => value.UnitSnapshot).HasMaxLength(16);
            entity.HasOne<RmaDocument>()
                .WithMany()
                .HasForeignKey(value => value.RmaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FrameworkContract>(entity =>
        {
            entity.ToTable("framework_contracts");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Number).HasMaxLength(40);
            entity.Property(value => value.Currency).HasMaxLength(3);
            entity.Property(value => value.Status).HasConversion<int>();
            entity.HasIndex(value => value.Number).IsUnique();
        });

        builder.Entity<FrameworkContractLine>(entity =>
        {
            entity.ToTable("framework_contract_lines");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.TargetQty).HasPrecision(18, 6);
            entity.Property(value => value.UnitPrice).HasPrecision(18, 4);
            entity.Property(value => value.UnitSnapshot).HasMaxLength(16);
            entity.HasOne<FrameworkContract>()
                .WithMany()
                .HasForeignKey(value => value.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RfqDocument>(entity =>
        {
            entity.ToTable("rfq_documents");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Number).HasMaxLength(40);
            entity.Property(value => value.Title).HasMaxLength(200);
            entity.Property(value => value.Status).HasConversion<int>();
            entity.HasIndex(value => value.Number).IsUnique();
        });

        builder.Entity<RfqLine>(entity =>
        {
            entity.ToTable("rfq_lines");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Quantity).HasPrecision(18, 6);
            entity.Property(value => value.UnitSnapshot).HasMaxLength(16);
            entity.HasOne<RfqDocument>()
                .WithMany()
                .HasForeignKey(value => value.RfqId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RfqQuote>(entity =>
        {
            entity.ToTable("rfq_quotes");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.TotalAmount).HasPrecision(18, 4);
            entity.Property(value => value.Currency).HasMaxLength(3);
            entity.Property(value => value.Notes).HasMaxLength(400);
        });

        builder.Entity<MaterialValuation>(entity =>
        {
            entity.ToTable("material_valuations");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Method).HasConversion<int>();
            entity.Property(value => value.StandardPrice).HasPrecision(18, 4);
            entity.Property(value => value.MovingAveragePrice).HasPrecision(18, 4);
            entity.Property(value => value.Currency).HasMaxLength(3);
            entity.HasIndex(value => new { value.MaterialId, value.WarehouseId }).IsUnique();
        });

        builder.Entity<ThreeWayMatchRecord>(entity =>
        {
            entity.ToTable("three_way_match_records");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.PoAmount).HasPrecision(18, 4);
            entity.Property(value => value.GrAmount).HasPrecision(18, 4);
            entity.Property(value => value.InvoiceAmount).HasPrecision(18, 4);
            entity.Property(value => value.Notes).HasMaxLength(400);
        });

        builder.Entity<SupplierInvoiceStub>(entity =>
        {
            entity.ToTable("supplier_invoice_stubs");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Number).HasMaxLength(40);
            entity.Property(value => value.Amount).HasPrecision(18, 4);
            entity.Property(value => value.Currency).HasMaxLength(3);
            entity.HasIndex(value => value.Number).IsUnique();
        });

        builder.Entity<GlAccount>(entity =>
        {
            entity.ToTable("gl_accounts");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(20);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.Property(value => value.AccountType).HasConversion<int>();
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<CostCenter>(entity =>
        {
            entity.ToTable("cost_centers");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(20);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("journal_entries");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Number).HasMaxLength(40);
            entity.Property(value => value.Source).HasMaxLength(30);
            entity.Property(value => value.Description).HasMaxLength(400);
            entity.HasIndex(value => value.Number).IsUnique();
        });

        builder.Entity<JournalLine>(entity =>
        {
            entity.ToTable("journal_lines");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Debit).HasPrecision(18, 4);
            entity.Property(value => value.Credit).HasPrecision(18, 4);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(value => value.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BillOfMaterials>(entity =>
        {
            entity.ToTable("bill_of_materials");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(30);
            entity.Property(value => value.Description).HasMaxLength(200);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<BomComponent>(entity =>
        {
            entity.ToTable("bom_components");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Quantity).HasPrecision(18, 6);
            entity.Property(value => value.UnitSnapshot).HasMaxLength(16);
            entity.HasOne<BillOfMaterials>()
                .WithMany()
                .HasForeignKey(value => value.BomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductionRoute>(entity =>
        {
            entity.ToTable("production_routes");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(30);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<RouteOperation>(entity =>
        {
            entity.ToTable("route_operations");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.WorkCenter).HasMaxLength(60);
            entity.Property(value => value.SetupMinutes).HasPrecision(18, 4);
            entity.Property(value => value.RunMinutes).HasPrecision(18, 4);
            entity.HasOne<ProductionRoute>()
                .WithMany()
                .HasForeignKey(value => value.RouteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductionOrder>(entity =>
        {
            entity.ToTable("production_orders");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Number).HasMaxLength(40);
            entity.Property(value => value.Quantity).HasPrecision(18, 6);
            entity.Property(value => value.UnitSnapshot).HasMaxLength(16);
            entity.Property(value => value.Status).HasConversion<int>();
            entity.HasIndex(value => value.Number).IsUnique();
        });

        builder.Entity<InspectionPlan>(entity =>
        {
            entity.ToTable("inspection_plans");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(30);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<InspectionCharacteristic>(entity =>
        {
            entity.ToTable("inspection_characteristics");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(80);
            entity.Property(value => value.Criterion).HasMaxLength(200);
        });

        builder.Entity<QualityHold>(entity =>
        {
            entity.ToTable("quality_holds");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Reason).HasMaxLength(400);
            entity.Property(value => value.Status).HasConversion<int>();
        });

        builder.Entity<QualityCertificate>(entity =>
        {
            entity.ToTable("quality_certificates");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Number).HasMaxLength(40);
            entity.Property(value => value.Result).HasMaxLength(200);
            entity.HasIndex(value => value.Number).IsUnique();
        });

        builder.Entity<Plant>(entity =>
        {
            entity.ToTable("plants");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(30);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<PlantWarehouse>(entity =>
        {
            entity.ToTable("plant_warehouses");
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.PlantId, value.WarehouseId }).IsUnique();
        });

        builder.Entity<UserOrgContext>(entity =>
        {
            entity.ToTable("user_org_contexts");
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => value.UserId).IsUnique();
        });

        builder.Entity<ApprovalRule>(entity =>
        {
            entity.ToTable("approval_rules");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.DocumentKind).HasMaxLength(40);
            entity.Property(value => value.RequiredRole).HasMaxLength(80);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.Property(value => value.MinAmount).HasPrecision(18, 4);
        });

        builder.Entity<ApprovalRequest>(entity =>
        {
            entity.ToTable("approval_requests");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.DocumentKind).HasMaxLength(40);
            entity.Property(value => value.Summary).HasMaxLength(400);
            entity.Property(value => value.Amount).HasPrecision(18, 4);
            entity.Property(value => value.Status).HasConversion<int>();
            entity.Property(value => value.RequiredRole).HasMaxLength(80);
        });

        builder.Entity<IntegrationConnector>(entity =>
        {
            entity.ToTable("integration_connectors");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(40);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.Property(value => value.Kind).HasMaxLength(30);
            entity.Property(value => value.Version).HasMaxLength(20);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<IntegrationInboxMessage>(entity =>
        {
            entity.ToTable("integration_inbox_messages");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.MessageType).HasMaxLength(60);
            entity.Property(value => value.Payload).HasMaxLength(8000);
        });

        builder.Entity<EmployeeProfile>(entity =>
        {
            entity.ToTable("employee_profiles");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.EmployeeNumber).HasMaxLength(30);
            entity.Property(value => value.DisplayName).HasMaxLength(120);
            entity.Property(value => value.Department).HasMaxLength(80);
            entity.HasIndex(value => value.EmployeeNumber).IsUnique();
            entity.HasIndex(value => value.UserId).IsUnique();
        });

        builder.Entity<DomainEventLog>(entity =>
        {
            entity.ToTable("domain_event_logs");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.EventName).HasMaxLength(80);
            entity.Property(value => value.AggregateType).HasMaxLength(80);
            entity.Property(value => value.Payload).HasMaxLength(8000);
            entity.HasIndex(value => value.OccurredUtc);
        });

        builder.Entity<PluginRegistration>(entity =>
        {
            entity.ToTable("plugin_registrations");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(40);
            entity.Property(value => value.Name).HasMaxLength(120);
            entity.Property(value => value.HookPoint).HasMaxLength(80);
            entity.Property(value => value.Description).HasMaxLength(400);
            entity.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<ConfigTransport>(entity =>
        {
            entity.ToTable("config_transports");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(40);
            entity.Property(value => value.Environment).HasMaxLength(20);
            entity.Property(value => value.PayloadJson).HasMaxLength(16000);
            entity.HasIndex(value => new { value.Code, value.Environment }).IsUnique();
        });
    }
}