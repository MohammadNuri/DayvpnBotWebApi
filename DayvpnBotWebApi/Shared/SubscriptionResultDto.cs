namespace DayvpnBotWebApi.Shared
{
    public class SubscriptionResultDto
    {
        public string UserFullName { get; set; } = string.Empty;
        public long TelegramId { get; set; }
        public decimal NewBalance { get; set; }
        public DateTime PurchasedAt { get; set; }

        public string ServiceName { get; set; } = string.Empty;
        public int VolumeGb { get; set; }
        public int DurationDays { get; set; }
        public int UserCount { get; set; }
        public decimal Price { get; set; }
    }

}
