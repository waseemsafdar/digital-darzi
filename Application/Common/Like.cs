namespace Application.Common;

/// <summary>Escapes user text for a Postgres LIKE/ILIKE pattern using '\' as the escape character.</summary>
public static class Like
{
    public const string EscapeChar = "\\";

    public static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>"%text%" with the text escaped.</summary>
    public static string Contains(string value) => "%" + Escape(value.Trim()) + "%";
}
