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

            var clusters = new Dictionary<string, List<Transaction>>();

            // NEGATİF KELİME KALKANI (Kantin, Market, Yemek vb. abonelik değildir)
            string[] blackList = { "KANTIN", "CAFE", "RESTORAN", "MARKET", "BUFE", "YEMEK", "SU", "KAHVE" };

            foreach (var tx in transactions)
            {
                string cleanName = CleanGarbageWords(tx.RawMerchantName);
                bool addedToExistingCluster = false;

                var incomingWords = cleanName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                             .Where(w => w.Length > 3)
                                             .ToList();

                if (!incomingWords.Any())
                {
                    incomingWords.Add(cleanName);
                }

                // Gelen harcamada yasaklı gıda/hizmet kelimesi var mı?
                bool isIncomingBlacklisted = blackList.Any(b => cleanName.Contains(b));

                foreach (var key in clusters.Keys)
                {
                    var keyWords = key.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                      .Where(w => w.Length > 3)
                                      .ToList();

                    if (!keyWords.Any())
                    {
                        keyWords.Add(key);
                    }

                    // Küme adında yasaklı kelime var mı?
                    bool isKeyBlacklisted = blackList.Any(b => key.Contains(b));

                    // ZEKİ KONTROL: Biri kantin harcaması, diğeri normal abonelikse, ortak kelime (MACFIT) olsa bile bunları ASLA BİRLEŞTİRME!
                    if (isIncomingBlacklisted != isKeyBlacklisted)
                    {
                        continue;
                    }

                    bool hasCommonWord = incomingWords.Intersect(keyWords).Any();

                    if (hasCommonWord || cleanName.Contains(key) || key.Contains(cleanName))
                    {
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

                if (!addedToExistingCluster)
                {
                    clusters[cleanName] = new List<Transaction> { tx };
                }
            }

            var leaks = new List<object>();

            foreach (var cluster in clusters)
            {
                var list = cluster.Value.OrderBy(t => t.TransactionDate).ToList();

                // Sadece içinde negatif kelime OLMAYAN, saf abonelik kümelerini sızıntı olarak değerlendir (Kantin zamları sızıntı abonelik değildir)
                bool isClusterBlacklisted = blackList.Any(b => cluster.Key.Contains(b));

                if (list.Count >= 2 && !isClusterBlacklisted)
                {
                    var first = list[0];
                    var last = list[^1];

                    if (last.Amount > first.Amount)
                    {
                        decimal increase = ((last.Amount - first.Amount) / first.Amount) * 100m;

                        leaks.Add(new
                        {
                            Merchant = cluster.Key,
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