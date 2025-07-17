namespace DayvpnBotWebApi.Core.Entities
{
    public class Subscription : BaseEntity
    {
        public string SubscriptionCode { get; set; } = string.Empty;
        public string SubscriptionName { get; set; } = string.Empty;
        public int SubscriptionVolumeGb { get; set; } = 0;
        public int UsedVolumeMb { get; set; } = 0;
        public DateTime? ActivationDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public DateTime? LastConnectionDate { get; set; }
        public string? LastConnectedClient { get; set; }
        public string SubscriptionLink { get; set; } = string.Empty;
        public DateTime? LastUpdatedDate { get; set; }
        public string SubscriptionStatus { get; set; } = string.Empty;
        public string TrackingCode { get; set; } = string.Empty;
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        public List<SubscriptionLinks> SubscriptionLinks { get; set; } = new List<SubscriptionLinks>(); 
    }
}
