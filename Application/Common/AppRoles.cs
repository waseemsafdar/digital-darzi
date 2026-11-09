namespace Application.Common;

/// <summary>
/// Sign-in role names. The built-in ones exist on every platform; a shop's own roles are created
/// at runtime with a name starting with <see cref="CustomPrefix"/>, and their permissions live
/// only in that shop's permission rows.
/// </summary>
public static class AppRoles
{
    public const string SystemAdmin  = "SystemAdmin";
    public const string Owner        = "Owner";
    public const string Manager      = "Manager";
    public const string Karigar      = "Karigar";
    public const string Receptionist = "Receptionist";

    // Roles found in Pakistani tailor shops (2026-11).
    public const string Master       = "Master";       // head tailor: measures, cuts, assigns, checks work
    public const string Cutter       = "Cutter";
    public const string KajButton    = "KajButton";    // buttonholes and buttons
    public const string Embroiderer  = "Embroiderer";  // kadhai, aari, zardozi
    public const string Presser      = "Presser";      // istri wala
    public const string Helper       = "Helper";       // shagird / apprentice
    public const string Salesman     = "Salesman";     // counter: orders, payments, hand-over
    public const string Munshi       = "Munshi";       // accounts, karigar pay, cash
    public const string Delivery     = "Delivery";     // pick-up and drop-off

    /// <summary>Prefix of a shop's own role names, e.g. "c_3f2a…". Never used for built-in roles.</summary>
    public const string CustomPrefix = "c_";

    public static bool IsCustom(string role) => role.StartsWith(CustomPrefix, StringComparison.Ordinal);

    /// <summary>Built-in roles a shop can assign, in display order, with their default name and summary.</summary>
    public static readonly IReadOnlyList<(string Name, string DisplayName, string Description)> BuiltIn = new[]
    {
        (Owner,        "Owner",                "Runs the shop. Can do everything."),
        (Manager,      "Manager",              "Runs the shop day to day: orders, production, money and setup. Cannot change PINs or remove staff."),
        (Master,       "Master (head tailor)", "Measures, cuts, assigns work to karigars and checks finished work. No money or staff."),
        (Salesman,     "Counter / salesman",   "Takes orders and measurements, collects payments and hands orders over."),
        (Receptionist, "Receptionist",         "Front desk: payments, hand-over, customers and measurements. Cannot create orders."),
        (Munshi,       "Munshi (accounts)",    "Keeps the accounts: expenses, salaries, karigar pay, cash and reports."),
        (Delivery,     "Delivery",             "Sees ready orders, hands them over and collects the balance."),
        (Karigar,      "Karigar",              "Stitches garments. Uses the karigar app."),
        (Cutter,       "Cutter",               "Cuts fabric from the measurements. Uses the karigar app."),
        (KajButton,    "Kaj-button worker",    "Buttonholes and buttons. Uses the karigar app."),
        (Embroiderer,  "Embroidery worker",    "Hand or machine embroidery. Uses the karigar app."),
        (Presser,      "Presser (istri)",      "Ironing and final pressing. Uses the karigar app."),
        (Helper,       "Helper (shagird)",     "Small jobs while learning. Uses the karigar app."),
    };

    public static bool IsBuiltIn(string role) => BuiltIn.Any(b => b.Name == role);
}
