using Application.Common;
using Domain.Enums;

namespace Application.ViewModels.User;

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
