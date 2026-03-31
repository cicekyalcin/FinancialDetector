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
using Microsoft.EntityFrameworkCore; // Fiziksel SQL kontrolü (AnyAsync) için eklendi.

namespace FinancialDetector.API.Controllers
{
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

            // KRİTİK GÜVENLİK DUVARI: Token geçerli olsa bile kullanıcı SQL'de fiziksel olarak var mı?
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return Unauthorized("Token onaylandı ancak bu kimliğe sahip bir kullanıcı SQL veritabanında yok! (Muhtemelen veritabanını sıfırladınız). Lütfen önce /api/Auth/register ile yeniden kayıt olun, ardından /login ile yeni token alıp tekrar deneyin.");
            }

            // GÜVENLİK 2: EF Core'un rastgele ID atamasında kafasının karışmasını önlemek için Guid.NewGuid() ile biz mühürlüyoruz.
            var newTransactions = uploadData.Select(dto => new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TransactionDate = dto.TransactionDate,
                Amount = dto.Amount,
                Currency = dto.Currency,
                RawMerchantName = dto.MerchantName,
                NormalizedMerchantName = dto.MerchantName.ToUpper()
            }).ToList();

            await _context.Transactions.AddRangeAsync(newTransactions);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"{newTransactions.Count} işlem başarıyla veritabanına kaydedildi." });
        }

        [HttpGet("leaks")]
        public async Task<IActionResult> GetLeaks()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Geçersiz oturum. Lütfen tekrar giriş yapın.");
            }

            var result = await _analyzerService.AnalyzeUserTransactions(userId);

            return Ok(result);
        }
    }
}