using DayvpnBotWebApi.Shared;

namespace DayvpnBotWebApi.Core.Entities
{
    public class TransactionRequest : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public TransactionRequestStatus Status { get; set; } = TransactionRequestStatus.Pending;

        public string? TrackingCode { get; set; }
    }
}
