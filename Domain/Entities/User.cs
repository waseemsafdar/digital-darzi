namespace Domain.Entities;

public class User : BaseDBModel
{
    public Guid? AuthId { get; set; }          // → AspNetUsers.Id (email+pass users)
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Passcode { get; set; }      // BCrypt hashed PIN for phone+PIN login
    public List<Guid> RoleIds { get; set; } = new();   // → AspNetRoles.Id
    public List<Guid> ShopIds { get; set; } = new();   // shops this user can access
    public DateTime? LastLoginDate { get; set; }
    public int LoginCount { get; set; } = 0;
}
