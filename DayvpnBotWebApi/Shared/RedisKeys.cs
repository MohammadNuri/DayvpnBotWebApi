namespace DayvpnBotWebApi.Shared
{
    public static class RedisKeys
    {
        public static string User(long userId) => $"user:{userId}";
        public static string User() => "user";
        public static string Subscription(long userId) => $"subscription:{userId}";
        public static string Subscription() => "subscription";
        public static string Wallet(long userId) => $"wallet:{userId}";
        public static string Wallet() => "wallet";
        public static string UsersList => "users:list";
    }
}
