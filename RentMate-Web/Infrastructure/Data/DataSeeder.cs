using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using RentMate.Models.Domain;

namespace RentMate.Infrastructure.Data;

/// <summary>
/// Handles initial data seeding for roles and admin user.
/// </summary>
public static class DataSeeder
{
    #region Constants

    /// <summary>
    /// Available system roles.
    /// </summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string User = "User";
        public const string Moderator = "Moderator";

        public static readonly string[] All = [Admin, User, Moderator];
    }

    private const string DefaultAdminEmail = "admin@rentmate.com";
    private const string DefaultAdminFirstName = "System";
    private const string DefaultAdminLastName = "Admin";
    private const string DefaultAdminCity = "Ljubljana";

    #endregion

    #region Public Methods

    /// <summary>
    /// Seeds system roles and creates the admin user if configured.
    /// Admin password must be set via User Secrets or environment variables for security.
    /// </summary>
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, configuration);
    }

    #endregion

    #region Private Helpers

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var adminEmail = configuration["AdminUser:Email"] ?? DefaultAdminEmail;
        var adminPassword = configuration["AdminUser:Password"];

        // Skip admin seeding if no password configured (production safety)
        if (string.IsNullOrEmpty(adminPassword))
            return;

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin != null)
            return;

        var adminUser = CreateAdminUser(adminEmail);
        var result = await userManager.CreateAsync(adminUser, adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        }
    }

    private static ApplicationUser CreateAdminUser(string email) => new()
    {
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        FirstName = DefaultAdminFirstName,
        LastName = DefaultAdminLastName,
        City = DefaultAdminCity
    };

    #endregion
}

