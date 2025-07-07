namespace DayvpnBotWebApi.Shared
{
    public enum LogEventType
    {
        UserRegistered,
        UserLoggedIn,
        UserLoggedOut,
        SubscriptionPurchased,
        SubscriptionRenewed,
        SubscriptionActivated,
        SubscriptionExpired,
        SubscriptionLinkOpened,
        BalanceIncreased,
        BalanceDecreased,
        Error,
        Info,
        Warning
    }
}
