namespace Application.Common;

/// <summary>
/// Centralised role name constants — avoids magic strings scattered across controllers and services.
/// </summary>
public static class AppRoles
{
    public const string SystemAdmin  = "SystemAdmin";
    public const string Owner        = "Owner";
    public const string Manager      = "Manager";
    public const string Karigar      = "Karigar";
    public const string Receptionist = "Receptionist";
}
