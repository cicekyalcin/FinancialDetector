using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinancialDetector.Domain.Interfaces;
using FinancialDetector.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using FinancialDetector.Domain.Entities;

namespace FinancialDetector.Infrastructure.Services
{
    public class TransactionAnalyzerService : ITransactionAnalyzerService
    {
        private readonly AppDbContext _context;

        public TransactionAnalyzerService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Banka pos cihazlarindan gelen asiri kirli verileri temizler.
        /// </summary>
        private string CleanGarbageWords(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "BILINMEYEN";

            string cleaned = rawName.ToUpperInvariant()
                .Replace("IYZICO*", "")
                .Replace("POS*", "")
                .Replace("PAYTR*", "")
                .Replace("MEMBER", "")
                .Replace("ISTANBUL", "")
                .Replace("ANKARA", "")
                .Replace("_", " ")
                .Replace("*", " ");

            return string.Join(" ", cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        public async Task<object> AnalyzeUserTransactions(Guid userId)
        {
            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            // DINAMIK KUMELEME (EVRENSEL NLP ALGORITMASI)
            var clusters = new Dictionary<string, List<Transaction>>();

            foreach (var tx in transactions)
            {
                string cleanName = CleanGarbageWords(tx.RawMerchantName);
                bool addedToExistingCluster = false;

                // 1. ADIM: Gelen kelimeyi parcala ve anlamsiz kisa harfleri (ve, ile, su) filtrele
                var incomingWords = cleanName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                             .Where(w => w.Length > 3)
                                             .ToList();

                // Eger butun kelimeler 3 harften kisaysa (Orn: "A101"), ismin kendisini al.
                if (!incomingWords.Any())
                {
                    incomingWords.Add(cleanName);
                }

                foreach (var key in clusters.Keys)
                {
                    // Klasor ismini de ayni sekilde parcala
                    var keyWords = key.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                      .Where(w => w.Length > 3)
                                      .ToList();

                    if (!keyWords.Any())
                    {
                        keyWords.Add(key);
                    }

                    // 2. ADIM: KESISIM KONTROLU (En az 1 ortak anlamli kelime var mi?)
                    bool hasCommonWord = incomingWords.Intersect(keyWords).Any();

                    // Ortak kelime varsa VEYA cumleler birbirini kapsiyorsa
                    if (hasCommonWord || cleanName.Contains(key) || key.Contains(cleanName))
                    {
                        // 3. ADIM: FIYAT TOLERANSI KONTROLU (< 3.0 kati)
                        var referenceAmount = clusters[key].First().Amount;
                        decimal ratio = tx.Amount > referenceAmount
                            ? tx.Amount / referenceAmount
                            : referenceAmount / tx.Amount;

                        if (ratio < 3.0m)
                        {
                            clusters[key].Add(tx);
                            addedToExistingCluster = true;
                            break;
                        }
                    }
                }

                // Ortak bir marka bulunamadiysa yeni klasor ac
                if (!addedToExistingCluster)
                {
                    clusters[cleanName] = new List<Transaction> { tx };
                }
            }

            // SIZINTI MATEMATIGI
            var leaks = new List<object>();

            foreach (var cluster in clusters)
            {
                var list = cluster.Value.OrderBy(t => t.TransactionDate).ToList();

                if (list.Count >= 2)
                {
                    var first = list[0];
                    var last = list[^1];

                    // Ilk ve Son harcama arasinda fiyat artisi var mi?
                    if (last.Amount > first.Amount)
                    {
                        decimal increase = ((last.Amount - first.Amount) / first.Amount) * 100m;

                        leaks.Add(new
                        {
                            Merchant = cluster.Key, // Ekranda ilk kaydedilen ortak adi gosterir
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