namespace DayvpnBotWebApi.Core.Entities
{
    public class Subscription : BaseEntity
    {
        public string SubscriptionCode { get; set; } = string.Empty;
        public string SubscriptionName { get; set; } = string.Empty;
        public int SubscriptionVolumeMb { get; set; } = 0;
        public int UsedVolumeMb { get; set; } = 0;
        public DateTime? ActivationDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public DateTime? LastConnectionDate { get; set; }
        public string? LastConnectedClient { get; set; }
        public string SubscriptionLink { get; set; } = string.Empty;
        public DateTime? LastUpdatedDate { get; set; }
        public string SubscriptionStatus { get; set; } = string.Empty;

        public int UserId { get; set; }
        public User User { get; set; } = new User();

        public List<SubscriptionLinks> SubscriptionLinks { get; set; } = new List<SubscriptionLinks>(); 
    }
}
