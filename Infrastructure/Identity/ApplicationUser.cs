using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    // Minimal identity user — authentication only.
    // Business profile is in Domain.Entities.User linked via AuthId.
}
