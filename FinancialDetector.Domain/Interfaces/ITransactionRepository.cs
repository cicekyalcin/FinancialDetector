using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FinancialDetector.Domain.Entities;

namespace FinancialDetector.Domain.Interfaces
{
    public interface ITransactionRepository
    {
        Task<(IEnumerable<Transaction> Data, int TotalCount)> GetTransactionsAsync(Guid userId, int pageNumber, int pageSize, DateTime? startDate, DateTime? endDate, int? month, string merchantName);
        Task AddTransactionsAsync(IEnumerable<Transaction> transactions);
        Task<bool> UserExistsAsync(Guid userId);

        // YENİ: Dashboard verilerini tek seferde hesaplayan metot sözleşmesi
        Task<object> GetDashboardStatsAsync(Guid userId);
    }
}