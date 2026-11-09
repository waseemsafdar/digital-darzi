using Application.Common;
using Domain.Enums;

namespace Application.ViewModels.User;

// ── Role ──────────────────────────────────────────────────────────────────────
public class RoleViewModel
{
    public Guid   Id   { get; set; }
    /// <summary>Sign-in role name: a built-in name, or "c_…" for the business's own role.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>What the shop calls it.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>The starter name of a built-in role; null for the business's own roles.</summary>
    public string? DefaultName { get; set; }
    public string? Description { get; set; }
    public bool IsBuiltIn { get; set; }
    /// <summary>The Owner role: shown, never editable.</summary>
    public bool IsLocked { get; set; }
    public bool IsHidden { get; set; }
    /// <summary>"karigar" when holders use the karigar app (production work, no order list), else "shop".</summary>
    public string Area { get; set; } = "shop";
    public List<string> Permissions { get; set; } = new();
    /// <summary>Active staff holding this role in the business.</summary>
    public int StaffCount { get; set; }
}

public class SaveRoleViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>The complete list of permissions the role should have.</summary>
    public List<string> Permissions { get; set; } = new();
    public bool IsHidden { get; set; }
}

public class PermissionInfoViewModel
{
    public string Key { get; set; } = string.Empty;
    /// <summary>False for Owner-only permissions, which no other role can hold.</summary>
    public bool Delegable { get; set; }
}

public class UserListViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public List<string> Roles { get; set; } = new();
    public ActiveStatus ActiveStatus { get; set; }
    public DateTime? LastLoginDate { get; set; }
}

public class UserDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public List<Guid> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();
    public List<Guid> ShopIds { get; set; } = new();
    public ActiveStatus ActiveStatus { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public int LoginCount { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class CreateUserViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Password { get; set; }       // for email+password users
    public string? Phone { get; set; }
    public string? Passcode { get; set; }        // 4-digit PIN for phone+PIN users
    public List<Guid> RoleIds { get; set; } = new();
    public List<Guid> ShopIds { get; set; } = new();
}

public class UpdateUserViewModel : IHasId
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public List<Guid>? RoleIds { get; set; }
    public List<Guid>? ShopIds { get; set; }
    public ActiveStatus? ActiveStatus { get; set; }
}

/// <summary>Owner sets a staff member's sign-in PIN (POST /api/users/{id}/reset-pin).</summary>
public class ResetPinViewModel
{
    public string NewPin { get; set; } = string.Empty;
}
