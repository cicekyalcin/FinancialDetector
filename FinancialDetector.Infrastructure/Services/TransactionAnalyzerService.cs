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

        // --- AKILLI İSİM TEMİZLEME MOTORU (YENİ EKLENDİ) ---
        private string NormalizeMerchantName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "BİLİNMEYEN";

            string upperName = rawName.ToUpperInvariant();

            // 1. Aşama: Sektördeki bilinen markaları doğrudan yakalama
            var knownBrands = new List<string> {
                "NETFLIX", "SPOTIFY", "YOUTUBE", "APPLE", "AMAZON",
                "EXXEN", "BLUTV", "DISNEY", "YEMEKSEPETI", "GETIR",
                "MARTI", "TURKCELL", "VODAFONE", "TELEKOM", "DIGITURK", "SUPERONLINE"
            };

            foreach (var brand in knownBrands)
            {
                if (upperName.Contains(brand))
                {
                    // İçinde NETFLIX geçiyorsa, geri kalan tüm çöpü at ve sadece NETFLIX döndür.
                    return brand;
                }
            }

            // 2. Aşama: Eğer listede olmayan yeni bir markaysa, genel Sanal POS ve Banka çöplerini temizle
            string cleaned = upperName
                .Replace("IYZICO*", "")
                .Replace("POS*", "")
                .Replace("PAYTR*", "")
                .Replace("MEMBER", "")
                .Replace("ISTANBUL", "")
                .Replace("ANKARA", "")
                .Replace("_", " ")
                .Replace("*", " ");

            // Fazla boşlukları temizle ve saf kelimeyi bırak
            return string.Join(" ", cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        public async Task<object> AnalyzeUserTransactions(Guid userId)
        {
            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            var leaks = new List<object>();

            // --- DEĞİŞİKLİK BURADA: Gruplamayı artık ham veriye göre değil, ZEKİ FİLTREYE göre yapıyoruz ---
            var grouped = transactions.GroupBy(t => NormalizeMerchantName(t.RawMerchantName));

            foreach (var group in grouped)
            {
                var list = group.ToList();
                if (list.Count >= 2)
                {
                    // İlk referans fiyat ile son güncel fiyatı kıyasla
                    var first = list[0];
                    var last = list[^1];

                    if (last.Amount > first.Amount)
                    {
                        // Sızıntı Oranı Hesaplama
                        decimal increase = ((last.Amount - first.Amount) / first.Amount) * 100m;

                        leaks.Add(new
                        {
                            Merchant = group.Key, // Ekranda artık tertemiz "NETFLIX" yazacak
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