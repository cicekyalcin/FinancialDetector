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
                return Unauthorized("Geçersiz oturum.");
            }

            var result = await _transactionRepository.GetTransactionsAsync(
                userId, filter.PageNumber, filter.PageSize, filter.StartDate, filter.EndDate, filter.Month, filter.MerchantName);

            var totalPages = (int)Math.Ceiling(result.TotalCount / (double)filter.PageSize);

            return Ok(new
            {
                TotalRecords = result.TotalCount,
                TotalPages = totalPages,
                CurrentPage = filter.PageNumber,
                Data = result.Data
            });
        }

        // DÜZELTİLEN METOT: LINQ (Count/Select) işlemleri kaldırıldı.
        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Geçersiz oturum.");
            }

            var stats = await _transactionRepository.GetDashboardStatsAsync(userId);
            var leaks = await _analyzerService.AnalyzeUserTransactions(userId);

            return Ok(new
            {
                Overview = stats,
                Leaks = leaks // Sızıntı verisini olduğu gibi Front-end'e paslıyoruz.
            });
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadTransactions([FromBody] List<TransactionUploadDto> uploadData)
        {
            if (uploadData == null || !uploadData.Any()) return BadRequest("Veri yok.");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId)) return Unauthorized();

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
            return Ok(new { Message = "Yüklendi." });
        }

        [HttpGet("leaks")]
        public async Task<IActionResult> GetLeaks()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId)) return Unauthorized();

            var result = await _analyzerService.AnalyzeUserTransactions(userId);
            return Ok(result);
        }
    }
}