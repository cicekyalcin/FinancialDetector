using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FinancialDetector.Domain.Entities;
using FinancialDetector.Infrastructure.Data;
using FinancialDetector.Domain.Interfaces;

namespace FinancialDetector.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITransactionAnalyzerService _analyzerService;

        public TransactionsController(AppDbContext context, ITransactionAnalyzerService analyzerService)
        {
            _context = context;
            _analyzerService = analyzerService;
        }

        [HttpGet]
        public IActionResult GetUserTransactions()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized("Kullanıcı kimliği bulunamadı.");

            var userId = Guid.Parse(userIdStr);
            var transactions = _context.Transactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            return Ok(new { Data = transactions });
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadTransactions([FromBody] List<Transaction> transactions)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized("Kullanıcı kimliği bulunamadı.");

            var userId = Guid.Parse(userIdStr);
            int addedCount = 0;

            foreach (var tx in transactions)
            {
                // ZIRH 1: Manuel eklemelerde aynı harcama zaten varsa atla!
                bool alreadyExists = _context.Transactions.Any(t =>
                    t.UserId == userId &&
                    t.TransactionDate.Date == tx.TransactionDate.Date &&
                    t.Amount == tx.Amount &&
                    t.RawMerchantName == tx.RawMerchantName);

                if (!alreadyExists)
                {
                    tx.Id = Guid.NewGuid();
                    tx.UserId = userId;

                    if (string.IsNullOrEmpty(tx.NormalizedMerchantName) && !string.IsNullOrEmpty(tx.RawMerchantName))
                    {
                        tx.NormalizedMerchantName = tx.RawMerchantName.Trim().ToUpperInvariant();
                    }

                    _context.Transactions.Add(tx);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = $"{addedCount} yeni işlem eklendi. (Mevcut olanlar korundu)" });
        }

        [HttpGet("leaks")]
        public async Task<IActionResult> GetLeaks()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            var userId = Guid.Parse(userIdStr);
            var leaks = await _analyzerService.AnalyzeUserTransactions(userId);

            return Ok(leaks);
        }

        [HttpPost("upload-csv")]
        public async Task<IActionResult> UploadCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Lütfen geçerli bir CSV dosyası seçin.");

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized("Kullanıcı kimliği bulunamadı.");

            var userId = Guid.Parse(userIdStr);
            var parsedTransactions = new List<Transaction>();
            int skippedCount = 0;

            try
            {
                using (var stream = new StreamReader(file.OpenReadStream()))
                {
                    await stream.ReadLineAsync();

                    while (!stream.EndOfStream)
                    {
                        var line = await stream.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = line.Split(',');

                        if (values.Length >= 3)
                        {
                            bool isDateParsed = DateTime.TryParse(values[0].Trim(), out DateTime txDate);
                            bool isAmountParsed = decimal.TryParse(values[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount);

                            if (isDateParsed && isAmountParsed)
                            {
                                string rawName = values[1].Trim();

                                // ZIRH 2: CSV Yüklemelerinde aynı harcama zaten varsa atla!
                                bool alreadyExists = _context.Transactions.Any(t =>
                                    t.UserId == userId &&
                                    t.TransactionDate.Date == txDate.Date &&
                                    t.Amount == amount &&
                                    t.RawMerchantName == rawName);

                                if (!alreadyExists)
                                {
                                    parsedTransactions.Add(new Transaction
                                    {
                                        Id = Guid.NewGuid(),
                                        UserId = userId,
                                        TransactionDate = txDate,
                                        RawMerchantName = rawName,
                                        NormalizedMerchantName = rawName.ToUpperInvariant(),
                                        Amount = amount,
                                        Currency = "TRY"
                                    });
                                }
                                else
                                {
                                    skippedCount++;
                                }
                            }
                        }
                    }
                }

                if (parsedTransactions.Count > 0)
                {
                    _context.Transactions.AddRange(parsedTransactions);
                    await _context.SaveChangesAsync();
                    return Ok(new { Message = $"{parsedTransactions.Count} yeni harcama eklendi! ({skippedCount} mükerrer kayıt çöpe atıldı.)" });
                }

                return Ok(new { Message = $"Yeni veri bulunamadı. ({skippedCount} mükerrer kayıt çöpe atıldı.)" });
            }
            catch (Exception ex)
            {
                string realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Veritabanı Kayıt Hatası: {realError}");
            }
        }

        // ÇÖPLÜĞÜ TEMİZLEMEK İÇİN YENİ SİLAHIN
        [HttpDelete("reset-data")]
        public async Task<IActionResult> ResetData()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            var userId = Guid.Parse(userIdStr);

            var userTransactions = _context.Transactions.Where(t => t.UserId == userId).ToList();
            _context.Transactions.RemoveRange(userTransactions);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Sistemdeki tüm test harcamaların temizlendi. Temiz bir sayfa açıldı." });
        }
    }
}