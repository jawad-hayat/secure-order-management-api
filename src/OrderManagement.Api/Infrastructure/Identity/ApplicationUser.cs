using System;
using Microsoft.AspNetCore.Identity;

namespace OrderManagement.Api.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        // Add application-specific profile fields here if needed in future.
        // Keep minimal now: IdentityUser already contains Email, NormalizedEmail,
        // UserName, NormalizedUserName, PasswordHash, SecurityStamp, Lockout data, etc.
    }
}
