using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FinancialDetector.Domain.Entities;
using FinancialDetector.Domain.Interfaces;
using FinancialDetector.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinancialDetector.Infrastructure.Services
{
    public class TransactionAnalyzerService : ITransactionAnalyzerService
    {
        private readonly AppDbContext _context;
        // Yabancı menşeili aboneliklerdeki kurşluk/küçük liralık kur farklarını sızıntı saymamak için %3 tolerans sınırı.
        private const decimal LEAK_TOLERANCE_PERCENTAGE = 0.03m;

        public TransactionAnalyzerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task AnalyzeUserTransactionsAsync(Guid userId)
        {
            // Global Query Filter DbContext seviyesinde koruma sağlasa da, savunma amaçlı programlama (Defensive Programming) gereği UserId filtremizi buraya da ekliyoruz.
            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId && !t.IsProcessedForSubscription)
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            if (!transactions.Any()) return;

            // 1. Veri Normalizasyon Katmanı
            foreach (var transaction in transactions)
            {
                transaction.NormalizedMerchantName = NormalizeMerchantName(transaction.RawMerchantName);
            }

            // 2. Gruplama ve Zaman Serisi Analizi
            var groupedTransactions = transactions
                .GroupBy(t => t.NormalizedMerchantName)
                .Where(g => g.Count() >= 2); // Abonelik periyodu tespit edebilmek için en az 2 veri noktası şarttır.

            foreach (var group in groupedTransactions)
            {
                var sortedList = group.OrderBy(t => t.TransactionDate).ToList();
                var latestTransaction = sortedList.Last();
                var previousTransaction = sortedList[sortedList.Count - 2];

                // Zaman Aralığı (Delta T) Hesaplama
                var intervalSpan = latestTransaction.TransactionDate - previousTransaction.TransactionDate;
                int estimatedIntervalDays = intervalSpan.Days;

                // Bir işlemin periyodik abonelik sayılabilmesi için 20 gün ile 400 gün arasında bir frekansı olmalıdır (Aylık veya Yıllık abonelikler).
                if (estimatedIntervalDays < 20 || estimatedIntervalDays > 400)
                {
                    continue;
                }

                // 3. Matematiksel Sızıntı (Leak) Tespiti
                bool isLeakDetected = false;
                string leakMessage = string.Empty;

                if (latestTransaction.Amount > previousTransaction.Amount)
                {
                    decimal difference = latestTransaction.Amount - previousTransaction.Amount;
                    decimal percentageIncrease = difference / previousTransaction.Amount;

                    if (percentageIncrease > LEAK_TOLERANCE_PERCENTAGE)
                    {
                        isLeakDetected = true;
                        leakMessage = $"Beklenmeyen fiyat artışı tespit edildi. Önceki Tutar: {previousTransaction.Amount} {previousTransaction.Currency}, Yeni Tutar: {latestTransaction.Amount} {latestTransaction.Currency}. Artış Oranı: %{Math.Round(percentageIncrease * 100, 2)}";
                    }
                }

                // 4. Abonelik Kaydının (Subscription) Güncellenmesi veya Oluşturulması
                var existingSubscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.MerchantName == group.Key);

                if (existingSubscription != null)
                {
                    existingSubscription.LastDetectedAmount = latestTransaction.Amount;
                    existingSubscription.LastPaymentDate = latestTransaction.TransactionDate;
                    existingSubscription.NextEstimatedPaymentDate = latestTransaction.TransactionDate.AddDays(estimatedIntervalDays);
                    existingSubscription.HasLeakDetected = isLeakDetected;
                    existingSubscription.LeakWarningMessage = leakMessage;
                }
                else
                {
                    var newSubscription = new Subscription
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        MerchantName = group.Key,
                        LastDetectedAmount = latestTransaction.Amount,
                        Currency = latestTransaction.Currency,
                        EstimatedIntervalInDays = estimatedIntervalDays,
                        LastPaymentDate = latestTransaction.TransactionDate,
                        NextEstimatedPaymentDate = latestTransaction.TransactionDate.AddDays(estimatedIntervalDays),
                        IsActive = true,
                        HasLeakDetected = isLeakDetected,
                        LeakWarningMessage = leakMessage
                    };
                    _context.Subscriptions.Add(newSubscription);
                }

                // İşlenen satırları sistemin tekrar taramaması için işaretliyoruz
                foreach (var t in group)
                {
                    t.IsProcessedForSubscription = true;
                }
            }

            await _context.SaveChangesAsync();
        }

        private string NormalizeMerchantName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "UNKNOWN";

            string normalized = rawName.ToUpperInvariant();

            // Ödeme sağlayıcı ön eklerini temizle
            normalized = Regex.Replace(normalized, @"(IYZICO|PAYTR|IYZ|PARAM|MOKA)\s*\*?\s*", "");

            // Domain uzantılarını temizle (.COM, .NET vb.)
            normalized = Regex.Replace(normalized, @"\.(COM|NET|ORG|CO|TR)", "");

            // Özel karakterleri temizle (Sadece harf, rakam ve boşluk kalır)
            normalized = Regex.Replace(normalized, @"[^A-Z0-9 ]", "");

            // Yan yana birden fazla boşluk oluştuysa tek boşluğa indirge
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            // Şehir/Bölge lokasyon verilerini temizle
            var locationKeywords = new[] { "ISTANBUL", "ANKARA", "IZMIR", "TURKEY", "AMSTERDAM", "LONDON", "DUBLIN" };
            foreach (var loc in locationKeywords)
            {
                normalized = normalized.Replace(loc, "").Trim();
            }

            return string.IsNullOrWhiteSpace(normalized) ? "UNKNOWN" : normalized;
        }
    }
}