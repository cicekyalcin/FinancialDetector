using System;
using System.ComponentModel.DataAnnotations;

namespace FinancialDetector.Domain.DTOs
{
    public class UserRegisterDto
    {
        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi formatı giriniz.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Şifre güvenliğiniz için en az 6 karakter olmalıdır.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        [MaxLength(100, ErrorMessage = "Ad Soyad 100 karakterden uzun olamaz.")]
        public string FullName { get; set; }
    }
}