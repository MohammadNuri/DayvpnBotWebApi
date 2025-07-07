using DayvpnBotWebApi.Shared;

namespace DayvpnBotWebApi.Core.Entities
{
    public class AppLog : BaseEntity
    {
        // کاربری که این عملیات را انجام داده
        public int? UserId { get; set; }
        public User? User { get; set; }

        // در صورت مربوط بودن به اشتراک خاص
        public int? SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }

        // نوع رویداد (ثبت‌نام، خرید، تمدید، خطا، و ...)
        public LogEventType EventType { get; set; }

        // توضیح متن کامل اتفاق (برای نمایش به ادمین یا ذخیره توضیح قابل فهم)
        public string Message { get; set; } = string.Empty;

        // جزئیات فنی یا داده‌ای (مثلاً JSON از request یا پارامترها)
        public string? Metadata { get; set; }

        // IP یا اطلاعات مرورگر/کلاینت
        public string? ClientInfo { get; set; }
    }
}
