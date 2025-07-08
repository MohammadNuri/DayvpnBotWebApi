namespace DayvpnBotWebApi.Shared
{
    public class UserProfileDTO
    {
        public string FullName { get; set; }
        public long TelegramId { get; set; }
        public DateTime RegisterDate { get; set; }
        public decimal Balance { get; set; }
        public int SubscriptionCount { get; set; }
    }
}
