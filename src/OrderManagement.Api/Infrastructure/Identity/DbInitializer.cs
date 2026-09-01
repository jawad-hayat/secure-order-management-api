using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrderManagement.Api.Infrastructure.Identity
{
    public static class DbInitializer
    {
        /// <summary>
        /// Seeds identity roles (Admin, Customer) and optionally seeds a development Admin user
        /// if 'SeedAdmin:Password' is provided via local User Secrets.
        /// </summary>
        public static async Task SeedIdentityAsync(
            IServiceProvider services,
            IConfiguration configuration,
            IWebHostEnvironment env,
            ILogger logger)
        {
            using var scope = services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Seed standard roles safely
            string[] standardRoles = { "Admin", "Customer" };
            foreach (var roleName in standardRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                    if (result.Succeeded)
                    {
                        logger.LogInformation("Seeded role '{RoleName}'.", roleName);
                    }
                    else
                    {
                        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                        logger.LogWarning("Failed to seed role '{RoleName}': {Errors}", roleName, errors);
                    }
                }
            }

            // 2. In Development only: Seed a single development Admin user from User Secrets (if configured)
            if (env.IsDevelopment())
            {
                var adminUserName = configuration["SeedAdmin:UserName"] ?? "admin";
                var adminEmail = configuration["SeedAdmin:Email"] ?? "admin@example.com";
                var adminPassword = configuration["SeedAdmin:Password"];

                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    logger.LogInformation(
                        "No development Admin password configured in User Secrets ('SeedAdmin:Password'). " +
                        "To seed a local Admin account, run: dotnet user-secrets set \"SeedAdmin:Password\" \"<YourStrongPassword>\"");
                    return;
                }

                var existingAdmin = await userManager.FindByNameAsync(adminUserName);
                if (existingAdmin == null)
                {
                    var adminUser = new ApplicationUser
                    {
                        UserName = adminUserName,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                        logger.LogInformation("Created development Admin user '{UserName}' with 'Admin' role.", adminUserName);
                    }
                    else
                    {
                        var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                        logger.LogWarning("Failed to create development Admin user '{UserName}': {Errors}", adminUserName, errors);
                    }
                }
                else
                {
                    if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
                    {
                        await userManager.AddToRoleAsync(existingAdmin, "Admin");
                        logger.LogInformation("Assigned 'Admin' role to existing development user '{UserName}'.", adminUserName);
                    }
                }
            }
        }
    }
}
