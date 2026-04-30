using Microsoft.AspNetCore.Identity;

namespace ThemeManagement.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
