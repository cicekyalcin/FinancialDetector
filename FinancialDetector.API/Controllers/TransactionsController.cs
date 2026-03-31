using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FinancialDetector.Domain.Entities;
using FinancialDetector.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialDetector.API.Controllers
{
    public class TransactionUploadDto
    {
        public DateTime TransactionDate { get; set; }
        public string MerchantName { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }

    public class TransactionFilterDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? Month { get; set; }
        public string? MerchantName { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        // MİMARİ DÜZELTME: Veritabanı yerine, soyutlanmış Repository arayüzü kullanılıyor.
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionAnalyzerService _analyzerService;

        public TransactionsController(ITransactionRepository transactionRepository, ITransactionAnalyzerService analyzerService)
        {
            _transactionRepository = transactionRepository;
            _analyzerService = analyzerService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactions([FromQuery] TransactionFilterDto filter)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Geçersiz oturum. Lütfen tekrar giriş yapın.");
            }

            // Karmaşık SQL sorguları yok. İşi sadece Repository'ye devrediyoruz.
            var result = await _transactionRepository.GetTransactionsAsync(
                userId, filter.PageNumber, filter.PageSize, filter.StartDate, filter.EndDate, filter.Month, filter.MerchantName);

            var totalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize);

            // API sadece veriyi formatlayıp dışarı sunar.
            var responseData = result.Data.Select(t => new
            {
                t.Id,
                t.TransactionDate,
                t.RawMerchantName,
                t.NormalizedMerchantName,
                t.Amount,
                t.Currency
            });

            return Ok(new
            {
                TotalRecords = result.TotalCount,
                TotalPages = totalPages,
                CurrentPage = filter.PageNumber,
                PageSize = filter.PageSize,
                Data = responseData
            });
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

            var userExists = await _transactionRepository.UserExistsAsync(userId);
            if (!userExists)
            {
                return Unauthorized("Token onaylandı ancak bu kimliğe sahip bir kullanıcı SQL veritabanında yok! Lütfen önce /api/Auth/register ile yeniden kayıt olun.");
            }

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

            await _transactionRepository.AddTransactionsAsync(newTransactions);

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