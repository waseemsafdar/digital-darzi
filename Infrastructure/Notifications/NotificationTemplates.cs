using System.Globalization;
using System.Text.RegularExpressions;
using Domain.Enums;

namespace Infrastructure.Notifications;

/// <summary>Built-in message templates used until a shop customises its own.</summary>
public static class NotificationTemplates
{
    public static readonly IReadOnlyDictionary<NotificationEvent, (string Body, bool Enabled)> Defaults =
        new Dictionary<NotificationEvent, (string, bool)>
        {
            [NotificationEvent.OrderConfirmed] =
                ("Assalam-o-Alaikum {CustomerName}, your order {OrderNumber} at {ShopName} is confirmed. Total {Currency}{GrandTotal}, paid {Currency}{AmountPaid}. Delivery on {DeliveryDate}.", true),
            [NotificationEvent.OrderReady] =
                ("{CustomerName}, your order {OrderNumber} is ready for pickup at {ShopName}. Balance due: {Currency}{BalanceDue}.", true),
            [NotificationEvent.TrialReady] =
                ("{CustomerName}, your order {OrderNumber} is ready for a trial fitting at {ShopName}. Please visit at your convenience.", true),
            [NotificationEvent.DeliveryReminder] =
                ("Reminder: your order {OrderNumber} at {ShopName} is due on {DeliveryDate}.", true),
            [NotificationEvent.PaymentReminder] =
                ("{CustomerName}, a balance of {Currency}{BalanceDue} is outstanding on order {OrderNumber} at {ShopName}.", true),
            [NotificationEvent.AlterationReady] =
                ("{CustomerName}, the alteration on order {OrderNumber} is ready for pickup at {ShopName}.", true),
            [NotificationEvent.PaymentReceived] =
                ("Received {Currency}{PaymentAmount} for order {OrderNumber}. Remaining balance {Currency}{BalanceDue}. Thank you, {ShopName}.", false),
            [NotificationEvent.OrderOverdue] =
                ("{CustomerName}, we apologise: order {OrderNumber} is running late. {ShopName} will contact you with a new date.", false),
            [NotificationEvent.PasswordReset] =
                ("Your Digital Darzi reset code is {Code}. It expires in 15 minutes. Do not share it.", true),
        };

    private static readonly Regex Placeholder = new(@"\{(\w+)\}", RegexOptions.Compiled);

    /// <summary>Replaces {Name} placeholders. Unknown placeholders are left as-is so typos are visible.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string?> values)
        => Placeholder.Replace(template, m =>
            values.TryGetValue(m.Groups[1].Value, out var v) ? v ?? string.Empty : m.Value);

    public static string Money(decimal amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

    public static string Date(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}
