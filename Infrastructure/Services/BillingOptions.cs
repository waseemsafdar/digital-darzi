namespace Infrastructure.Services;

/// <summary>
/// "Billing" configuration: how new shops start, the billing periods offered, and where Owners pay.
/// Payment accounts are empty by default; set them in appsettings or as environment variables, e.g.
/// Billing__PaymentAccounts__0__Method=JazzCash, Billing__PaymentAccounts__0__AccountTitle=..., Billing__PaymentAccounts__0__AccountNumber=...
/// </summary>
public sealed class BillingOptions
{
    public const string Section = "Billing";

    /// <summary>Lets anyone create a shop from the sign-up screen.</summary>
    public bool SignupEnabled { get; set; } = true;

    /// <summary>The plan a new shop tries during its trial. Missing plan = trial without limits.</summary>
    public string TrialPlanName { get; set; } = "Growth";

    /// <summary>Trial length when the trial plan has no TrialDays set.</summary>
    public int DefaultTrialDays { get; set; } = 14;

    public List<BillingPeriodOption> Periods { get; set; } = new()
    {
        new() { Months = 1, DiscountPercent = 0 },
        new() { Months = 6, DiscountPercent = 10 },
        new() { Months = 12, DiscountPercent = 20 }
    };

    public List<PaymentAccountOption> PaymentAccounts { get; set; } = new();

    public string? Instructions { get; set; } =
        "Send the amount to one of these accounts, then add the transaction ID and a photo of the receipt. Your plan starts once we check it, usually within a day.";
}

public sealed class BillingPeriodOption
{
    public int Months { get; set; }
    public int DiscountPercent { get; set; }
}

public sealed class PaymentAccountOption
{
    public string Method { get; set; } = string.Empty;
    public string AccountTitle { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? Details { get; set; }
}
