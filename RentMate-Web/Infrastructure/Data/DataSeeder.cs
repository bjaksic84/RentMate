using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

    // Hardcoded test data for the RIS seminar "Oddaja zahteve za rezervacijo" use case.
    private const string TestOwnerEmail = "rentmate.owner@gmail.com";
    private const string TestRenterEmail = "rentmate.renter@gmail.com";
    private const string TestUserPassword = "Test123!";
    private const string TestItemTitle = "Vrtalni stroj Bosch";

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
        var context = services.GetRequiredService<RentMateContext>();

        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, configuration);
        await SeedTestDataAsync(userManager, context);
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

    /// <summary>
    /// Seeds hardcoded test data so the reservation use case is exercisable end-to-end:
    /// an item owner, a renter, one listed item, and two accessories. Idempotent.
    /// </summary>
    private static async Task SeedTestDataAsync(
        UserManager<ApplicationUser> userManager,
        RentMateContext context)
    {
        var owner = await EnsureUserAsync(userManager, TestOwnerEmail, "Janez", "Najemodajalec");
        await EnsureUserAsync(userManager, TestRenterEmail, "Maja", "Najemnica");

        if (owner == null)
            return;

        if (await context.Items.AnyAsync(i => i.Title == TestItemTitle))
            return;

        var item = new Item
        {
            Title = TestItemTitle,
            Description = "Vrtalni stroj za domačo uporabo. Vključuje kovček in osnovni komplet svedrov.",
            Category = "Orodje",
            Price = 15.00m,
            Location = DefaultAdminCity,
            UserId = owner.Id,
            IsListed = true,
            HasAvailability = true,
            CreatedAt = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };

        context.Items.Add(item);
        await context.SaveChangesAsync();

        context.ItemAccessories.AddRange(
            new ItemAccessory { ItemId = item.Id, Name = "Komplet svedrov", DailyPrice = 3.00m, IsAvailable = true },
            new ItemAccessory { ItemId = item.Id, Name = "Dodatna baterija", DailyPrice = 2.00m, IsAvailable = true });
        await context.SaveChangesAsync();
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string firstName,
        string lastName)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
            return existing;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            City = DefaultAdminCity
        };

        var result = await userManager.CreateAsync(user, TestUserPassword);
        if (!result.Succeeded)
            return null;

        await userManager.AddToRoleAsync(user, Roles.User);
        return user;
    }

    #endregion
}

