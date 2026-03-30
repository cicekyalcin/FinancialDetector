using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FinancialDetector.Domain.DTOs;
using FinancialDetector.Domain.Entities;
using FinancialDetector.Domain.Interfaces;
using FinancialDetector.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancialDetector.API.Controllers
{
    // [Authorize] etiketi, bu sınıftaki tüm uç noktalara sadece geçerli bir JWT'si olanların girmesini zorunlu kılar.
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

        [HttpPost("upload")]
        public async Task<IActionResult> UploadTransactions([FromBody] List<TransactionCreateDto> transactionsDto)
        {
            if (transactionsDto == null || !transactionsDto.Any())
            {
                return BadRequest("Yüklenecek işlem bulunamadı.");
            }

            // GÜVENLİK: Kullanıcının ID'sini asla dışarıdan parametre olarak almıyoruz.
            // Sistemi kandıramamaları için ID'yi doğrudan şifreli JWT Token'ın içinden çekiyoruz.
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized("Geçersiz kullanıcı kimliği. Lütfen tekrar giriş yapın.");
            }

            var newTransactions = new List<Transaction>();

            foreach (var dto in transactionsDto)
            {
                newTransactions.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TransactionDate = dto.TransactionDate,
                    RawMerchantName = dto.MerchantName,
                    Amount = dto.Amount,
                    Currency = dto.Currency,
                    IsProcessedForSubscription = false,
                    NormalizedMerchantName = string.Empty
                });
            }

            await _context.Transactions.AddRangeAsync(newTransactions);
            await _context.SaveChangesAsync();

            // Veriler kaydedildikten hemen sonra Faz 2'de yazdığımız Sızıntı Dedektörü Algoritmasını tetikliyoruz.
            await _analyzerService.AnalyzeUserTransactionsAsync(userId);

            return Ok(new { Message = $"{newTransactions.Count} adet işlem başarıyla yüklendi ve sızıntı analizi tamamlandı." });
        }

        [HttpGet("leaks")]
        public async Task<IActionResult> GetDetectedLeaks()
        {
            // Yine kimliği güvenli bir şekilde token içinden çekiyoruz.
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            // Global Query Filter ile zaten sadece bu kullanıcının verileri gelir, 
            // ama çift katmanlı güvenlik için (Defense in Depth) UserId'yi burada da sorguluyoruz.
            var leaks = await _context.Subscriptions
                .Where(s => s.UserId == userId && s.HasLeakDetected)
                .OrderByDescending(s => s.LastPaymentDate)
                .Select(s => new
                {
                    s.MerchantName,
                    s.LastDetectedAmount,
                    s.Currency,
                    s.EstimatedIntervalInDays,
                    s.LastPaymentDate,
                    s.NextEstimatedPaymentDate,
                    s.LeakWarningMessage
                })
                .ToListAsync();

            if (!leaks.Any())
            {
                return Ok(new { Message = "Harika! Herhangi bir finansal sızıntı, habersiz çekim veya zam tespit edilmedi.", Leaks = leaks });
            }

            return Ok(new { Message = $"Dikkat! {leaks.Count} adet potansiyel sızıntı tespit edildi.", Leaks = leaks });
        }
    }
}