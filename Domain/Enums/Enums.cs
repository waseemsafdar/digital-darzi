namespace Domain.Enums;

public enum ActiveStatus
{
    Inactive = 0,
    Active   = 1
}

public enum MeasurementUnit
{
    Inch = 1,
    CM   = 2,
    MM   = 3
}

public enum Gender
{
    Male   = 1,
    Female = 2,
    Other  = 3
}

public enum GarmentType
{
    ShalwarKameez = 1,
    Suit          = 2,
    Sherwani      = 3,
    Coat          = 4,
    Trouser       = 5,
    Shirt         = 6,
    Frock         = 7,
    Lehnga        = 8,
    Kurta         = 9,
    Waistcoat     = 10,
    Other         = 99
}

public enum OrderStatus
{
    Pending            = 0,
    Received           = 1,
    Cutting            = 2,
    Stitching          = 3,
    Embroidery         = 4,
    Finishing          = 5,
    TrialReady         = 6,
    TrialDone          = 7,
    ReadyToDeliver     = 8,
    PartiallyDelivered = 9,
    Delivered          = 10,
    Cancelled          = 11,

    /// <summary>
    /// LEGACY. Earlier versions overwrote the production status with this value when an order was
    /// paid in full. Payment state now lives in <see cref="PaymentStatus"/>; the API never sets this
    /// value any more, and an order still carrying it may be moved to any non-legacy status.
    /// </summary>
    FullyPaid          = 12
}

public enum OrderPriority
{
    Normal = 1,
    Rush   = 2,
    VIP    = 3
}

public enum PaymentMethod
{
    Cash         = 1,
    JazzCash     = 2,
    EasyPaisa    = 3,
    BankTransfer = 4,
    Other        = 5
}

public enum FabricStatus
{
    Sufficient = 1,
    Short      = 2,
    Excess     = 3
}

public enum CancellationStage
{
    BeforeCutting  = 1,
    AfterCutting   = 2,
    AfterStitching = 3
}

public enum RefundStatus
{
    Pending   = 1,
    Processed = 2,
    Waived    = 3
}

public enum AlterationStatus
{
    Received   = 1,
    InProgress = 2,
    Done       = 3,
    Delivered  = 4,
    Cancelled  = 5
}

public enum StageAssignmentMode
{
    PrePlanned = 1,
    AdHoc      = 2
}

public enum ExpenseCategory
{
    Rent      = 1,
    Salary    = 2,
    Utilities = 3,
    Materials = 4,
    Equipment = 5,
    Other     = 9
}

public enum SalaryStatus
{
    Pending       = 1,
    PartiallyPaid = 2,
    Paid          = 3
}

public enum AttachmentType
{
    Order     = 1,
    Expense   = 2,
    Customer  = 3,
    OrderItem = 4
}

public enum ProductionStage
{
    Cutting    = 1,
    Stitching  = 2,
    Embroidery = 3,
    Ironing    = 4,
    Finishing  = 5,
    QualityCheck = 6
}

public enum OrderItemStatus
{
    Pending    = 0,
    InProgress = 1,
    Done       = 2,
    Delivered  = 3,
    Cancelled  = 4
}

public enum AttendanceStatus
{
    Present  = 1,
    Absent   = 2,
    HalfDay  = 3,
    Leave    = 4,
    Holiday  = 5
}

public enum SubscriptionStatus
{
    Trial = 1,
    Active = 2,
    Suspended = 3,
    Cancelled = 4
}

public enum PaymentStatus
{
    Unpaid   = 0,
    Partial  = 1,
    Paid     = 2,
    Refunded = 3
}

public enum StageLogStatus
{
    InProgress     = 0,
    Completed      = 1,
    ReworkRequired = 2
}

public enum CustomerType
{
    Regular   = 1,
    VIP       = 2,
    Corporate = 3,
    WalkIn    = 4
}

public enum FabricSource
{
    CustomerProvided  = 1,
    ShopProvided      = 2,
    PurchasedForOrder = 3
}

public enum KarigarLedgerEntryType
{
    Earning    = 1,   // +  completed stage work
    Advance    = 2,   // -  money handed over before it was earned
    Deduction  = 3,   // -  penalty / rework / damaged fabric
    Payment    = 4,   // -  payout settling the balance
    Adjustment = 5    // +/- manual correction
}

public enum TrialOutcome
{
    Approved           = 1,
    AlterationRequired = 2
}

public enum MeasurementValueType
{
    Number = 1,
    Text   = 2
}

public enum NotificationChannel
{
    WhatsApp = 1,
    Sms      = 2,
    Email    = 3,
    InApp    = 4
}

public enum NotificationEvent
{
    OrderConfirmed   = 1,
    OrderReady       = 2,
    TrialReady       = 3,
    DeliveryReminder = 4,
    PaymentReminder  = 5,
    AlterationReady  = 6,
    PaymentReceived  = 7,
    OrderOverdue     = 8,
    PasswordReset    = 9,
    Custom           = 99
}

public enum NotificationStatus
{
    Pending = 0,
    Sending = 1,
    Sent    = 2,
    Failed  = 3,
    Skipped = 4
}

public enum InventoryCategory
{
    Fabric    = 1,
    Button    = 2,
    Thread    = 3,
    Zipper    = 4,
    Lining    = 5,
    Accessory = 6,
    Packaging = 7,
    Other     = 99
}

public enum InventoryUnit
{
    Meter = 1,
    Yard  = 2,
    Piece = 3,
    Roll  = 4,
    Spool = 5,
    Kg    = 6,
    Pack  = 7
}

public enum InventoryMovementType
{
    Purchase        = 1,   // +
    IssueToOrder    = 2,   // -
    ReturnFromOrder = 3,   // +
    Adjustment      = 4,   // +/-
    Waste           = 5,   // -
    TransferOut     = 6,   // -
    TransferIn      = 7,   // +
    SupplierReturn  = 8    // -
}

public enum CashSessionStatus
{
    Open   = 1,
    Closed = 2
}

public enum CashMovementDirection
{
    In  = 1,
    Out = 2
}

public enum InvoiceStatus
{
    Pending = 1,
    Paid    = 2,
    Void    = 3
}
