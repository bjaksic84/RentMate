using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentMate.Models;

namespace RentMate.Data
{
    public class RentMateContext : IdentityDbContext<ApplicationUser>
    {
        public RentMateContext(DbContextOptions<RentMateContext> options)
            : base(options) { }

        public DbSet<Item> Items { get; set; }
        public DbSet<Rental> Rentals { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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

            // Item → Rentals
            modelBuilder.Entity<Item>()
                .HasMany(i => i.Rentals)
                .WithOne(r => r.Item)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // Item → Reviews relationship
            modelBuilder.Entity<Item>()
                .HasMany(i => i.Reviews)
                .WithOne(r => r.Item)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser → Reviews
            modelBuilder.Entity<ApplicationUser>()
                .HasMany<Review>()
                .WithOne(r => r.Reviewer)
                .HasForeignKey(r => r.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Rental entity
            modelBuilder.Entity<Rental>(entity =>
            {
                entity.Property(r => r.TotalPrice)
                      .HasColumnType("decimal(10,2)");

                entity.Property(r => r.Status)
                      .HasConversion<string>();
            });

            // Configure Payment entity
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

            // Review indexes for performance
            modelBuilder.Entity<Review>()
                .HasIndex(r => new { r.ItemId, r.IsDeleted });
            modelBuilder.Entity<Review>()
                .HasIndex(r => r.ReviewerId);

            modelBuilder.Entity<Review>()
                .Property(r => r.Rating)
                .HasDefaultValue(5);

            // --- PERFORMANCE INDEXES ---
            
            // Items: Fast filtering by Listing status, Category, Price and User
            modelBuilder.Entity<Item>()
                .HasIndex(i => new { i.IsListed, i.Category, i.Price });
            
            modelBuilder.Entity<Item>()
                .HasIndex(i => i.UserId);

            // Rentals: Fast lookup for specific statuses and parties
            modelBuilder.Entity<Rental>()
                .HasIndex(r => new { r.Status, r.StartDate, r.EndDate });

            modelBuilder.Entity<Rental>()
                .HasIndex(r => r.OwnerId);

            modelBuilder.Entity<Rental>()
                .HasIndex(r => r.RenterId);

            // User: Fast location search
            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.City);        }
    }
}