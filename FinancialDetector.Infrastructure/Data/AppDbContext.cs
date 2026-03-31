using FinancialDetector.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FinancialDetector.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // GÜVENLİK: E-posta adreslerinin benzersiz (Unique) olmasını mühürlüyoruz.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // KRİTİK MİMARİ DÜZELTME: 'UserId1' hayalet kolonunun oluşmasını KÖKTEN engelliyoruz.
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.NoAction;
            }
        }
    }
}