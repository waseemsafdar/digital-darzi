using Application.Interfaces.Services;
using Application.ViewModels.Admin;
using Application.ViewModels.Customer;
using Application.ViewModels.Finance;
using Application.ViewModels.Measurement;
using Application.ViewModels.Notifications;
using Application.ViewModels.Operations;
using Application.ViewModels.Order;
using Application.ViewModels.Shop;
using Application.ViewModels.User;
using FluentValidation;

namespace Application.Validation;

// Request-shape rules only. Rules that need the database (does the customer exist, is the
// discount within the subtotal, is the transition legal) live in the services.

internal static class Rules
{
    public const string PhonePattern = @"^\+?[0-9][0-9\- ]{6,19}$";

    public static IRuleBuilderOptions<T, string> Phone<T>(this IRuleBuilder<T, string> rule)
        => rule.Matches(PhonePattern).WithMessage("{PropertyName} must be a phone number (digits, optional +, 7–20 characters).");

    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule)
        => rule.Matches(PhonePattern).WithMessage("{PropertyName} must be a phone number (digits, optional +, 7–20 characters).");

    public static IRuleBuilderOptions<T, decimal> Money<T>(this IRuleBuilder<T, decimal> rule)
        => rule.GreaterThanOrEqualTo(0).LessThan(100_000_000m);
}

// ── Orders ───────────────────────────────────────────────────────────────────

public class CreateOrderValidator : AbstractValidator<CreateOrderViewModel>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DeliveryDate).NotEmpty().WithMessage("Delivery date is required.");
        RuleFor(x => x.Discount).Money();
        RuleFor(x => x.AdvancePayment).Money();
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Items).NotEmpty().WithMessage("An order needs at least one item.");
        RuleFor(x => x.Items.Count).LessThanOrEqualTo(50).WithMessage("An order can have at most 50 items.");
        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemValidator());
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.SpecialInstructions).MaximumLength(2000);
    }
}

public class CreateOrderItemValidator : AbstractValidator<CreateOrderItemViewModel>
{
    public CreateOrderItemValidator()
    {
        RuleFor(x => x.GarmentType).IsInEnum();
        RuleFor(x => x.Qty).InclusiveBetween(1, 500);
        RuleFor(x => x.Price).Money();
        RuleFor(x => x.FabricCharge).Money();
        RuleFor(x => x.FabricSource).IsInEnum();
        RuleFor(x => x).Must(x => !(x.MeasurementProfileId.HasValue && (x.InlineMeasurements?.Count > 0 || x.InlineTextMeasurements?.Count > 0)))
            .WithMessage("Use either a saved measurement profile or inline measurements for an item, not both.");
        RuleForEach(x => x.InlineMeasurements).Must(kv => kv.Value is >= 0 and <= 1000)
            .WithMessage("Measurement values must be between 0 and 1000.");
        RuleForEach(x => x.StageAssignments).ChildRules(a =>
        {
            a.RuleFor(s => s.Stage).IsInEnum();
            a.RuleFor(s => s.StagePrice).GreaterThanOrEqualTo(0).When(s => s.StagePrice.HasValue);
            a.RuleFor(s => s.EstimatedDays).InclusiveBetween(0, 365);
        });
        RuleFor(x => x.StyleNotes).MaximumLength(2000);
    }
}

public class UpdateOrderValidator : AbstractValidator<UpdateOrderViewModel>
{
    public UpdateOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Discount!.Value).GreaterThanOrEqualTo(0).When(x => x.Discount.HasValue).WithName("Discount");
        RuleFor(x => x.Priority!.Value).IsInEnum().When(x => x.Priority.HasValue).WithName("Priority");
    }
}

public class ChangeOrderStatusValidator : AbstractValidator<ChangeOrderStatusViewModel>
{
    public ChangeOrderStatusValidator() => RuleFor(x => x.Status).IsInEnum();
}

public class RecordOrderPaymentValidator : AbstractValidator<RecordOrderPaymentViewModel>
{
    public RecordOrderPaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThan(100_000_000m);
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class RecordRefundValidator : AbstractValidator<RecordRefundViewModel>
{
    public RecordRefundValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThan(100_000_000m);
        RuleFor(x => x.PaymentMethod).IsInEnum();
    }
}

public class CancelOrderValidator : AbstractValidator<CancelOrderViewModel>
{
    public CancelOrderValidator()
    {
        RuleFor(x => x.RefundAmount).Money();
        RuleFor(x => x.RefundMethod).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public class CreateAlterationValidator : AbstractValidator<CreateOrderAlterationViewModel>
{
    public CreateAlterationValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.AdditionalCharge).Money();
    }
}

public class UpdateAlterationValidator : AbstractValidator<UpdateAlterationViewModel>
{
    public UpdateAlterationValidator() => RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status.HasValue).WithName("Status");
}

public class TrialResultValidator : AbstractValidator<TrialResultViewModel>
{
    public TrialResultValidator()
    {
        RuleFor(x => x.Outcome).IsInEnum();
        RuleForEach(x => x.Alterations).ChildRules(a =>
        {
            a.RuleFor(t => t.OrderItemId).NotEmpty();
            a.RuleFor(t => t.Description).NotEmpty().MaximumLength(1000);
            a.RuleFor(t => t.AdditionalCharge).GreaterThanOrEqualTo(0);
        });
    }
}

public class AssignStageValidator : AbstractValidator<AssignStageViewModel>
{
    public AssignStageValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.AssignedKarigarId).NotEmpty();
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.StagePrice!.Value).GreaterThanOrEqualTo(0).When(x => x.StagePrice.HasValue).WithName("StagePrice");
        RuleFor(x => x.EstimatedDays).InclusiveBetween(0, 365);
    }
}

public class RecordStageLogValidator : AbstractValidator<RecordStageLogViewModel>
{
    public RecordStageLogValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.Stage).IsInEnum();
    }
}

public class CompleteStageValidator : AbstractValidator<CompleteStageViewModel>
{
    public CompleteStageValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.FabricUsed!.Value).GreaterThanOrEqualTo(0).When(x => x.FabricUsed.HasValue).WithName("FabricUsed");
        RuleFor(x => x.CutQuantity!.Value).GreaterThanOrEqualTo(0).When(x => x.CutQuantity.HasValue).WithName("CutQuantity");
    }
}

public class RejectStageValidator : AbstractValidator<RejectStageViewModel>
{
    public RejectStageValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.DeductAmount!.Value).GreaterThan(0).When(x => x.DeductAmount.HasValue).WithName("DeductAmount");
    }
}

public class ReorderValidator : AbstractValidator<ReorderViewModel>
{
    public ReorderValidator() => RuleFor(x => x.DeliveryDate).NotEmpty();
}

// ── Customers & measurements ────────────────────────────────────────────────

public class CreateCustomerValidator : AbstractValidator<CreateCustomerViewModel>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Phone();
        RuleFor(x => x.WhatsAppNumber).OptionalPhone().When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber));
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.CustomerType).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class UpdateCustomerValidator : AbstractValidator<UpdateCustomerViewModel>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name != null);
        RuleFor(x => x.Phone).OptionalPhone().When(x => x.Phone != null);
        RuleFor(x => x.WhatsAppNumber).OptionalPhone().When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber));
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class SaveMeasurementProfileValidator : AbstractValidator<SaveMeasurementProfileViewModel>
{
    public SaveMeasurementProfileValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.ProfileName).MaximumLength(200);
        RuleForEach(x => x.TextValues).Must(kv => kv.Value == null || kv.Value.Length <= 500)
            .WithMessage("Text measurements are limited to 500 characters.");
    }
}

public class CreateMeasurementFieldValidator : AbstractValidator<CreateMeasurementFieldViewModel>
{
    public CreateMeasurementFieldValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).IsInEnum();
        RuleFor(x => x.ValueType).IsInEnum();
    }
}

public class CreateTemplateValidator : AbstractValidator<CreateTemplateViewModel>
{
    public CreateTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Fields).NotEmpty().WithMessage("A template needs at least one field.");
    }
}

public class SaveGarmentAttributeValidator : AbstractValidator<SaveGarmentAttributeViewModel>
{
    public SaveGarmentAttributeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GarmentType!.Value).IsInEnum().When(x => x.GarmentType.HasValue).WithName("GarmentType");
        RuleFor(x => x.Options.Count).LessThanOrEqualTo(50);
    }
}

// ── Finance ─────────────────────────────────────────────────────────────────

public class CreateExpenseValidator : AbstractValidator<CreateShopExpenseViewModel>
{
    public CreateExpenseValidator()
    {
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Amount).GreaterThan(0).LessThan(100_000_000m);
        RuleFor(x => x.ExpenseDate).NotEmpty();
        RuleFor(x => x.PaymentMethod).IsInEnum();
    }
}

public class UpdateExpenseValidator : AbstractValidator<UpdateShopExpenseViewModel>
{
    public UpdateExpenseValidator()
    {
        RuleFor(x => x.Amount!.Value).GreaterThan(0).When(x => x.Amount.HasValue).WithName("Amount");
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100).When(x => x.Category != null);
    }
}

public class RecordSalaryValidator : AbstractValidator<RecordStaffSalaryViewModel>
{
    public RecordSalaryValidator()
    {
        RuleFor(x => x.StaffUserId).NotEmpty();
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.BaseSalary).Money();
        RuleFor(x => x.Bonus).Money();
        RuleFor(x => x.Deduction).Money();
        RuleFor(x => x).Must(x => x.BaseSalary + x.Bonus - x.Deduction >= 0).WithMessage("Deductions cannot exceed salary plus bonus.");
        RuleFor(x => x.PaymentMethod).IsInEnum();
    }
}

public class RecordAttendanceValidator : AbstractValidator<RecordAttendanceViewModel>
{
    public RecordAttendanceValidator()
    {
        RuleFor(x => x.StaffUserId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x).Must(x => !(x.CheckIn.HasValue && x.CheckOut.HasValue) || x.CheckOut > x.CheckIn)
            .WithMessage("Check-out must be after check-in.");
    }
}

public class SaveSalaryRuleValidator : AbstractValidator<SaveSalaryRuleViewModel>
{
    public SaveSalaryRuleValidator()
    {
        RuleFor(x => x.StaffUserId).NotEmpty();
        RuleFor(x => x.MonthlySalary).Money();
        RuleFor(x => x.WorkingDaysPerMonth).InclusiveBetween(1, 31);
        RuleFor(x => x.PaidLeaveDays).InclusiveBetween(0, 31);
        RuleFor(x => x.HalfDayFactor).InclusiveBetween(0m, 1m);
    }
}

public class RecordSalaryAdvanceValidator : AbstractValidator<RecordSalaryAdvanceViewModel>
{
    public RecordSalaryAdvanceValidator()
    {
        RuleFor(x => x.StaffUserId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class RecordKarigarLedgerEntryValidator : AbstractValidator<RecordKarigarLedgerEntryViewModel>
{
    public RecordKarigarLedgerEntryValidator()
    {
        RuleFor(x => x.EntryType).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0).LessThan(100_000_000m);
    }
}

public class UpsertKarigarRateValidator : AbstractValidator<UpsertKarigarRateViewModel>
{
    public UpsertKarigarRateValidator()
    {
        RuleFor(x => x.GarmentType).IsInEnum();
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.Rate).Money();
    }
}

// ── Users, shops, platform ──────────────────────────────────────────────────

public class CreateUserValidator : AbstractValidator<CreateUserViewModel>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).OptionalPhone().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Passcode).Matches(@"^\d{4,6}$").WithMessage("PIN must be 4–6 digits.").When(x => !string.IsNullOrWhiteSpace(x.Passcode));
        RuleFor(x => x.Password).MinimumLength(8).When(x => !string.IsNullOrWhiteSpace(x.Password));
    }
}

public class UpdateUserValidator : AbstractValidator<UpdateUserViewModel>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name != null);
        RuleFor(x => x.Phone).OptionalPhone().When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}

public class CreateShopValidator : AbstractValidator<CreateShopViewModel>
{
    public CreateShopValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).OptionalPhone().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.WhatsAppNumber).OptionalPhone().When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber));
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(5);
    }
}

public class SavePlanValidator : AbstractValidator<SaveSubscriptionPlanViewModel>
{
    public SavePlanValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MonthlyPrice).Money();
        RuleFor(x => x.TrialDays).InclusiveBetween(0, 365);
        RuleFor(x => x.MaxBranches!.Value).GreaterThan(0).When(x => x.MaxBranches.HasValue).WithName("MaxBranches");
        RuleFor(x => x.MaxUsers!.Value).GreaterThan(0).When(x => x.MaxUsers.HasValue).WithName("MaxUsers");
        RuleFor(x => x.MaxOrdersPerMonth!.Value).GreaterThan(0).When(x => x.MaxOrdersPerMonth.HasValue).WithName("MaxOrdersPerMonth");
    }
}

public class UpsertNotificationTemplateValidator : AbstractValidator<UpsertNotificationTemplateViewModel>
{
    public UpsertNotificationTemplateValidator()
    {
        RuleFor(x => x.EventType).IsInEnum();
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(1000);
    }
}

public class SaveInventoryItemValidator : AbstractValidator<SaveInventoryItemViewModel>
{
    public SaveInventoryItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Unit).IsInEnum();
        RuleFor(x => x.PurchasePrice).Money();
        RuleFor(x => x.SellingPrice).Money();
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

public class CashMovementValidator : AbstractValidator<CashMovementRequestViewModel>
{
    public CashMovementValidator()
    {
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
        => RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Provide the email or phone number of the account.");
}

public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("The code is 6 digits.");
        RuleFor(x => x.NewPassword).MinimumLength(8).When(x => !string.IsNullOrWhiteSpace(x.NewPassword));
        RuleFor(x => x.NewPin).Matches(@"^\d{4,6}$").WithMessage("PIN must be 4–6 digits.").When(x => !string.IsNullOrWhiteSpace(x.NewPin));
    }
}
