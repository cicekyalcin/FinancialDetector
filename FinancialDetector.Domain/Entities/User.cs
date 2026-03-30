using System;
using System.Collections.Generic;
using System.Transactions;

namespace FinancialDetector.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        public ICollection<Transaction> Transactions { get; set; }
        public ICollection<Subscription> Subscriptions { get; set; }

        public User()
        {
            Transactions = new HashSet<Transaction>();
            Subscriptions = new HashSet<Subscription>();
        }
    }
}