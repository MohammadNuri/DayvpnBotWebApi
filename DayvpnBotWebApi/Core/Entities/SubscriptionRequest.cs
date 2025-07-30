using DayvpnBotWebApi.Shared;

namespace DayvpnBotWebApi.Core.Entities
{
    public class SubscriptionRequest : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        public string SubscriptionName { get; set; } = string.Empty;

        public Status Status { get; set; }
    }
}
