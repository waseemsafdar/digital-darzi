using Domain.Enums;

namespace Application.ViewModels.Notifications;

public class NotificationTemplateViewModel
{
    public NotificationEvent EventType { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    /// <summary>True when the shop has not customised this template.</summary>
    public bool IsDefault { get; set; }
}

public class UpsertNotificationTemplateViewModel
{
    public NotificationEvent EventType { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.WhatsApp;
    public string Body { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public class NotificationMessageViewModel
{
    public Guid Id { get; set; }
    public NotificationEvent EventType { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class SendOrderNotificationViewModel
{
    public NotificationEvent EventType { get; set; }
    /// <summary>Only for EventType = Custom.</summary>
    public string? CustomBody { get; set; }
}

public class TestNotificationViewModel
{
    public NotificationChannel Channel { get; set; } = NotificationChannel.WhatsApp;
    public string Recipient { get; set; } = string.Empty;
    public string Body { get; set; } = "Test message from Digital Darzi.";
}
