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
using Microsoft.EntityFrameworkCore;

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

        // YENİ EKLENEN MİMARİ: Sayfalamalı Listeleme (Pagination)
        [HttpGet]
        public async Task<IActionResult> GetTransactions([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Geçersiz oturum. Lütfen tekrar giriş yapın.");
            }

            // Güvenlik: Sadece giriş yapan kullanıcının verilerini çek ve tarihe göre en yeniden eskiye sırala.
            var query = _context.Transactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate);

            // Matematik: Toplam kayıt ve sayfa sayısını hesapla
            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            // Veriyi sayfalayarak çek (Sadece istenen sayfanın verisi RAM'e alınır)
            var transactions = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.TransactionDate,
                    t.RawMerchantName,
                    t.NormalizedMerchantName,
                    t.Amount,
                    t.Currency
                })
                .ToListAsync();

            return Ok(new
            {
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                Data = transactions
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

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
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