using System.Globalization;
using System.Text.Json;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Appendix;
using AAALogistics.Domain.Auditing;
using AAALogistics.Domain.Industrial;
using AAALogistics.Domain.Integrations;
using AAALogistics.Domain.Billing;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;
using AAALogistics.Domain.Documents;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Operations;
using AAALogistics.Domain.Purchasing;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Security;
using AAALogistics.Domain.Warehouse;
using AAALogistics.Infrastructure.Auditing;
using AAALogistics.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace AAALogistics.Infrastructure.Persistence;

public sealed partial class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    AuditChainService auditChainService,
    IRequestContext requestContext)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    private static readonly HashSet<string> SensitiveProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "PasswordHash",
            "SecurityStamp",
            "ConcurrencyStamp",
            "AuthenticatorKey",
            "RecoveryCode",
            "Token",
            "TokenHash",
            "KeyMaterial",
            // ASP.NET Identity stores the TOTP seed in IdentityUserToken.Value.
            "Value",
        };

    private readonly AuditChainService _auditChainService =
        auditChainService
        ?? throw new ArgumentNullException(nameof(auditChainService));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<AuditChainHead> AuditChainHeads => Set<AuditChainHead>();

    public DbSet<AuditCheckpoint> AuditCheckpoints => Set<AuditCheckpoint>();

    public DbSet<PersonDataKey> PersonDataKeys =>
        Set<PersonDataKey>();

    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    public DbSet<Material> Materials => Set<Material>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<DocumentNumberRange> DocumentNumberRanges =>
        Set<DocumentNumberRange>();

    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();

    public DbSet<InventoryCountLine> InventoryCountLines =>
        Set<InventoryCountLine>();

    public DbSet<IdempotencyRecord> IdempotencyRecords =>
        Set<IdempotencyRecord>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines =>
        Set<PurchaseOrderLine>();

    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    public DbSet<GoodsReceiptLine> GoodsReceiptLines =>
        Set<GoodsReceiptLine>();

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    public DbSet<RegistrationRequest> RegistrationRequests =>
        Set<RegistrationRequest>();

    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();

    public DbSet<Delivery> Deliveries => Set<Delivery>();

    public DbSet<DeliveryLine> DeliveryLines => Set<DeliveryLine>();

    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LocationNode> LocationNodes => Set<LocationNode>();

    public DbSet<OrderPrepSession> OrderPrepSessions => Set<OrderPrepSession>();

    public DbSet<PrepPickLine> PrepPickLines => Set<PrepPickLine>();

    public DbSet<Lot> Lots => Set<Lot>();

    public DbSet<Carrier> Carriers => Set<Carrier>();

    public DbSet<CmrDocument> CmrDocuments => Set<CmrDocument>();

    public DbSet<HandlingUnit> HandlingUnits => Set<HandlingUnit>();

    public DbSet<InvoiceSeries> InvoiceSeries => Set<InvoiceSeries>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<IssuerFiscalProfile> IssuerFiscalProfiles =>
        Set<IssuerFiscalProfile>();

    public DbSet<MaterialDeletionRequest> MaterialDeletionRequests =>
        Set<MaterialDeletionRequest>();

    public DbSet<Wave> Waves => Set<Wave>();

    public DbSet<WaveLine> WaveLines => Set<WaveLine>();

    public DbSet<UnitConversion> UnitConversions => Set<UnitConversion>();

    public DbSet<PurchaseRequisition> PurchaseRequisitions =>
        Set<PurchaseRequisition>();

    public DbSet<RateCard> RateCards => Set<RateCard>();

    public DbSet<RateCardLine> RateCardLines => Set<RateCardLine>();

    public DbSet<TransportRoute> TransportRoutes => Set<TransportRoute>();

    public DbSet<TransportStop> TransportStops => Set<TransportStop>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public DbSet<CustomerCommercialTerms> CustomerCommercialTerms =>
        Set<CustomerCommercialTerms>();

    public DbSet<MensajeOutbox> MensajesOutbox => Set<MensajeOutbox>();

    public DbSet<MapeoIntegracion> MapeosIntegracion => Set<MapeoIntegracion>();

    public DbSet<IntercambioExterno> IntercambiosExternos =>
        Set<IntercambioExterno>();

    public Guid? CurrentCustomerId => _requestContext.CustomerId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureAudit(builder);
        ConfigureMasters(builder);
        ConfigureInventory(builder);
        ConfigurePurchasing(builder);
        ConfigureSales(builder);
        ConfigureExtendedLogistics(builder);
        ConfigureSapIntegration(builder);
        ConfigureStudio(builder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesWithAuditAsync(
                acceptAllChangesOnSuccess,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) =>
        SaveChangesWithAuditAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);

    private async Task<int> SaveChangesWithAuditAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        var changes = CaptureAuditChanges();
        if (changes.Count == 0)
        {
            return await base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken);
        }

        IDbContextTransaction? ownedTransaction = null;
        if (Database.CurrentTransaction is null)
        {
            ownedTransaction =
                await Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            await _auditChainService.AppendChangesAsync(
                this,
                changes,
                cancellationToken);
            var result = await base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken);
            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    private List<PendingAuditChange> CaptureAuditChanges()
    {
        var changes = new List<PendingAuditChange>();
        foreach (var entry in ChangeTracker.Entries()
                     .Where(IsAuditable)
                     .OrderBy(value => value.Metadata.GetTableName(), StringComparer.Ordinal)
                     .ThenBy(GetRecordId, StringComparer.Ordinal))
        {
            var action = entry.State switch
            {
                EntityState.Added => "INSERT",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted => "DELETE",
                _ => throw new InvalidOperationException(
                    "Estado de auditoría no admitido."),
            };
            var oldJson = entry.State == EntityState.Added
                ? null
                : SerializeValues(entry.OriginalValues);
            var newJson = entry.State == EntityState.Deleted
                ? null
                : SerializeValues(entry.CurrentValues);
            Guid? personId = entry.Entity is IPersonDataEntity person
                ? person.PersonId
                : null;

            changes.Add(
                new PendingAuditChange(
                    entry.Metadata.GetTableName()
                    ?? entry.Metadata.ClrType.Name,
                    GetRecordId(entry),
                    action,
                    oldJson,
                    newJson,
                    personId));
        }

        return changes;
    }

    private static bool IsAuditable(EntityEntry entry) =>
        entry.State is EntityState.Added
            or EntityState.Modified
            or EntityState.Deleted
        && entry.Entity is not AuditLogEntry
        && entry.Entity is not AuditChainHead
        && entry.Entity is not AuditCheckpoint
        && entry.Entity is not PersonDataKey
        && entry.Entity is not BackupRecord
        && entry.Entity is not RefreshToken
        // Never chain Identity tokens (AuthenticatorKey lives in Value).
        && entry.Entity is not IdentityUserToken<Guid>
        && entry.Entity is not IdentityUserLogin<Guid>;

    private static string GetRecordId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return "(sin-clave)";
        }

        return string.Join(
            ",",
            key.Properties.Select(property =>
            {
                var value = entry.Property(property.Name).CurrentValue
                    ?? entry.Property(property.Name).OriginalValue;
                return Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture) ?? "(null)";
            }));
    }

    private static string SerializeValues(PropertyValues values)
    {
        var snapshot = new SortedDictionary<string, object?>(
            StringComparer.Ordinal);
        foreach (var property in values.Properties)
        {
            var value = values[property];
            snapshot[property.Name] = SensitiveProperties.Contains(property.Name)
                ? "[REDACTED]"
                : value;
        }

        return JsonSerializer.Serialize(
            snapshot,
            new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(user =>
        {
            user.ToTable("identity_users");
            user.Property(applicationUser => applicationUser.CustomerId);
            user.HasIndex(applicationUser => applicationUser.CustomerId);
            user.HasIndex(applicationUser => applicationUser.NormalizedEmail)
                .HasDatabaseName("ix_identity_users_normalized_email");
            user.HasIndex(applicationUser => applicationUser.NormalizedUserName)
                .HasDatabaseName("ix_identity_users_normalized_user_name");
            user.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(applicationUser => applicationUser.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdentityRole<Guid>>(role =>
        {
            role.ToTable("identity_roles");
            role.HasIndex(identityRole => identityRole.NormalizedName)
                .HasDatabaseName("ix_identity_roles_normalized_name");
        });

        builder.Entity<IdentityRoleClaim<Guid>>()
            .ToTable("identity_role_claims");
        builder.Entity<IdentityUserClaim<Guid>>()
            .ToTable("identity_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>()
            .ToTable("identity_user_logins");
        builder.Entity<IdentityUserRole<Guid>>()
            .ToTable("identity_user_roles");
        builder.Entity<IdentityUserToken<Guid>>()
            .ToTable("identity_user_tokens");
    }

    private static void ConfigureAudit(ModelBuilder builder)
    {
        builder.Entity<AuditLogEntry>(entry =>
        {
            entry.ToTable("audit_log_entries");
            entry.HasKey(value => value.Sequence);
            entry.Property(value => value.Sequence).ValueGeneratedNever();
            entry.Property(value => value.PrevHash).HasMaxLength(64);
            entry.Property(value => value.EntryHash).HasMaxLength(64);
            entry.Property(value => value.TableName).HasMaxLength(128);
            entry.Property(value => value.RecordId).HasMaxLength(300);
            entry.Property(value => value.Action).HasMaxLength(32);
            entry.Property(value => value.Ip).HasMaxLength(64);
            entry.HasIndex(value => value.EntryHash).IsUnique();
        });

        builder.Entity<AuditChainHead>(head =>
        {
            head.ToTable(
                "audit_chain_heads",
                table => table.HasCheckConstraint(
                    "ck_audit_chain_heads_singleton",
                    "id = 1"));
            head.HasKey(value => value.Id);
            head.Property(value => value.LastHash).HasMaxLength(64);
            head.HasData(
                new
                {
                    Id = AuditChainHead.SingletonId,
                    LastSequence = 0L,
                    LastHash = AuditChainIntegrity.GenesisHash,
                });
        });

        builder.Entity<PersonDataKey>(key =>
        {
            key.ToTable("person_data_keys");
            key.HasKey(value => value.PersonId);
            key.Property(value => value.KeyMaterial).HasMaxLength(500);
        });

        builder.Entity<AuditCheckpoint>(checkpoint =>
        {
            checkpoint.ToTable("audit_checkpoints");
            checkpoint.HasKey(value => value.Id);
            checkpoint.Property(value => value.ThroughHash).HasMaxLength(64);
            checkpoint.Property(value => value.ExternalPath).HasMaxLength(1_000);
            checkpoint.Property(value => value.Note).HasMaxLength(500);
            checkpoint.HasIndex(value => value.ThroughSequence);
            checkpoint.HasIndex(value => value.CreatedUtc);
        });

        builder.Entity<BackupRecord>(backup =>
        {
            backup.ToTable("backup_records");
            backup.HasKey(value => value.Id);
            backup.Property(value => value.FileName).HasMaxLength(500);
            backup.Property(value => value.Sha256).HasMaxLength(64);
            backup.Property(value => value.Error).HasMaxLength(2_000);
            backup.HasIndex(value => value.StartedUtc);
        });
    }

    private void ConfigureMasters(ModelBuilder builder)
    {
        builder.Entity<UnitOfMeasure>(unit =>
        {
            unit.ToTable(
                "units_of_measure",
                table => table.HasCheckConstraint(
                    "ck_units_of_measure_decimal_scale",
                    "decimal_scale BETWEEN 0 AND 6"));
            unit.HasKey(value => value.Id);
            unit.Property(value => value.Code).HasMaxLength(16);
            unit.Property(value => value.Name).HasMaxLength(100);
            unit.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<Material>(material =>
        {
            material.ToTable(
                "materials",
                table => table.HasCheckConstraint(
                    "ck_materials_catalog_price",
                    "catalog_price >= 0"));
            material.HasKey(value => value.Id);
            material.Property(value => value.Code).HasMaxLength(40);
            material.Property(value => value.DisplayName).HasMaxLength(120);
            material.Property(value => value.Description).HasMaxLength(250);
            material.Property(value => value.CatalogPrice)
                .HasPrecision(18, 4);
            material.Property(value => value.Currency).HasMaxLength(3);
            material.Property(value => value.ExternalId).HasMaxLength(100);
            material.Property(value => value.RequiresLot)
                .HasDefaultValue(false);
            material.HasIndex(value => value.Code).IsUnique();
            material.HasIndex(value => value.ExternalId)
                .IsUnique()
                .HasFilter("external_id IS NOT NULL");
            material.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(value => value.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Warehouse>(warehouse =>
        {
            warehouse.ToTable("warehouses");
            warehouse.HasKey(value => value.Id);
            warehouse.Property(value => value.Code).HasMaxLength(30);
            warehouse.Property(value => value.Name).HasMaxLength(150);
            warehouse.Property(value => value.SiteKind).HasConversion<string>().HasMaxLength(20);
            warehouse.HasIndex(value => value.Code).IsUnique();
        });

        ConfigureParty<Customer>(builder, "customers");
        ConfigureParty<Supplier>(builder, "suppliers");

        builder.Entity<Supplier>()
            .Property(value => value.LeadTimeDays);

        builder.Entity<Customer>()
            .HasQueryFilter(
                customer =>
                    CurrentCustomerId == null
                    || customer.Id == CurrentCustomerId);

        builder.Entity<DocumentNumberRange>(range =>
        {
            range.ToTable(
                "document_number_ranges",
                table => table.HasCheckConstraint(
                    "ck_document_number_ranges_next_value",
                    "next_value > 0"));
            range.HasKey(value => value.DocumentType);
            range.Property(value => value.DocumentType).HasMaxLength(40);
            range.Property(value => value.Prefix).HasMaxLength(12);
        });
    }

    private static void ConfigureParty<TParty>(
        ModelBuilder builder,
        string tableName)
        where TParty : Entity
    {
        builder.Entity<TParty>(party =>
        {
            party.ToTable(tableName);
            party.HasKey("Id");
            party.Property<string>("Name").HasMaxLength(200);
            party.Property<string>("TaxId").HasMaxLength(40);
            party.Property<string>("AddressLine1").HasMaxLength(200);
            party.Property<string?>("AddressLine2").HasMaxLength(200);
            party.Property<string>("City").HasMaxLength(100);
            party.Property<string?>("Region").HasMaxLength(100);
            party.Property<string>("PostalCode").HasMaxLength(30);
            party.Property<string>("CountryCode").HasMaxLength(2);
            party.Property<string?>("ExternalId").HasMaxLength(100);
            party.HasIndex("TaxId").IsUnique();
            party.HasIndex("ExternalId")
                .IsUnique()
                .HasFilter("external_id IS NOT NULL");
        });
    }

    private static void ConfigureInventory(ModelBuilder builder)
    {
        builder.Entity<Lot>(lot =>
        {
            lot.ToTable("lots");
            lot.HasKey(value => value.Id);
            lot.Property(value => value.Code).HasMaxLength(40);
            lot.HasIndex(value => new { value.MaterialId, value.Code })
                .IsUnique();
            lot.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockBalance>(balance =>
        {
            balance.ToTable(
                "stock_balances",
                table =>
                {
                    table.HasCheckConstraint(
                        "ck_stock_balances_quantity",
                        "quantity >= 0");
                    table.HasCheckConstraint(
                        "ck_stock_balances_min_quantity",
                        "min_quantity >= 0");
                });
            balance.HasKey(value => value.Id);
            balance.Property(value => value.Quantity).HasPrecision(18, 6);
            balance.Property(value => value.MinQuantity).HasPrecision(18, 6);
            balance.Property(value => value.LocationId).IsRequired();
            balance.Property(value => value.LotId).IsRequired();
            balance.Property(value => value.OwnerCustomerId).IsRequired();
            balance.Property(value => value.Status)
                .HasConversion<int>()
                .HasDefaultValue(StockStatus.Available);
            balance.HasIndex(value => new
            {
                value.MaterialId,
                value.WarehouseId,
                value.LocationId,
                value.LotId,
                value.OwnerCustomerId,
            }).IsUnique();
            balance.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            balance.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            balance.HasOne<LocationNode>()
                .WithMany()
                .HasForeignKey(value => value.LocationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            // LotId may be Guid.Empty (non-lot sentinel) — no FK.
        });

        builder.Entity<StockMovement>(movement =>
        {
            movement.ToTable(
                "stock_movements",
                table => table.HasCheckConstraint(
                    "ck_stock_movements_signed_quantity",
                    "signed_quantity <> 0"));
            movement.HasKey(value => value.Id);
            movement.Property(value => value.SignedQuantity)
                .HasPrecision(18, 6);
            movement.Property(value => value.UnitSnapshot).HasMaxLength(16);
            movement.Property(value => value.OriginDocumentType)
                .HasMaxLength(40);
            movement.Property(value => value.Reason).HasMaxLength(500);
            movement.Property(value => value.LocationId).IsRequired();
            movement.Property(value => value.LotId).IsRequired();
            movement.Property(value => value.OwnerCustomerId).IsRequired();
            movement.HasIndex(value => value.PostedUtc);
            movement.HasIndex(value => new
            {
                value.MaterialId,
                value.WarehouseId,
                value.OwnerCustomerId,
            });
            movement.HasIndex(value => value.ReversedMovementId)
                .IsUnique()
                .HasFilter("reversed_movement_id IS NOT NULL");
            movement.HasIndex(value => value.LocationId);
            movement.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<LocationNode>()
                .WithMany()
                .HasForeignKey(value => value.LocationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<StockMovement>()
                .WithMany()
                .HasForeignKey(value => value.ReversedMovementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockReservation>(reservation =>
        {
            reservation.ToTable(
                "stock_reservations",
                table =>
                {
                    table.HasCheckConstraint(
                        "ck_stock_reservations_quantity",
                        "quantity > 0");
                    table.HasCheckConstraint(
                        "ck_stock_reservations_consumed",
                        "consumed_quantity >= 0 AND consumed_quantity <= quantity");
                });
            reservation.HasKey(value => value.Id);
            reservation.Property(value => value.Quantity).HasPrecision(18, 6);
            reservation.Property(value => value.ConsumedQuantity)
                .HasPrecision(18, 6);
            reservation.HasIndex(value => new
            {
                value.MaterialId,
                value.WarehouseId,
                value.IsActive,
            });
            reservation.HasIndex(value => value.SalesOrderId);
            reservation.HasIndex(value => value.SalesOrderLineId);
            reservation.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            reservation.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            reservation.HasOne<SalesOrder>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            reservation.HasOne<SalesOrderLine>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdempotencyRecord>(record =>
        {
            record.ToTable("idempotency_records");
            record.HasKey(value => value.Id);
            record.Property(value => value.CommandType).HasMaxLength(80);
            record.Property(value => value.Key).HasMaxLength(200);
            record.Property(value => value.RequestHash).HasMaxLength(64);
            record.Property(value => value.ResultJson).HasMaxLength(32_000);
            record.HasIndex(value => new { value.CommandType, value.Key })
                .IsUnique();
        });

        builder.Entity<InventoryCount>(count =>
        {
            count.ToTable("inventory_counts");
            count.HasKey(value => value.Id);
            count.Property(value => value.Number).HasMaxLength(40);
            count.HasIndex(value => value.Number).IsUnique();
            count.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryCountLine>(line =>
        {
            line.ToTable(
                "inventory_count_lines",
                table =>
                {
                    table.HasCheckConstraint(
                        "ck_inventory_count_lines_quantities",
                        "system_quantity >= 0 AND counted_quantity >= 0");
                });
            line.HasKey(value => value.Id);
            line.Property(value => value.SystemQuantity).HasPrecision(18, 6);
            line.Property(value => value.CountedQuantity).HasPrecision(18, 6);
            line.HasOne<InventoryCount>()
                .WithMany()
                .HasForeignKey(value => value.InventoryCountId)
                .OnDelete(DeleteBehavior.Cascade);
            line.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePurchasing(ModelBuilder builder)
    {
        builder.Entity<PurchaseOrder>(order =>
        {
            order.ToTable("purchase_orders");
            order.HasKey(value => value.Id);
            order.Property(value => value.Number).HasMaxLength(40);
            order.Property(value => value.Currency).HasMaxLength(3);
            order.Property(value => value.ExternalId).HasMaxLength(100);
            order.HasIndex(value => value.Number).IsUnique();
            order.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(value => value.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            order.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.DefaultWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseOrderLine>(line =>
        {
            line.ToTable(
                "purchase_order_lines",
                table => table.HasCheckConstraint(
                    "ck_purchase_order_lines_quantities",
                    "requested_quantity >= 0 AND confirmed_quantity >= 0 "
                    + "AND posted_quantity >= 0 AND cancelled_quantity >= 0 "
                    + "AND posted_quantity + cancelled_quantity <= confirmed_quantity "
                    + "AND confirmed_quantity <= requested_quantity"));
            line.HasKey(value => value.Id);
            ConfigureOrderLineQuantities(line);
            line.Property(value => value.PurchasePrice).HasPrecision(18, 4);
            line.Property(value => value.Currency).HasMaxLength(3);
            line.Property(value => value.UnitSnapshot).HasMaxLength(16);
            line.Ignore(value => value.OpenQuantity);
            line.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(value => value.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsReceipt>(receipt =>
        {
            receipt.ToTable("goods_receipts");
            receipt.HasKey(value => value.Id);
            receipt.Property(value => value.Number).HasMaxLength(40);
            receipt.HasIndex(value => value.Number).IsUnique();
            receipt.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(value => value.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            receipt.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(value => value.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            receipt.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.DefaultWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsReceiptLine>(line =>
        {
            line.ToTable(
                "goods_receipt_lines",
                table => table.HasCheckConstraint(
                    "ck_goods_receipt_lines_quantity",
                    "quantity > 0"));
            line.HasKey(value => value.Id);
            line.Property(value => value.Quantity).HasPrecision(18, 6);
            line.Property(value => value.UnitSnapshot).HasMaxLength(16);
            line.HasOne<GoodsReceipt>()
                .WithMany()
                .HasForeignKey(value => value.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasOne<PurchaseOrderLine>()
                .WithMany()
                .HasForeignKey(value => value.PurchaseOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ConfigureSales(ModelBuilder builder)
    {
        builder.Entity<SalesOrder>(order =>
        {
            order.ToTable("sales_orders");
            order.HasKey(value => value.Id);
            order.Property(value => value.Number).HasMaxLength(40);
            order.Property(value => value.Currency).HasMaxLength(3);
            order.Property(value => value.DeliveryAddressLine1)
                .HasMaxLength(200);
            order.Property(value => value.DeliveryAddressLine2)
                .HasMaxLength(200);
            order.Property(value => value.DeliveryCity).HasMaxLength(100);
            order.Property(value => value.DeliveryRegion).HasMaxLength(100);
            order.Property(value => value.DeliveryPostalCode).HasMaxLength(30);
            order.Property(value => value.DeliveryCountryCode).HasMaxLength(2);
            order.Property(value => value.ExternalId).HasMaxLength(100);
            order.Property(value => value.Incoterms).HasMaxLength(40);
            order.Property(value => value.CustomsReference).HasMaxLength(80);
            order.HasIndex(value => value.Number).IsUnique();
            order.HasQueryFilter(
                value =>
                    CurrentCustomerId == null
                    || value.CustomerId == CurrentCustomerId);
            order.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(value => value.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            order.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.DefaultWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Quote>(quote =>
        {
            quote.ToTable("quotes");
            quote.HasKey(value => value.Id);
            quote.Property(value => value.Number).HasMaxLength(40);
            quote.Property(value => value.Currency).HasMaxLength(3);
            quote.Property(value => value.Status).HasConversion<string>().HasMaxLength(20);
            quote.Property(value => value.Notes).HasMaxLength(500);
            quote.Property(value => value.TaxRate).HasPrecision(5, 2);
            quote.Property(value => value.NetAmount).HasPrecision(18, 2);
            quote.Property(value => value.TaxAmount).HasPrecision(18, 2);
            quote.Property(value => value.TotalAmount).HasPrecision(18, 2);
            quote.HasIndex(value => value.Number).IsUnique();
            quote.HasQueryFilter(
                value =>
                    CurrentCustomerId == null
                    || value.CustomerId == CurrentCustomerId);
            quote.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(value => value.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            quote.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.DefaultWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuoteLine>(line =>
        {
            line.ToTable("quote_lines");
            line.HasKey(value => value.Id);
            line.Property(value => value.Description).HasMaxLength(250);
            line.Property(value => value.Quantity).HasPrecision(18, 4);
            line.Property(value => value.UnitPrice).HasPrecision(18, 4);
            line.Property(value => value.TaxRate).HasPrecision(5, 2);
            line.Property(value => value.NetAmount).HasPrecision(18, 2);
            line.HasOne(value => value.Quote)
                .WithMany()
                .HasForeignKey(value => value.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
            line.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasQueryFilter(
                value =>
                    CurrentCustomerId == null
                    || value.Quote.CustomerId == CurrentCustomerId);
        });

        builder.Entity<SalesOrderLine>(line =>
        {
            line.ToTable(
                "sales_order_lines",
                table => table.HasCheckConstraint(
                    "ck_sales_order_lines_quantities",
                    "requested_quantity >= 0 AND confirmed_quantity >= 0 "
                    + "AND posted_quantity >= 0 AND cancelled_quantity >= 0 "
                    + "AND posted_quantity + cancelled_quantity <= confirmed_quantity "
                    + "AND confirmed_quantity <= requested_quantity"));
            line.HasKey(value => value.Id);
            ConfigureOrderLineQuantities(line);
            line.Property(value => value.CatalogPriceSnapshot)
                .HasPrecision(18, 4);
            line.Property(value => value.Currency).HasMaxLength(3);
            line.Property(value => value.UnitSnapshot).HasMaxLength(16);
            line.Ignore(value => value.OpenQuantity);
            line.HasOne<SalesOrder>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Delivery>(delivery =>
        {
            delivery.ToTable("deliveries");
            delivery.HasKey(value => value.Id);
            delivery.Property(value => value.Number).HasMaxLength(40);
            delivery.Property(value => value.DeliveryAddressSnapshot)
                .HasMaxLength(800);
            delivery.HasIndex(value => value.Number).IsUnique();
            delivery.HasQueryFilter(
                value =>
                    CurrentCustomerId == null
                    || value.CustomerId == CurrentCustomerId);
            delivery.HasOne<SalesOrder>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            delivery.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(value => value.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            delivery.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.DefaultWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RegistrationRequest>(request =>
        {
            request.ToTable("registration_requests");
            request.HasKey(value => value.Id);
            request.Property(value => value.CompanyName).HasMaxLength(200);
            request.Property(value => value.TaxId).HasMaxLength(40);
            request.Property(value => value.ContactName).HasMaxLength(150);
            request.Property(value => value.Email).HasMaxLength(200);
            request.Property(value => value.UserName).HasMaxLength(64);
            request.Property(value => value.AddressLine1).HasMaxLength(200);
            request.Property(value => value.City).HasMaxLength(100);
            request.Property(value => value.PostalCode).HasMaxLength(30);
            request.Property(value => value.CountryCode).HasMaxLength(2);
            request.HasIndex(value => value.TaxId)
                .IsUnique()
                .HasFilter("status = 0");
            request.HasIndex(value => value.CreatedUtc);
            request.Property(value => value.RejectionReason).HasMaxLength(500);
            request.Property(value => value.LegalVersion).HasMaxLength(64);
            request.HasQueryFilter(
                value =>
                    CurrentCustomerId == null
                    || value.CustomerId == CurrentCustomerId);
            request.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(value => value.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DeliveryLine>(line =>
        {
            line.ToTable(
                "delivery_lines",
                table => table.HasCheckConstraint(
                    "ck_delivery_lines_quantity",
                    "quantity > 0"));
            line.HasKey(value => value.Id);
            line.Property(value => value.Quantity).HasPrecision(18, 6);
            line.Property(value => value.UnitSnapshot).HasMaxLength(16);
            line.HasOne<Delivery>()
                .WithMany()
                .HasForeignKey(value => value.DeliveryId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasOne<SalesOrderLine>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LegalAcceptance>(acceptance =>
        {
            acceptance.ToTable("legal_acceptances");
            acceptance.HasKey(value => value.Id);
            acceptance.Property(value => value.Version).HasMaxLength(64);
            acceptance.Property(value => value.Ip).HasMaxLength(64);
            acceptance.HasIndex(value => value.RegistrationRequestId);
            acceptance.HasIndex(value => value.CustomerId);
            acceptance.HasQueryFilter(
                value =>
                    CurrentCustomerId == null
                    || value.CustomerId == CurrentCustomerId);
            acceptance.HasOne<RegistrationRequest>()
                .WithMany()
                .HasForeignKey(value => value.RegistrationRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            acceptance.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(value => value.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("refresh_tokens");
            token.HasKey(value => value.Id);
            token.Property(value => value.TokenHash).HasMaxLength(64);
            token.HasIndex(value => value.TokenHash).IsUnique();
            token.HasIndex(value => value.UserId);
            token.HasIndex(value => value.FamilyId);
            token.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(value => value.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOrderLineQuantities<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> line)
        where TEntity : class
    {
        line.Property<decimal>("RequestedQuantity").HasPrecision(18, 6);
        line.Property<decimal>("ConfirmedQuantity").HasPrecision(18, 6);
        line.Property<decimal>("PostedQuantity").HasPrecision(18, 6);
        line.Property<decimal>("CancelledQuantity").HasPrecision(18, 6);
    }

    private void ConfigureExtendedLogistics(ModelBuilder builder)
    {
        builder.Entity<LocationNode>(node =>
        {
            node.ToTable("location_nodes");
            node.HasKey(value => value.Id);
            node.Property(value => value.Code).HasMaxLength(40);
            node.Property(value => value.Name).HasMaxLength(150);
            node.Property(value => value.Kind).HasConversion<string>().HasMaxLength(20);
            node.HasIndex(value => new { value.WarehouseId, value.Code }).IsUnique();
            node.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            node.HasOne<LocationNode>()
                .WithMany()
                .HasForeignKey(value => value.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderPrepSession>(session =>
        {
            session.ToTable("order_prep_sessions");
            session.HasKey(value => value.Id);
            session.Property(value => value.UserName).HasMaxLength(100);
            session.Property(value => value.Mode).HasConversion<string>().HasMaxLength(20);
            session.HasIndex(value => new { value.SalesOrderId, value.UserId, value.IsActive });
            session.HasOne<SalesOrder>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PrepPickLine>(pick =>
        {
            pick.ToTable(
                "prep_pick_lines",
                table =>
                {
                    table.HasCheckConstraint(
                        "ck_prep_pick_lines_quantity",
                        "quantity > 0");
                    table.HasCheckConstraint(
                        "ck_prep_pick_lines_confirmed",
                        "confirmed_quantity >= 0 AND confirmed_quantity <= quantity");
                    table.HasCheckConstraint(
                        "ck_prep_pick_lines_consumed",
                        "consumed_quantity >= 0 AND consumed_quantity <= confirmed_quantity");
                });
            pick.HasKey(value => value.Id);
            pick.Property(value => value.Quantity).HasPrecision(18, 6);
            pick.Property(value => value.ConfirmedQuantity).HasPrecision(18, 6);
            pick.Property(value => value.ConsumedQuantity).HasPrecision(18, 6);
            pick.HasIndex(value => value.SalesOrderId);
            pick.HasIndex(value => value.SalesOrderLineId);
            pick.HasOne<SalesOrder>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            pick.HasOne<SalesOrderLine>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderLineId)
                .OnDelete(DeleteBehavior.Cascade);
            pick.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            pick.HasOne<LocationNode>()
                .WithMany()
                .HasForeignKey(value => value.SuggestedLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Carrier>(carrier =>
        {
            carrier.ToTable("carriers");
            carrier.HasKey(value => value.Id);
            carrier.Property(value => value.Code).HasMaxLength(30);
            carrier.Property(value => value.Name).HasMaxLength(200);
            carrier.Property(value => value.TaxId).HasMaxLength(40);
            carrier.Property(value => value.Phone).HasMaxLength(40);
            carrier.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<CmrDocument>(cmr =>
        {
            cmr.ToTable("cmr_documents");
            cmr.HasKey(value => value.Id);
            cmr.Property(value => value.Number).HasMaxLength(40);
            cmr.Property(value => value.VehiclePlate).HasMaxLength(20);
            cmr.Property(value => value.Remarks).HasMaxLength(500);
            cmr.HasIndex(value => value.Number).IsUnique();
            cmr.HasOne<Carrier>()
                .WithMany()
                .HasForeignKey(value => value.CarrierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HandlingUnit>(unit =>
        {
            unit.ToTable("handling_units");
            unit.HasKey(value => value.Id);
            unit.Property(value => value.Sscc).HasMaxLength(20);
            unit.HasIndex(value => value.Sscc).IsUnique();
            unit.HasIndex(value => value.SalesOrderId);
            unit.HasOne<SalesOrder>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            unit.HasOne<Delivery>()
                .WithMany()
                .HasForeignKey(value => value.DeliveryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceSeries>(series =>
        {
            series.ToTable("invoice_series");
            series.HasKey(value => value.Id);
            series.Property(value => value.Code).HasMaxLength(16);
            series.Property(value => value.Name).HasMaxLength(100);
            series.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<Invoice>(invoice =>
        {
            invoice.ToTable("invoices");
            invoice.HasKey(value => value.Id);
            invoice.Property(value => value.SeriesCode).HasMaxLength(16);
            invoice.Property(value => value.DocumentNumber).HasMaxLength(40);
            invoice.Property(value => value.Kind).HasConversion<string>().HasMaxLength(20);
            invoice.Property(value => value.Status).HasConversion<string>().HasMaxLength(20);
            invoice.Property(value => value.CustomerTaxId).HasMaxLength(40);
            invoice.Property(value => value.CustomerName).HasMaxLength(200);
            invoice.Property(value => value.CustomerAddress).HasMaxLength(300);
            invoice.Property(value => value.Currency).HasMaxLength(3);
            invoice.Property(value => value.NetAmount).HasPrecision(18, 6);
            invoice.Property(value => value.TaxRate).HasPrecision(8, 4);
            invoice.Property(value => value.TaxAmount).HasPrecision(18, 6);
            invoice.Property(value => value.TotalAmount).HasPrecision(18, 6);
            invoice.Property(value => value.SifHash).HasMaxLength(128);
            invoice.Property(value => value.PreviousSifHash).HasMaxLength(128);
            invoice.HasIndex(value => value.DocumentNumber).IsUnique();
            invoice.HasIndex(value => value.SifHash).IsUnique();
            invoice.HasQueryFilter(
                value =>
                    CurrentCustomerId == null
                    || value.CustomerId == CurrentCustomerId);
        });

        builder.Entity<InvoiceLine>(line =>
        {
            line.ToTable("invoice_lines");
            line.HasKey(value => value.Id);
            line.Property(value => value.Description).HasMaxLength(250);
            line.Property(value => value.Quantity).HasPrecision(18, 6);
            line.Property(value => value.UnitPrice).HasPrecision(18, 6);
            line.Property(value => value.TaxRate).HasPrecision(8, 4);
            line.Property(value => value.NetAmount).HasPrecision(18, 6);
            line.Property(value => value.TaxAmount).HasPrecision(18, 6);
            line.HasOne(value => value.Invoice)
                .WithMany()
                .HasForeignKey(value => value.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            line.HasQueryFilter(
                value =>
                    CurrentCustomerId == null
                    || value.Invoice.CustomerId == CurrentCustomerId);
        });

        builder.Entity<IssuerFiscalProfile>(issuer =>
        {
            issuer.ToTable("issuer_fiscal_profiles");
            issuer.HasKey(value => value.Id);
            issuer.Property(value => value.LegalName).HasMaxLength(200);
            issuer.Property(value => value.TaxId).HasMaxLength(40);
            issuer.Property(value => value.AddressLine).HasMaxLength(200);
            issuer.Property(value => value.City).HasMaxLength(100);
            issuer.Property(value => value.PostalCode).HasMaxLength(20);
            issuer.Property(value => value.CountryCode).HasMaxLength(2);
            issuer.Property(value => value.Phone).HasMaxLength(40);
            issuer.Property(value => value.Email).HasMaxLength(120);
            issuer.Property(value => value.Website).HasMaxLength(120);
            issuer.Property(value => value.SignerName).HasMaxLength(120);
            issuer.Property(value => value.SignerTitle).HasMaxLength(80);
            issuer.Property(value => value.Iban).HasMaxLength(40);
            issuer.Property(value => value.BankName).HasMaxLength(120);
            issuer.Property(value => value.PaymentMethod).HasMaxLength(120);
            issuer.Property(value => value.LegalFooterText).HasMaxLength(800);
            issuer.Property(value => value.IrpfPercent).HasPrecision(5, 2);
            issuer.Property(value => value.SealPngRelativePath).HasMaxLength(260);
            issuer.Property(value => value.SignaturePngRelativePath).HasMaxLength(260);
            issuer.Property(value => value.LogoPngRelativePath).HasMaxLength(260);
            issuer.Property(value => value.VerifactuMode).HasMaxLength(40);
        });

        builder.Entity<MaterialDeletionRequest>(request =>
        {
            request.ToTable("material_deletion_requests");
            request.HasKey(value => value.Id);
            request.Property(value => value.Reason).HasMaxLength(500);
            request.Property(value => value.Status).HasConversion<string>().HasMaxLength(20);
            request.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Wave>(wave =>
        {
            wave.ToTable("waves");
            wave.HasKey(value => value.Id);
            wave.Property(value => value.Number).HasMaxLength(40);
            wave.Property(value => value.Status).HasConversion<string>().HasMaxLength(20);
            wave.HasIndex(value => value.Number).IsUnique();
        });

        builder.Entity<WaveLine>(line =>
        {
            line.ToTable("wave_lines");
            line.HasKey(value => value.Id);
            line.HasIndex(value => new { value.WaveId, value.SalesOrderId }).IsUnique();
            line.HasOne<Wave>()
                .WithMany()
                .HasForeignKey(value => value.WaveId)
                .OnDelete(DeleteBehavior.Cascade);
            line.HasOne<SalesOrder>()
                .WithMany()
                .HasForeignKey(value => value.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UnitConversion>(conversion =>
        {
            conversion.ToTable(
                "unit_conversions",
                table => table.HasCheckConstraint(
                    "ck_unit_conversions_factor",
                    "factor > 0"));
            conversion.HasKey(value => value.Id);
            conversion.Property(value => value.Factor).HasPrecision(18, 6);
            conversion.HasIndex(value => new { value.FromUnitId, value.ToUnitId }).IsUnique();
            conversion.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(value => value.FromUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            conversion.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(value => value.ToUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseRequisition>(pr =>
        {
            pr.ToTable(
                "purchase_requisitions",
                table => table.HasCheckConstraint(
                    "ck_purchase_requisitions_quantity",
                    "quantity > 0"));
            pr.HasKey(value => value.Id);
            pr.Property(value => value.Number).HasMaxLength(40);
            pr.Property(value => value.Quantity).HasPrecision(18, 6);
            pr.Property(value => value.Status).HasConversion<string>().HasMaxLength(20);
            pr.Property(value => value.Source).HasConversion<string>().HasMaxLength(20);
            pr.HasIndex(value => value.Number).IsUnique();
            pr.HasOne<Material>()
                .WithMany()
                .HasForeignKey(value => value.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            pr.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(value => value.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RateCard>(card =>
        {
            card.ToTable("rate_cards");
            card.HasKey(value => value.Id);
            card.Property(value => value.Code).HasMaxLength(30);
            card.Property(value => value.Name).HasMaxLength(200);
            card.Property(value => value.Currency).HasMaxLength(3);
            card.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<RateCardLine>(line =>
        {
            line.ToTable("rate_card_lines");
            line.HasKey(value => value.Id);
            line.Property(value => value.ServiceCode).HasMaxLength(30);
            line.Property(value => value.UnitPrice).HasPrecision(18, 6);
            line.Property(value => value.UnitLabel).HasMaxLength(40);
            line.HasIndex(value => new { value.RateCardId, value.ServiceCode }).IsUnique();
            line.HasOne<RateCard>()
                .WithMany()
                .HasForeignKey(value => value.RateCardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TransportRoute>(route =>
        {
            route.ToTable("transport_routes");
            route.HasKey(value => value.Id);
            route.Property(value => value.Code).HasMaxLength(30);
            route.Property(value => value.Name).HasMaxLength(200);
            route.Property(value => value.DistanceKm).HasPrecision(18, 6);
            route.Property(value => value.BaseTariff).HasPrecision(18, 6);
            route.Property(value => value.RatePerKm).HasPrecision(18, 6);
            route.HasIndex(value => value.Code).IsUnique();
            route.HasOne<Carrier>()
                .WithMany()
                .HasForeignKey(value => value.CarrierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TransportStop>(stop =>
        {
            stop.ToTable("transport_stops");
            stop.HasKey(value => value.Id);
            stop.Property(value => value.AddressSnapshot).HasMaxLength(500);
            stop.HasIndex(value => new { value.RouteId, value.Sequence }).IsUnique();
            stop.HasOne<TransportRoute>()
                .WithMany()
                .HasForeignKey(value => value.RouteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Company>(company =>
        {
            company.ToTable("companies");
            company.HasKey(value => value.Id);
            company.Property(value => value.Code).HasMaxLength(30);
            company.Property(value => value.LegalName).HasMaxLength(200);
            company.Property(value => value.TaxId).HasMaxLength(40);
            company.Property(value => value.Currency).HasMaxLength(3);
            company.HasIndex(value => value.Code).IsUnique();
        });

        builder.Entity<ExchangeRate>(rate =>
        {
            rate.ToTable(
                "exchange_rates",
                table => table.HasCheckConstraint("ck_exchange_rates_rate", "rate > 0"));
            rate.HasKey(value => value.Id);
            rate.Property(value => value.FromCurrency).HasMaxLength(3);
            rate.Property(value => value.ToCurrency).HasMaxLength(3);
            rate.Property(value => value.Rate).HasPrecision(18, 8);
            rate.HasIndex(value => new
            {
                value.FromCurrency,
                value.ToCurrency,
                value.ValidOn,
            }).IsUnique();
        });

        builder.Entity<CustomerCommercialTerms>(terms =>
        {
            terms.ToTable("customer_commercial_terms");
            terms.HasKey(value => value.Id);
            terms.Property(value => value.CreditLimit).HasPrecision(18, 6);
            terms.Property(value => value.OpenReceivables).HasPrecision(18, 6);
            terms.Property(value => value.Currency).HasMaxLength(3);
            terms.HasIndex(value => new { value.CustomerId, value.CompanyId }).IsUnique();
            terms.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(value => value.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            terms.HasOne<Company>()
                .WithMany()
                .HasForeignKey(value => value.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSapIntegration(ModelBuilder builder)
    {
        builder.Entity<MensajeOutbox>(message =>
        {
            message.ToTable("mensajes_outbox");
            message.HasKey(value => value.Id);
            message.Property(value => value.MessageType).HasMaxLength(80);
            message.Property(value => value.PayloadJson).HasMaxLength(64_000);
            message.Property(value => value.IdempotencyKey).HasMaxLength(120);
            message.Property(value => value.Status).HasConversion<string>().HasMaxLength(20);
            message.Property(value => value.LastError).HasMaxLength(1000);
            message.HasIndex(value => value.IdempotencyKey).IsUnique();
            message.HasIndex(value => new { value.Status, value.NextAttemptUtc });
        });

        builder.Entity<MapeoIntegracion>(map =>
        {
            map.ToTable("mapeos_integracion");
            map.HasKey(value => value.Id);
            map.Property(value => value.SystemCode).HasMaxLength(40);
            map.Property(value => value.EntityType).HasMaxLength(40);
            map.Property(value => value.ExternalKey).HasMaxLength(120);
            map.HasIndex(value => new
            {
                value.SystemCode,
                value.EntityType,
                value.ExternalKey,
            }).IsUnique();
        });

        builder.Entity<IntercambioExterno>(exchange =>
        {
            exchange.ToTable("intercambios_externos");
            exchange.HasKey(value => value.Id);
            exchange.Property(value => value.Direction).HasMaxLength(20);
            exchange.Property(value => value.MessageType).HasMaxLength(80);
            exchange.Property(value => value.CorrelationKey).HasMaxLength(120);
            exchange.Property(value => value.Summary).HasMaxLength(250);
            exchange.Property(value => value.Detail).HasMaxLength(2000);
            exchange.HasIndex(value => value.OccurredUtc);
        });
    }
}
