using System.Linq;
using FinancialDetector.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinancialDetector.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

            // MİMARİ DÜZELTME: EF Core'un 'UserId1' gibi gizli (Shadow) kolonlar yaratmasını,
            // ilişkileri açıkça belirterek ve Cascade silmeyi kapatarak engelliyoruz.
            modelBuilder.Entity<Transaction>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Subscription>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}