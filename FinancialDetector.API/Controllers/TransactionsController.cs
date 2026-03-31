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
    // 1. DTO: Veri yükleme işlemleri için nesnemiz
    public class TransactionUploadDto
    {
        public DateTime TransactionDate { get; set; }
        public string MerchantName { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }

    // 2. YENİ DTO: Arama, Filtreleme ve Sayfalama kriterlerini tutan kurumsal nesnemiz
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
        private readonly AppDbContext _context;
        private readonly ITransactionAnalyzerService _analyzerService;

        public TransactionsController(AppDbContext context, ITransactionAnalyzerService analyzerService)
        {
            _context = context;
            _analyzerService = analyzerService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactions([FromQuery] TransactionFilterDto filter)
        {
            // GÜVENLİK ADIMI 1: Kimlik Doğrulama
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Geçersiz oturum. Lütfen tekrar giriş yapın.");
            }

            // GÜVENLİK ADIMI 2: Veri İzolasyonu. Sorguyu SADECE bu kullanıcıya ait verilerle başlat.
            var query = _context.Transactions.Where(t => t.UserId == userId).AsQueryable();

            // MİMARİ ADIM: Dinamik Filtreleme İnşası (Deferred Execution)
            if (filter.StartDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate <= filter.EndDate.Value);
            }

            if (filter.Month.HasValue && filter.Month.Value >= 1 && filter.Month.Value <= 12)
            {
                // Belirli bir aydaki (Örn: Sadece Mart) harcamaları getir.
                query = query.Where(t => t.TransactionDate.Month == filter.Month.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.MerchantName))
            {
                // Kurum adına göre arama (Büyük/küçük harf duyarsız arama için veritabanındaki normalize alanı kullanıyoruz)
                query = query.Where(t => t.NormalizedMerchantName.Contains(filter.MerchantName.ToUpper()));
            }

            // Sıralama (En yeniden en eskiye)
            query = query.OrderByDescending(t => t.TransactionDate);

            // Sayfalama Matematiği
            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalRecords / (double)filter.PageSize);

            // VERİTABANINA VURMA ANI (SQL Server sadece bu satırda çalışır ve filtrelenmiş paket veriyi getirir)
            var transactions = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
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
                CurrentPage = filter.PageNumber,
                PageSize = filter.PageSize,
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