using System;

namespace FinancialDetector.Domain.Entities
{
    public class Transaction
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public DateTime TransactionDate { get; set; }
        public string RawMerchantName { get; set; }
        public string NormalizedMerchantName { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
       
        public bool IsProcessedForSubscription { get; set; }

        public User ? User { get; set; }
    }
}