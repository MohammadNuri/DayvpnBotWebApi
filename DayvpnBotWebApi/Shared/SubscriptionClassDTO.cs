namespace DayvpnBotWebApi.Shared
{
    public class SubscriptionClassDTO
    {
        public long UserId { get; set; }
        public string SubscriptionName { get; set; } = string.Empty;
        public int ServiceId { get; set; }
    }
}