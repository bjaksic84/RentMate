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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 🔹 ApplicationUser → Items (ownership)
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Items)
                .WithOne(i => i.User)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Delete user => delete their items

            // 🔹 ApplicationUser → Rentals as renter
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.RentalsAsRenter)
                .WithOne(r => r.Renter)
                .HasForeignKey(r => r.RenterId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete (preserve rental history)

            // 🔹 ApplicationUser → Rentals as owner
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.RentalsAsOwner)
                .WithOne(r => r.Owner)
                .HasForeignKey(r => r.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔹 Item → Rentals
            modelBuilder.Entity<Item>()
                .HasMany(i => i.Rentals)
                .WithOne(r => r.Item)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Cascade); // Delete item => delete associated rentals

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

            // 🔹 Configure Rental entity
            modelBuilder.Entity<Rental>(entity =>
            {
                entity.Property(r => r.TotalPrice)
                      .HasColumnType("decimal(10,2)");

                entity.Property(r => r.Status)
                      .HasConversion<string>(); // store enum as string for readability
            });
            // Review indexes for performance
            modelBuilder.Entity<Review>()
                .HasIndex(r => new { r.ItemId, r.IsDeleted });
            modelBuilder.Entity<Review>()
                .HasIndex(r => r.ReviewerId);

            modelBuilder.Entity<Review>()
                .Property(r => r.Rating)
                .HasDefaultValue(5);

        }
    }
}

