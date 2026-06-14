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
    Done       = 3
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
    Order = 1,
    // future types can be added here, e.g., Customer, Product, etc.
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
