namespace DayvpnBotWebApi.Core.Entities
{
    public class Service : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public int DurationInDays { get; set; }
        public int DataQuotaGB { get; set; }
        public int AllowedUsersCount { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }

        public virtual ICollection<Subscription> Subscriptions { get; set; }
    }
}
