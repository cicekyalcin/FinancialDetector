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
            // İzolasyon sağlayan ana sorgu
            var query = _context.Transactions.Where(t => t.UserId == userId).AsQueryable();

            // Dinamik Filtreleme Katmanı (Ertelenmiş Çalıştırma)
            if (startDate.HasValue)
                query = query.Where(t => t.TransactionDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.TransactionDate <= endDate.Value);

            if (month.HasValue && month.Value >= 1 && month.Value <= 12)
                query = query.Where(t => t.TransactionDate.Month == month.Value);

            if (!string.IsNullOrWhiteSpace(merchantName))
                query = query.Where(t => t.NormalizedMerchantName.Contains(merchantName.ToUpper()));

            query = query.OrderByDescending(t => t.TransactionDate);

            // Veritabanına Vurma Anı
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
    }
}