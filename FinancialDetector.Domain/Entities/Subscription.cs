using System;

namespace FinancialDetector.Domain.Entities
{
    public class Subscription
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public string MerchantName { get; set; }
        public decimal LastDetectedAmount { get; set; }
        public string Currency { get; set; }
        public int EstimatedIntervalInDays { get; set; }
        public DateTime LastPaymentDate { get; set; }
        public DateTime NextEstimatedPaymentDate { get; set; }

        public bool IsActive { get; set; }
        public bool HasLeakDetected { get; set; }
        public string LeakWarningMessage { get; set; }

        public User User { get; set; }
    }
}