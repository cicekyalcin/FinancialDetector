using System;
using System.Threading.Tasks;
using FinancialDetector.Domain.DTOs;
using FinancialDetector.Domain.Entities;
using FinancialDetector.Domain.Interfaces;
using FinancialDetector.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancialDetector.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;

        public AuthController(AppDbContext context, IJwtTokenGenerator jwtTokenGenerator)
        {
            _context = context;
            _jwtTokenGenerator = jwtTokenGenerator;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest("Bu e-posta adresi zaten sistemde kayıtlı.");
            }

            // MÜLAKAT KRİTERİ: Şifreler veritabanına asla açık metin yazılmaz. BCrypt ile geri döndürülemez şekilde şifreliyoruz.
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                FullName = dto.FullName,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Kayıt başarılı. Lütfen giriş yapınız." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == dto.Email);

            // Kullanıcı yoksa veya BCrypt ile şifre eşleşmiyorsa kapıdan çevir.
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                return Unauthorized("E-posta adresiniz veya şifreniz hatalı.");
            }

            string token = _jwtTokenGenerator.GenerateToken(user);

            return Ok(new { Token = token, Message = "Giriş başarılı. İşlemlerinizi bu token ile yapabilirsiniz." });
        }
    }
}