using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentMate.Models;
using RentMate.Shared;

namespace RentMate.Data
{
    public class RentMateContext : IdentityDbContext<ApplicationUser>
    {
        public RentMateContext(DbContextOptions<RentMateContext> options)
            : base(options) { }

        public DbSet<RentMate.Models.Item> Items { get; set; }
        public DbSet<RentMate.Models.Rental> Rentals { get; set; }
        public DbSet<RentMate.Models.Review> Reviews { get; set; }

        public DbSet<RentMate.Models.Payment> Payments { get; set; } // NOVO

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
            modelBuilder.Entity<RentMate.Models.Item>()
                .HasMany(i => i.Rentals)
                .WithOne(r => r.Item)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Cascade); // Delete item => delete associated rentals

            // Item → Reviews relationship
            modelBuilder.Entity<RentMate.Models.Item>()
                .HasMany(i => i.Reviews)
                .WithOne(r => r.Item)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser → Reviews
            modelBuilder.Entity<ApplicationUser>()
                .HasMany<RentMate.Models.Review>()
                .WithOne(r => r.Reviewer)
                .HasForeignKey(r => r.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔹 Configure Rental entity
            modelBuilder.Entity<RentMate.Models.Rental>(entity =>
            {
                entity.Property(r => r.TotalPrice)
                      .HasColumnType("decimal(10,2)");

                entity.Property(r => r.Status)
                      .HasConversion<string>(); // store enum as string for readability
            });

            // 🔹 Konfiguracija za Payment
            modelBuilder.Entity<RentMate.Models.Payment>(entity =>
            {
                entity.Property(p => p.Amount)
                      .HasColumnType("decimal(18,2)"); // Nujno za denarne vrednosti

                // Povezava Payment -> User
                entity.HasOne(p => p.User)
                      .WithMany()
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Povezava Payment -> Rental
                entity.HasOne(p => p.Rental)
                      .WithMany()
                      .HasForeignKey(p => p.RentalId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            // Review indexes for performance
            modelBuilder.Entity<RentMate.Models.Review>()
                .HasIndex(r => new { r.ItemId, r.IsDeleted });
            modelBuilder.Entity<RentMate.Models.Review>()
                .HasIndex(r => r.ReviewerId);

            modelBuilder.Entity<RentMate.Models.Review>()
                .Property(r => r.Rating)
                .HasDefaultValue(5);

        }
    }
}

