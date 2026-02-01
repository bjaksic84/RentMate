using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentMate.Models;

namespace RentMate.Data;

/// <summary>
/// Entity Framework database context for the RentMate application.
/// Extends IdentityDbContext for ASP.NET Identity support.
/// </summary>
public class RentMateContext : IdentityDbContext<ApplicationUser>
{
    #region Constructor

    public RentMateContext(DbContextOptions<RentMateContext> options)
        : base(options) { }

    #endregion

    #region DbSets

    public DbSet<Item> Items { get; set; }
    public DbSet<Rental> Rentals { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<AccountItemFavorite> AccountItemFavorites { get; set; }

    #endregion

    #region Model Configuration

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUserRelationships(modelBuilder);
        ConfigureItemRelationships(modelBuilder);
        ConfigureRentalEntity(modelBuilder);
        ConfigurePaymentEntity(modelBuilder);
        ConfigureReviewEntity(modelBuilder);
        ConfigureFavoritesEntity(modelBuilder);
        ConfigurePerformanceIndexes(modelBuilder);
    }

    #endregion

    #region Entity Configurations

    private static void ConfigureUserRelationships(ModelBuilder modelBuilder)
    {
        // ApplicationUser → Items (ownership)
        modelBuilder.Entity<ApplicationUser>()
            .HasMany(u => u.Items)
            .WithOne(i => i.User)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ApplicationUser → Rentals as renter
        modelBuilder.Entity<ApplicationUser>()
            .HasMany(u => u.RentalsAsRenter)
            .WithOne(r => r.Renter)
            .HasForeignKey(r => r.RenterId)
            .OnDelete(DeleteBehavior.Restrict);

        // ApplicationUser → Rentals as owner
        modelBuilder.Entity<ApplicationUser>()
            .HasMany(u => u.RentalsAsOwner)
            .WithOne(r => r.Owner)
            .HasForeignKey(r => r.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ApplicationUser → Reviews
        modelBuilder.Entity<ApplicationUser>()
            .HasMany<Review>()
            .WithOne(r => r.Reviewer)
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        // User location index
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.City);
    }

    private static void ConfigureItemRelationships(ModelBuilder modelBuilder)
    {
        // Item → Rentals
        modelBuilder.Entity<Item>()
            .HasMany(i => i.Rentals)
            .WithOne(r => r.Item)
            .HasForeignKey(r => r.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Item → Reviews
        modelBuilder.Entity<Item>()
            .HasMany(i => i.Reviews)
            .WithOne(r => r.Item)
            .HasForeignKey(r => r.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureRentalEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rental>(entity =>
        {
            entity.Property(r => r.TotalPrice)
                  .HasColumnType("decimal(10,2)");

            entity.Property(r => r.Status)
                  .HasConversion<string>();
        });
    }

    private static void ConfigurePaymentEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(p => p.Amount)
                  .HasColumnType("decimal(18,2)");

            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Rental)
                  .WithMany()
                  .HasForeignKey(p => p.RentalId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureReviewEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Review>(entity =>
        {
            entity.Property(r => r.Rating)
                  .HasDefaultValue(5);

            // Performance indexes
            entity.HasIndex(r => new { r.ItemId, r.IsDeleted });
            entity.HasIndex(r => r.ReviewerId);
        });
    }

    private static void ConfigureFavoritesEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountItemFavorite>(entity =>
        {
            // Composite primary key
            entity.HasKey(f => new { f.AccountId, f.ItemId });

            // AccountItemFavorite → ApplicationUser
            entity.HasOne(f => f.Account)
                  .WithMany(u => u.Favorites)
                  .HasForeignKey(f => f.AccountId)
                  .OnDelete(DeleteBehavior.Cascade);

            // AccountItemFavorite → Item
            entity.HasOne(f => f.Item)
                  .WithMany(i => i.FavoritedBy)
                  .HasForeignKey(f => f.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Index for efficient queries by ItemId
            entity.HasIndex(f => f.ItemId);
        });
    }

    private static void ConfigurePerformanceIndexes(ModelBuilder modelBuilder)
    {
        // Items: Fast filtering by listing status, category, price
        modelBuilder.Entity<Item>()
            .HasIndex(i => new { i.IsListed, i.Category, i.Price });

        modelBuilder.Entity<Item>()
            .HasIndex(i => i.UserId);

        // Rentals: Fast lookup for status and date ranges
        modelBuilder.Entity<Rental>()
            .HasIndex(r => new { r.Status, r.StartDate, r.EndDate });

        modelBuilder.Entity<Rental>()
            .HasIndex(r => r.OwnerId);

        modelBuilder.Entity<Rental>()
            .HasIndex(r => r.RenterId);
    }

    #endregion
}