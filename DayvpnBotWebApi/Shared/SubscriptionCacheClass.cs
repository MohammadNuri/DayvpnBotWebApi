namespace DayvpnBotWebApi.Shared
{
    public class SubscriptionCacheClass
    {
        public int ServiceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DurationInDays { get; set; }
        public int DataQuotaGB { get; set; }
        public int AllowedUsersCount { get; set; }
        public decimal Price { get; set; }
        public string RequestedSubscriptioName { get; set; } = string.Empty;
    }
}
