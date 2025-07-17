namespace DayvpnBotWebApi.Shared
{
    public class UserCacheClass
    {
        public int RealUserId { get; set; }
        public long UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public UserState State { get; set; } = UserState.None;
    }
}
