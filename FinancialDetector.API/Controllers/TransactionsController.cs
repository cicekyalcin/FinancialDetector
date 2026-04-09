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

            foreach (var tx in transactions)
            {
                tx.Id = Guid.NewGuid();
                tx.UserId = userId;

                // Manuel eklemelerde Normalized kolonunu otomatik doldurur
                if (string.IsNullOrEmpty(tx.NormalizedMerchantName) && !string.IsNullOrEmpty(tx.RawMerchantName))
                {
                    tx.NormalizedMerchantName = tx.RawMerchantName.Trim().ToUpperInvariant();
                }

                _context.Transactions.Add(tx);
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "İşlemler başarıyla kaydedildi." });
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
                                parsedTransactions.Add(new Transaction
                                {
                                    Id = Guid.NewGuid(),
                                    UserId = userId,
                                    TransactionDate = txDate,
                                    RawMerchantName = values[1].Trim(),
                                    // İşte SQL'in istediği o meşhur kolon! Artık kızmayacak.
                                    NormalizedMerchantName = values[1].Trim().ToUpperInvariant(),
                                    Amount = amount,
                                    Currency = "TRY"
                                });
                            }
                        }
                    }
                }

                if (parsedTransactions.Count > 0)
                {
                    _context.Transactions.AddRange(parsedTransactions);
                    await _context.SaveChangesAsync();
                    return Ok(new { Message = $"{parsedTransactions.Count} adet harcama başarıyla sisteme aktarıldı!" });
                }

                return BadRequest("CSV dosyasından hiçbir geçerli veri okunamadı. Sütun formatlarınızı kontrol edin.");
            }
            catch (Exception ex)
            {
                string realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Veritabanı Kayıt Hatası: {realError}");
            }
        }
    }
}