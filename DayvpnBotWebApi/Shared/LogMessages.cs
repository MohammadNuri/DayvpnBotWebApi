namespace DayvpnBotWebApi.Shared
{
    public static class LogMessages
    {
        // 🔹 User
        public const string UserRegistered = "کاربر با موفقیت ثبت‌نام کرد.";

        // 🔹 Subscription
        public const string SubscriptionPurchased = "کاربر یک اشتراک جدید خریداری کرد.";
        public const string SubscriptionRenewed = "کاربر اشتراک خود را تمدید کرد.";
        public const string SubscriptionActivated = "اشتراک کاربر فعال شد.";
        public const string SubscriptionExpired = "اشتراک منقضی شد.";
        public const string SubscriptionLinkOpened = "کاربر لینک اتصال اشتراک را باز کرد.";

        // 🔹 Balance
        public const string BalanceIncreased = "موجودی حساب کاربر افزایش یافت.";
        public const string BalanceDecreased = "موجودی حساب کاربر کاهش یافت.";

        // 🔹 System
        public const string ErrorOccurred = "خطایی در سیستم رخ داد.";
        public const string Info = "اطلاعات سیستمی ثبت شد.";
        public const string Warning = "هشدار سیستمی ثبت شد.";
    }
}
