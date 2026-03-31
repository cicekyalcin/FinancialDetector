using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinancialDetector.Domain.Entities;
using FinancialDetector.Domain.Interfaces;
using FinancialDetector.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinancialDetector.Infrastructure.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly AppDbContext _context;

        public TransactionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<Transaction> Data, int TotalCount)> GetTransactionsAsync(Guid userId, int pageNumber, int pageSize, DateTime? startDate, DateTime? endDate, int? month, string merchantName)
        {
            var query = _context.Transactions.Where(t => t.UserId == userId).AsQueryable();

            if (startDate.HasValue) query = query.Where(t => t.TransactionDate >= startDate.Value);
            if (endDate.HasValue) query = query.Where(t => t.TransactionDate <= endDate.Value);
            if (month.HasValue && month.Value >= 1 && month.Value <= 12) query = query.Where(t => t.TransactionDate.Month == month.Value);
            if (!string.IsNullOrWhiteSpace(merchantName)) query = query.Where(t => t.NormalizedMerchantName.Contains(merchantName.ToUpper()));

            query = query.OrderByDescending(t => t.TransactionDate);

            var totalCount = await query.CountAsync();
            var data = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            return (data, totalCount);
        }

        public async Task AddTransactionsAsync(IEnumerable<Transaction> transactions)
        {
            await _context.Transactions.AddRangeAsync(transactions);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UserExistsAsync(Guid userId)
        {
            return await _context.Users.AnyAsync(u => u.Id == userId);
        }

        // Dashboard İstatistiklerini Hesaplayan Fonksiyon
        public async Task<object> GetDashboardStatsAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            var currentMonth = now.Month;
            var currentYear = now.Year;

            var userTransactions = _context.Transactions.Where(t => t.UserId == userId);

            // 1. Bu ayki toplam harcama
            var monthlyTotal = await userTransactions
                .Where(t => t.TransactionDate.Month == currentMonth && t.TransactionDate.Year == currentYear)
                .SumAsync(t => t.Amount);

            // 2. En çok harcama yapılan kurum (Top Merchant)
            var topMerchant = await userTransactions
                .GroupBy(t => t.RawMerchantName)
                .OrderByDescending(g => g.Sum(t => t.Amount))
                .Select(g => new { Name = g.Key, Total = g.Sum(t => t.Amount) })
                .FirstOrDefaultAsync();

            // 3. Toplam işlem sayısı
            var totalCount = await userTransactions.CountAsync();

            // 4. Son 5 işlem (Quick View)
            var lastTransactions = await userTransactions
                .OrderByDescending(t => t.TransactionDate)
                .Take(5)
                .Select(t => new { t.TransactionDate, t.RawMerchantName, t.Amount })
                .ToListAsync();

            return new
            {
                MonthlyTotal = monthlyTotal,
                TopMerchant = topMerchant?.Name ?? "Veri Yok",
                TopMerchantAmount = topMerchant?.Total ?? 0,
                TotalTransactionCount = totalCount,
                RecentTransactions = lastTransactions
            };
        }
    }
}