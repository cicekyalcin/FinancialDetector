using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinancialDetector.Domain.Interfaces;
using FinancialDetector.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinancialDetector.Infrastructure.Services
{
    public class TransactionAnalyzerService : ITransactionAnalyzerService
    {
        private readonly AppDbContext _context;

        public TransactionAnalyzerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<object> AnalyzeUserTransactions(Guid userId)
        {
            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            var leaks = new List<object>();
            var grouped = transactions.GroupBy(t => t.RawMerchantName);

            foreach (var group in grouped)
            {
                var list = group.ToList();
                if (list.Count >= 2)
                {
                    // Sızıntı Mantığı Güncellendi: 
                    // Sadece son ikisini değil; son işlemi, o markaya ait İLK orijinal kayıtla (baz fiyat) kıyaslıyoruz.
                    var first = list[0];
                    var last = list[^1];

                    // Eğer güncel fiyat, kullanıcının o markaya ödediği ilk fiyattan 1 kuruş bile yüksekse alarm ver.
                    if (last.Amount > first.Amount)
                    {
                        // GÜVENLİK: Kesin ondalıklı hesaplama için 100 yerine 100m kullanıldı.
                        decimal increase = ((last.Amount - first.Amount) / first.Amount) * 100m;

                        leaks.Add(new
                        {
                            Merchant = group.Key,
                            Status = "Sızıntı Tespit Edildi",
                            IncreaseRate = $"{increase:F2}",
                            LastAmount = last.Amount,
                            PreviousAmount = first.Amount
                        });
                    }
                }
            }

            return leaks;
        }
    }
}