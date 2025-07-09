namespace DayvpnBotWebApi.Core.Entities
{
    public class SubscriptionLinks : BaseEntity
    {
        public int SubscriptionId { get; set; }
        public Subscription Subscription { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
    }
}
