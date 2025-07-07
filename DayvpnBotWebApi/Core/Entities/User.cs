namespace DayvpnBotWebApi.Core.Entities
{
    public class User : BaseEntity
    {
        public long TelegramId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public int SubscriptionCount { get; set; } = 0;
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        public decimal Balance { get; set; } = 0;

        public virtual ICollection<Subscription> Subscriptions { get; set; }
    }
}
