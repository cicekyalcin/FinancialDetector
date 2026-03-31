using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FinancialDetector.Domain.Entities;

namespace FinancialDetector.Domain.Interfaces
{
    public interface ITransactionRepository
    {
        // 1. Veri listeleme ve filtreleme sözleşmesi
        Task<(IEnumerable<Transaction> Data, int TotalCount)> GetTransactionsAsync(Guid userId, int pageNumber, int pageSize, DateTime? startDate, DateTime? endDate, int? month, string merchantName);

        // 2. Veri ekleme sözleşmesi
        Task AddTransactionsAsync(IEnumerable<Transaction> transactions);

        // 3. Güvenlik: Kullanıcının fiziksel olarak var olup olmadığını kontrol sözleşmesi
        Task<bool> UserExistsAsync(Guid userId);
    }
}