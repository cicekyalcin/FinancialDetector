using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FinancialDetector.Domain.Entities;
using FinancialDetector.Domain.Interfaces;
using FinancialDetector.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialDetector.API.Controllers
{
    // 1. DTO: Swagger'dan gelen saf JSON'ı 400 hatasına düşmeden karşılamak için kalkanımız.
    public class TransactionUploadDto
    {
        public DateTime TransactionDate { get; set; }
        public string MerchantName { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
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
        public async Task<IActionResult> UploadTransactions([FromBody] List<TransactionUploadDto> uploadData)
        {
            if (uploadData == null || !uploadData.Any())
            {
                return BadRequest("Yüklenecek işlem bulunamadı.");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Geçersiz oturum. Lütfen tekrar giriş yapın.");
            }

            // 2. KÖKLÜ ÇÖZÜM: DTO'yu, senin zorunlu alanları olan veritabanı Entity'sine dönüştürüyoruz.
            var newTransactions = uploadData.Select(dto => new Transaction
            {
                Id = Guid.Empty,
                UserId = userId,
                TransactionDate = dto.TransactionDate,
                Amount = dto.Amount,
                Currency = dto.Currency,
                RawMerchantName = dto.MerchantName,
                NormalizedMerchantName = dto.MerchantName.ToUpper() // Boş kalmaması için otomatik dolduruyoruz
            }).ToList();

            await _context.Transactions.AddRangeAsync(newTransactions);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"{newTransactions.Count} işlem başarıyla veritabanına kaydedildi." });
        }

        // 3. KAYBOLAN METOD: Sızıntı analizini ekrana getiren uç noktamız geri geldi.
        [HttpGet("leaks")]
        public async Task<IActionResult> GetLeaks()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Geçersiz oturum. Lütfen tekrar giriş yapın.");
            }

            // KRİTİK DÜZELTME: Metodu çağırırken (Guid userId) değil, sadece (userId) yazıyoruz.
            // Ayrıca metodun adını Interface'de belirlediğimiz orijinal 'AnalyzeUserTransactions' ile eşitledik.
            var result = await _analyzerService.AnalyzeUserTransactions(userId);

            return Ok(result);
        }
    }
}