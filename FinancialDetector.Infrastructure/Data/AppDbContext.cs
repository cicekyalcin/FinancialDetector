using FinancialDetector.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;

namespace FinancialDetector.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly Guid _currentUserId;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // API katmanında token üzerinden inject edilecek, şimdilik varsayılan boş.
            _currentUserId = Guid.Empty;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Transaction>().ToTable("Transactions");
            modelBuilder.Entity<Subscription>().ToTable("Subscriptions");

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Subscription>()
                .Property(s => s.LastDetectedAmount)
                .HasColumnType("decimal(18,2)");

            // Multi-Tenancy (Çok Kiracılı) Veri İzolasyonu Global Filtresi
            if (_currentUserId != Guid.Empty)
            {
                modelBuilder.Entity<Transaction>().HasQueryFilter(t => t.UserId == _currentUserId);
                modelBuilder.Entity<Subscription>().HasQueryFilter(s => s.UserId == _currentUserId);
            }
        }
    }
}