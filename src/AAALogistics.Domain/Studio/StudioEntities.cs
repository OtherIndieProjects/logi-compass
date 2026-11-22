using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Studio;

// ——— P1 Pricing / IVA / RMA ———

public sealed class TaxCode : Entity
{
    private TaxCode()
    {
    }

    public TaxCode(string code, string name, decimal ratePercent, string countryCode)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 16);
        Name = UnitOfMeasure.Required(name, nameof(name), 100);
        if (ratePercent < 0 || ratePercent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ratePercent), ratePercent, "El tipo impositivo debe estar entre 0 y 100.");
        }

        RatePercent = ratePercent;
        CountryCode = UnitOfMeasure.NormalizeCode(countryCode, 2);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public decimal RatePercent { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;
}

public sealed class PriceCondition : Entity
{
    private PriceCondition()
    {
    }

    public PriceCondition(
        string code,
        string name,
        decimal percentOff,
        decimal amountOff,
        Guid? materialId,
        Guid? customerId,
        DateOnly validFrom,
        DateOnly? validTo)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        PercentOff = percentOff is < 0 or > 100 ? throw new ArgumentOutOfRangeException(nameof(percentOff), percentOff, "El descuento debe estar entre 0 y 100 %.") : percentOff;
        AmountOff = amountOff < 0 ? throw new ArgumentOutOfRangeException(nameof(amountOff), amountOff, "El importe de descuento no puede ser negativo.") : amountOff;
        if (percentOff == 0 && amountOff == 0)
        {
            throw new ArgumentException("Indique un descuento en % o un importe de descuento.", nameof(percentOff));
        }

        if (validTo is { } end && end < validFrom)
        {
            throw new ArgumentException("La fecha final no puede ser anterior a la inicial.", nameof(validTo));
        }

        MaterialId = materialId;
        CustomerId = customerId;
        ValidFrom = validFrom;
        ValidTo = validTo;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public decimal PercentOff { get; private set; }

    public decimal AmountOff { get; private set; }

    public Guid? MaterialId { get; private set; }

    public Guid? CustomerId { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public DateOnly? ValidTo { get; private set; }

    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;
}

public enum RmaStatus
{
    Open = 0,
    Received = 1,
    Closed = 2,
    Cancelled = 3,
}

public sealed class RmaDocument : Entity
{
    private RmaDocument()
    {
    }

    public RmaDocument(
        string number,
        Guid customerId,
        Guid? salesOrderId,
        string reason,
        DateTimeOffset createdUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        CustomerId = customerId;
        SalesOrderId = salesOrderId;
        Reason = UnitOfMeasure.Required(reason, nameof(reason), 400);
        Status = RmaStatus.Open;
        CreatedUtc = createdUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public Guid? SalesOrderId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public RmaStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public void MarkReceived() => Status = RmaStatus.Received;

    public void Close() => Status = RmaStatus.Closed;

    public void Cancel() => Status = RmaStatus.Cancelled;
}

public sealed class RmaLine : Entity
{
    private RmaLine()
    {
    }

    public RmaLine(Guid rmaId, Guid materialId, decimal quantity, string unitSnapshot)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La cantidad debe ser mayor que cero.");
        }

        RmaId = rmaId;
        MaterialId = materialId;
        Quantity = quantity;
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
    }

    public Guid RmaId { get; private set; }

    public Guid MaterialId { get; private set; }

    public decimal Quantity { get; private set; }

    public string UnitSnapshot { get; private set; } = string.Empty;
}

// ——— P2 MM ———

public enum FrameworkContractStatus
{
    Draft = 0,
    Active = 1,
    Closed = 2,
}

public sealed class FrameworkContract : Entity
{
    private FrameworkContract()
    {
    }

    public FrameworkContract(
        string number,
        Guid supplierId,
        DateOnly validFrom,
        DateOnly validTo,
        string currency,
        DateTimeOffset createdUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        SupplierId = supplierId;
        ValidFrom = validFrom;
        ValidTo = validTo;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        Status = FrameworkContractStatus.Active;
        CreatedUtc = createdUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid SupplierId { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public DateOnly ValidTo { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public FrameworkContractStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public void Close() => Status = FrameworkContractStatus.Closed;
}

public sealed class FrameworkContractLine : Entity
{
    private FrameworkContractLine()
    {
    }

    public FrameworkContractLine(
        Guid contractId,
        Guid materialId,
        decimal targetQty,
        decimal unitPrice,
        string unitSnapshot)
    {
        ContractId = contractId;
        MaterialId = materialId;
        TargetQty = targetQty <= 0 ? throw new ArgumentOutOfRangeException(nameof(targetQty), targetQty, "La cantidad objetivo debe ser mayor que cero.") : targetQty;
        UnitPrice = unitPrice < 0 ? throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice, "El precio no puede ser negativo.") : unitPrice;
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
    }

    public Guid ContractId { get; private set; }

    public Guid MaterialId { get; private set; }

    public decimal TargetQty { get; private set; }

    public decimal UnitPrice { get; private set; }

    public string UnitSnapshot { get; private set; } = string.Empty;
}

public enum RfqStatus
{
    Open = 0,
    Awarded = 1,
    Cancelled = 2,
}

public sealed class RfqDocument : Entity
{
    private RfqDocument()
    {
    }

    public RfqDocument(string number, string title, DateTimeOffset createdUtc, DateOnly? dueDate)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        Title = UnitOfMeasure.Required(title, nameof(title), 200);
        Status = RfqStatus.Open;
        CreatedUtc = createdUtc;
        DueDate = dueDate;
    }

    public string Number { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public RfqStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public void Award() => Status = RfqStatus.Awarded;

    public void Cancel() => Status = RfqStatus.Cancelled;
}

public sealed class RfqLine : Entity
{
    private RfqLine()
    {
    }

    public RfqLine(Guid rfqId, Guid materialId, decimal quantity, string unitSnapshot)
    {
        RfqId = rfqId;
        MaterialId = materialId;
        Quantity = quantity <= 0 ? throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La cantidad debe ser mayor que cero.") : quantity;
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
    }

    public Guid RfqId { get; private set; }

    public Guid MaterialId { get; private set; }

    public decimal Quantity { get; private set; }

    public string UnitSnapshot { get; private set; } = string.Empty;
}

public sealed class RfqQuote : Entity
{
    private RfqQuote()
    {
    }

    public RfqQuote(Guid rfqId, Guid supplierId, decimal totalAmount, string currency, string notes)
    {
        RfqId = rfqId;
        SupplierId = supplierId;
        TotalAmount = totalAmount < 0 ? throw new ArgumentOutOfRangeException(nameof(totalAmount), totalAmount, "El importe no puede ser negativo.") : totalAmount;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        Notes = string.IsNullOrWhiteSpace(notes) ? string.Empty : UnitOfMeasure.Required(notes, nameof(notes), 400);
    }

    public Guid RfqId { get; private set; }

    public Guid SupplierId { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string Notes { get; private set; } = string.Empty;
}

public enum StockValuationMethod
{
    Standard = 0,
    MovingAverage = 1,
}

public sealed class MaterialValuation : Entity
{
    private MaterialValuation()
    {
    }

    public MaterialValuation(
        Guid materialId,
        Guid? warehouseId,
        StockValuationMethod method,
        decimal standardPrice,
        decimal movingAveragePrice,
        string currency)
    {
        MaterialId = materialId;
        WarehouseId = warehouseId;
        Method = method;
        StandardPrice = standardPrice < 0 ? throw new ArgumentOutOfRangeException(nameof(standardPrice), standardPrice, "El precio estándar no puede ser negativo.") : standardPrice;
        MovingAveragePrice = movingAveragePrice < 0
            ? throw new ArgumentOutOfRangeException(nameof(movingAveragePrice), movingAveragePrice, "El precio medio no puede ser negativo.")
            : movingAveragePrice;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
    }

    public Guid MaterialId { get; private set; }

    public Guid? WarehouseId { get; private set; }

    public StockValuationMethod Method { get; private set; }

    public decimal StandardPrice { get; private set; }

    public decimal MovingAveragePrice { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal UnitValue =>
        Method == StockValuationMethod.Standard ? StandardPrice : MovingAveragePrice;

    public void Update(
        StockValuationMethod method,
        decimal standardPrice,
        decimal movingAveragePrice)
    {
        Method = method;
        StandardPrice = standardPrice < 0 ? throw new ArgumentOutOfRangeException(nameof(standardPrice), standardPrice, "El precio estándar no puede ser negativo.") : standardPrice;
        MovingAveragePrice = movingAveragePrice < 0
            ? throw new ArgumentOutOfRangeException(nameof(movingAveragePrice), movingAveragePrice, "El precio medio no puede ser negativo.")
            : movingAveragePrice;
    }

    public void ApplyReceipt(decimal qty, decimal unitPrice)
    {
        if (Method != StockValuationMethod.MovingAverage || qty <= 0)
        {
            return;
        }

        // Simplified: caller supplies prior qty for full MA; here we blend with previous MA as proxy.
        MovingAveragePrice = Math.Round(
            ((MovingAveragePrice) + unitPrice) / 2m,
            4,
            MidpointRounding.AwayFromZero);
    }
}

public sealed class ThreeWayMatchRecord : Entity
{
    private ThreeWayMatchRecord()
    {
    }

    public ThreeWayMatchRecord(
        Guid purchaseOrderId,
        Guid? goodsReceiptId,
        Guid? supplierInvoiceId,
        decimal poAmount,
        decimal grAmount,
        decimal invoiceAmount,
        bool matched,
        string notes,
        DateTimeOffset createdUtc)
    {
        PurchaseOrderId = purchaseOrderId;
        GoodsReceiptId = goodsReceiptId;
        SupplierInvoiceId = supplierInvoiceId;
        PoAmount = poAmount;
        GrAmount = grAmount;
        InvoiceAmount = invoiceAmount;
        Matched = matched;
        Notes = string.IsNullOrWhiteSpace(notes) ? string.Empty : UnitOfMeasure.Required(notes, nameof(notes), 400);
        CreatedUtc = createdUtc;
    }

    public Guid PurchaseOrderId { get; private set; }

    public Guid? GoodsReceiptId { get; private set; }

    public Guid? SupplierInvoiceId { get; private set; }

    public decimal PoAmount { get; private set; }

    public decimal GrAmount { get; private set; }

    public decimal InvoiceAmount { get; private set; }

    public bool Matched { get; private set; }

    public string Notes { get; private set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; private set; }
}

public sealed class SupplierInvoiceStub : Entity
{
    private SupplierInvoiceStub()
    {
    }

    public SupplierInvoiceStub(
        string number,
        Guid supplierId,
        Guid? purchaseOrderId,
        decimal amount,
        string currency,
        DateOnly invoiceDate)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        SupplierId = supplierId;
        PurchaseOrderId = purchaseOrderId;
        Amount = amount < 0 ? throw new ArgumentOutOfRangeException(nameof(amount), amount, "El importe no puede ser negativo.") : amount;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        InvoiceDate = invoiceDate;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid SupplierId { get; private set; }

    public Guid? PurchaseOrderId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public DateOnly InvoiceDate { get; private set; }
}

// ——— P3 FI/CO ———

public enum GlAccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Income = 3,
    Expense = 4,
}

public sealed class GlAccount : Entity
{
    private GlAccount()
    {
    }

    public GlAccount(string code, string name, GlAccountType accountType)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 20);
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        AccountType = accountType;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public GlAccountType AccountType { get; private set; }

    public bool IsActive { get; private set; }
}

public sealed class CostCenter : Entity
{
    private CostCenter()
    {
    }

    public CostCenter(string code, string name, Guid? companyId)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 20);
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        CompanyId = companyId;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid? CompanyId { get; private set; }

    public bool IsActive { get; private set; }
}

public sealed class JournalEntry : Entity
{
    private JournalEntry()
    {
    }

    public JournalEntry(
        string number,
        string source,
        string description,
        DateOnly postingDate,
        DateTimeOffset createdUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        Source = UnitOfMeasure.NormalizeCode(source, 30);
        Description = UnitOfMeasure.Required(description, nameof(description), 400);
        PostingDate = postingDate;
        CreatedUtc = createdUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public string Source { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public DateOnly PostingDate { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }
}

public sealed class JournalLine : Entity
{
    private JournalLine()
    {
    }

    public JournalLine(
        Guid journalEntryId,
        Guid glAccountId,
        Guid? costCenterId,
        decimal debit,
        decimal credit)
    {
        if (debit < 0 || credit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(debit), debit, "El debe y el haber no pueden ser negativos.");
        }

        if (debit > 0 && credit > 0)
        {
            throw new ArgumentException("Una línea no puede tener debe y haber.");
        }

        JournalEntryId = journalEntryId;
        GlAccountId = glAccountId;
        CostCenterId = costCenterId;
        Debit = debit;
        Credit = credit;
    }

    public Guid JournalEntryId { get; private set; }

    public Guid GlAccountId { get; private set; }

    public Guid? CostCenterId { get; private set; }

    public decimal Debit { get; private set; }

    public decimal Credit { get; private set; }
}

// ——— P4 PP ———

public sealed class BillOfMaterials : Entity
{
    private BillOfMaterials()
    {
    }

    public BillOfMaterials(string code, Guid parentMaterialId, string description)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        ParentMaterialId = parentMaterialId;
        Description = UnitOfMeasure.Required(description, nameof(description), 200);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public Guid ParentMaterialId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
}

public sealed class BomComponent : Entity
{
    private BomComponent()
    {
    }

    public BomComponent(Guid bomId, Guid componentMaterialId, decimal quantity, string unitSnapshot)
    {
        BomId = bomId;
        ComponentMaterialId = componentMaterialId;
        Quantity = quantity <= 0 ? throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La cantidad debe ser mayor que cero.") : quantity;
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
    }

    public Guid BomId { get; private set; }

    public Guid ComponentMaterialId { get; private set; }

    public decimal Quantity { get; private set; }

    public string UnitSnapshot { get; private set; } = string.Empty;
}

public sealed class ProductionRoute : Entity
{
    private ProductionRoute()
    {
    }

    public ProductionRoute(string code, Guid materialId, string name)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        MaterialId = materialId;
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public Guid MaterialId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
}

public sealed class RouteOperation : Entity
{
    private RouteOperation()
    {
    }

    public RouteOperation(Guid routeId, int sequence, string workCenter, decimal setupMinutes, decimal runMinutes)
    {
        RouteId = routeId;
        Sequence = sequence < 1 ? throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "La secuencia debe ser 1 o mayor.") : sequence;
        WorkCenter = UnitOfMeasure.Required(workCenter, nameof(workCenter), 60);
        SetupMinutes = setupMinutes < 0 ? throw new ArgumentOutOfRangeException(nameof(setupMinutes), setupMinutes, "Los minutos de preparación no pueden ser negativos.") : setupMinutes;
        RunMinutes = runMinutes < 0 ? throw new ArgumentOutOfRangeException(nameof(runMinutes), runMinutes, "Los minutos de ejecución no pueden ser negativos.") : runMinutes;
    }

    public Guid RouteId { get; private set; }

    public int Sequence { get; private set; }

    public string WorkCenter { get; private set; } = string.Empty;

    public decimal SetupMinutes { get; private set; }

    public decimal RunMinutes { get; private set; }
}

public enum ProductionOrderStatus
{
    Planned = 0,
    Released = 1,
    Completed = 2,
    Cancelled = 3,
}

public sealed class ProductionOrder : Entity
{
    private ProductionOrder()
    {
    }

    public ProductionOrder(
        string number,
        Guid materialId,
        Guid? bomId,
        Guid? routeId,
        decimal quantity,
        string unitSnapshot,
        DateTimeOffset createdUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        MaterialId = materialId;
        BomId = bomId;
        RouteId = routeId;
        Quantity = quantity <= 0 ? throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La cantidad debe ser mayor que cero.") : quantity;
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
        Status = ProductionOrderStatus.Planned;
        CreatedUtc = createdUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid MaterialId { get; private set; }

    public Guid? BomId { get; private set; }

    public Guid? RouteId { get; private set; }

    public decimal Quantity { get; private set; }

    public string UnitSnapshot { get; private set; } = string.Empty;

    public ProductionOrderStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public void Release()
    {
        if (Status != ProductionOrderStatus.Planned)
        {
            throw new InvalidOperationException("Solo se puede liberar una OF en estado Planificada.");
        }

        Status = ProductionOrderStatus.Released;
    }

    public void Complete()
    {
        if (Status != ProductionOrderStatus.Released)
        {
            throw new InvalidOperationException("Solo se puede completar una OF liberada.");
        }

        Status = ProductionOrderStatus.Completed;
    }

    public void Cancel()
    {
        if (Status == ProductionOrderStatus.Completed)
        {
            throw new InvalidOperationException("No se puede cancelar una OF ya completada.");
        }

        Status = ProductionOrderStatus.Cancelled;
    }
}

// ——— P5 QM ———

public sealed class InspectionPlan : Entity
{
    private InspectionPlan()
    {
    }

    public InspectionPlan(string code, Guid materialId, string name)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        MaterialId = materialId;
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public Guid MaterialId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
}

public sealed class InspectionCharacteristic : Entity
{
    private InspectionCharacteristic()
    {
    }

    public InspectionCharacteristic(Guid planId, string name, string criterion)
    {
        PlanId = planId;
        Name = UnitOfMeasure.Required(name, nameof(name), 80);
        Criterion = UnitOfMeasure.Required(criterion, nameof(criterion), 200);
    }

    public Guid PlanId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Criterion { get; private set; } = string.Empty;
}

public enum QualityHoldStatus
{
    Held = 0,
    Released = 1,
    Rejected = 2,
}

public sealed class QualityHold : Entity
{
    private QualityHold()
    {
    }

    public QualityHold(Guid lotId, Guid? materialId, string reason, DateTimeOffset createdUtc)
    {
        LotId = lotId;
        MaterialId = materialId;
        Reason = UnitOfMeasure.Required(reason, nameof(reason), 400);
        Status = QualityHoldStatus.Held;
        CreatedUtc = createdUtc;
    }

    public Guid LotId { get; private set; }

    public Guid? MaterialId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public QualityHoldStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public void Release()
    {
        EnsureHeld();
        Status = QualityHoldStatus.Released;
    }

    public void Reject()
    {
        EnsureHeld();
        Status = QualityHoldStatus.Rejected;
    }

    private void EnsureHeld()
    {
        if (Status != QualityHoldStatus.Held)
        {
            throw new InvalidOperationException("El bloqueo de calidad ya está liberado o rechazado.");
        }
    }
}

public sealed class QualityCertificate : Entity
{
    private QualityCertificate()
    {
    }

    public QualityCertificate(
        string number,
        Guid materialId,
        Guid? lotId,
        string result,
        DateTimeOffset issuedUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        MaterialId = materialId;
        LotId = lotId;
        Result = UnitOfMeasure.Required(result, nameof(result), 200);
        IssuedUtc = issuedUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid MaterialId { get; private set; }

    public Guid? LotId { get; private set; }

    public string Result { get; private set; } = string.Empty;

    public DateTimeOffset IssuedUtc { get; private set; }
}

// ——— P6 Org ———

public sealed class Plant : Entity
{
    private Plant()
    {
    }

    public Plant(Guid companyId, string code, string name)
    {
        CompanyId = companyId;
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        IsActive = true;
    }

    public Guid CompanyId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
}

public sealed class PlantWarehouse : Entity
{
    private PlantWarehouse()
    {
    }

    public PlantWarehouse(Guid plantId, Guid warehouseId)
    {
        PlantId = plantId;
        WarehouseId = warehouseId;
    }

    public Guid PlantId { get; private set; }

    public Guid WarehouseId { get; private set; }
}

public sealed class UserOrgContext : Entity
{
    private UserOrgContext()
    {
    }

    public UserOrgContext(Guid userId, Guid? companyId, Guid? plantId)
    {
        UserId = userId;
        CompanyId = companyId;
        PlantId = plantId;
    }

    public Guid UserId { get; private set; }

    public Guid? CompanyId { get; private set; }

    public Guid? PlantId { get; private set; }

    public void Set(Guid? companyId, Guid? plantId)
    {
        CompanyId = companyId;
        PlantId = plantId;
    }
}

// ——— P7 Workflow ———

public sealed class ApprovalRule : Entity
{
    private ApprovalRule()
    {
    }

    public ApprovalRule(string documentKind, decimal minAmount, string requiredRole, string name)
    {
        DocumentKind = UnitOfMeasure.NormalizeCode(documentKind, 40);
        MinAmount = minAmount < 0 ? throw new ArgumentOutOfRangeException(nameof(minAmount), minAmount, "El importe mínimo no puede ser negativo.") : minAmount;
        RequiredRole = UnitOfMeasure.Required(requiredRole, nameof(requiredRole), 80);
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        IsActive = true;
    }

    public string DocumentKind { get; private set; } = string.Empty;

    public decimal MinAmount { get; private set; }

    public string RequiredRole { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
}

public enum ApprovalRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public sealed class ApprovalRequest : Entity
{
    private ApprovalRequest()
    {
    }

    public ApprovalRequest(
        string documentKind,
        Guid documentId,
        decimal amount,
        string summary,
        DateTimeOffset createdUtc,
        Guid? requestedByUserId = null,
        string? requiredRole = null)
    {
        DocumentKind = UnitOfMeasure.NormalizeCode(documentKind, 40);
        DocumentId = documentId == Guid.Empty
            ? throw new ArgumentException("El documento es obligatorio.", nameof(documentId))
            : documentId;
        Amount = amount < 0 ? throw new ArgumentOutOfRangeException(nameof(amount), amount, "El importe no puede ser negativo.") : amount;
        Summary = UnitOfMeasure.Required(summary, nameof(summary), 400);
        Status = ApprovalRequestStatus.Pending;
        CreatedUtc = createdUtc;
        RequestedByUserId = requestedByUserId;
        RequiredRole = string.IsNullOrWhiteSpace(requiredRole)
            ? null
            : UnitOfMeasure.Required(requiredRole, nameof(requiredRole), 80);
    }

    public string DocumentKind { get; private set; } = string.Empty;

    public Guid DocumentId { get; private set; }

    public decimal Amount { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public ApprovalRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedUtc { get; private set; }

    public Guid? RequestedByUserId { get; private set; }

    public string? RequiredRole { get; private set; }

    public void Approve(Guid userId, DateTimeOffset utc)
    {
        EnsurePending();
        if (RequestedByUserId is { } requester && requester == userId)
        {
            throw new InvalidOperationException(
                "Quien solicita no puede aprobar su propia solicitud (principio de cuatro ojos).");
        }

        Status = ApprovalRequestStatus.Approved;
        DecidedByUserId = userId;
        DecidedUtc = utc;
    }

    public void Reject(Guid userId, DateTimeOffset utc)
    {
        EnsurePending();
        Status = ApprovalRequestStatus.Rejected;
        DecidedByUserId = userId;
        DecidedUtc = utc;
    }

    private void EnsurePending()
    {
        if (Status != ApprovalRequestStatus.Pending)
        {
            throw new InvalidOperationException("La solicitud ya fue decidida.");
        }
    }
}

// ——— P8 Integration hub ———

public sealed class IntegrationConnector : Entity
{
    private IntegrationConnector()
    {
    }

    public IntegrationConnector(string code, string name, string kind, string version)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 40);
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        Kind = UnitOfMeasure.NormalizeCode(kind, 30);
        Version = UnitOfMeasure.Required(version, nameof(version), 20);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Kind { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
}

public sealed class IntegrationInboxMessage : Entity
{
    private IntegrationInboxMessage()
    {
    }

    public IntegrationInboxMessage(
        Guid? connectorId,
        string messageType,
        string payload,
        DateTimeOffset receivedUtc)
    {
        ConnectorId = connectorId;
        MessageType = UnitOfMeasure.NormalizeCode(messageType, 60);
        Payload = UnitOfMeasure.Required(payload, nameof(payload), 8000);
        ReceivedUtc = receivedUtc;
        Processed = false;
    }

    public Guid? ConnectorId { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedUtc { get; private set; }

    public bool Processed { get; private set; }

    public void MarkProcessed() => Processed = true;
}

// ——— P10 HR ———

public sealed class EmployeeProfile : Entity
{
    private EmployeeProfile()
    {
    }

    public EmployeeProfile(
        Guid userId,
        string employeeNumber,
        string displayName,
        string department,
        DateOnly? hireDate)
    {
        UserId = userId;
        EmployeeNumber = UnitOfMeasure.NormalizeCode(employeeNumber, 30);
        DisplayName = UnitOfMeasure.Required(displayName, nameof(displayName), 120);
        Department = string.IsNullOrWhiteSpace(department)
            ? string.Empty
            : UnitOfMeasure.Required(department, nameof(department), 80);
        HireDate = hireDate;
        IsActive = true;
    }

    public Guid UserId { get; private set; }

    public string EmployeeNumber { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string Department { get; private set; } = string.Empty;

    public DateOnly? HireDate { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string displayName, string department, DateOnly? hireDate)
    {
        DisplayName = UnitOfMeasure.Required(displayName, nameof(displayName), 120);
        Department = string.IsNullOrWhiteSpace(department)
            ? string.Empty
            : UnitOfMeasure.Required(department, nameof(department), 80);
        HireDate = hireDate;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

// ——— P11 Extensibility ———

public sealed class DomainEventLog : Entity
{
    private DomainEventLog()
    {
    }

    public DomainEventLog(string eventName, string aggregateType, Guid aggregateId, string payload, DateTimeOffset occurredUtc)
    {
        EventName = UnitOfMeasure.NormalizeCode(eventName, 80);
        AggregateType = UnitOfMeasure.NormalizeCode(aggregateType, 80);
        AggregateId = aggregateId;
        Payload = UnitOfMeasure.Required(payload, nameof(payload), 8000);
        OccurredUtc = occurredUtc;
    }

    public string EventName { get; private set; } = string.Empty;

    public string AggregateType { get; private set; } = string.Empty;

    public Guid AggregateId { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredUtc { get; private set; }
}

public sealed class PluginRegistration : Entity
{
    private PluginRegistration()
    {
    }

    public PluginRegistration(string code, string name, string hookPoint, string description)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 40);
        Name = UnitOfMeasure.Required(name, nameof(name), 120);
        HookPoint = UnitOfMeasure.NormalizeCode(hookPoint, 80);
        Description = UnitOfMeasure.Required(description, nameof(description), 400);
        IsEnabled = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string HookPoint { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}

public sealed class ConfigTransport : Entity
{
    private ConfigTransport()
    {
    }

    public ConfigTransport(
        string code,
        string environment,
        string payloadJson,
        DateTimeOffset createdUtc)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 40);
        Environment = UnitOfMeasure.NormalizeCode(environment, 20);
        PayloadJson = UnitOfMeasure.Required(payloadJson, nameof(payloadJson), 16000);
        CreatedUtc = createdUtc;
    }

    public string Code { get; private set; } = string.Empty;

    public string Environment { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; private set; }
}
