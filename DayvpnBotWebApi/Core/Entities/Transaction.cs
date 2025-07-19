using DayvpnBotWebApi.Shared;
using System.Transactions;
using TransactionStatus = DayvpnBotWebApi.Shared.TransactionStatus;

namespace DayvpnBotWebApi.Core.Entities
{
    public class Transaction : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public TransactionType Type { get; set; }

        public decimal Amount { get; set; }

        public string? Description { get; set; }

        public PaymentMethod? PaymentMethod { get; set; }

        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    }
}
