using System;

namespace FinancialDetector.Domain.DTOs
{
    public class TransactionCreateDto
    {
        public DateTime TransactionDate { get; set; }
        public string MerchantName { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }
}