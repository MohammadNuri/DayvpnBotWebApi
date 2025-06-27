using DayvpnBotWebApi.Shared;
using System.Diagnostics.Eventing.Reader;

namespace DayvpnBotWebApi.Services
{
    public static class SubscriptionHelper
    {
        private static List<SubClass> _subs = new List<SubClass>();
        private class SubClass
        {
            public long UserId { get; set; }
            public string SubName { get; set; } = string.Empty;
            public SubMode SubMode { get; set; }
            public byte[]? PaymentImage { get; set; } // عکس فیش واریزی
        }

        public static void AddSub(long userId, string subName, SubMode subMode)
        {
            _subs.Add(new SubClass
            {
                UserId = userId,
                SubName = subName,
                SubMode = subMode
            });
        }

        public static bool HasSub(long userId)
        {
            return _subs.Any(x => x.UserId == userId);
        }

        public static void SubmitSubName(long userId, string subName)
        {
            var sub = _subs.Where(c => c.UserId == userId).FirstOrDefault();
            if (sub != null)
                sub.SubName = subName;
        }

        public static string GetSubCost(long userId)
        {
            var sub = _subs.Where(c => c.UserId == userId).FirstOrDefault();
            if (sub != null)
                return ((int)sub.SubMode).ToString();
            return "مبلغ نا مشخص! لطفا با ادمین هماهنگ کنید.. \n\n @DarvyXe";
        }

        public static void SubmitPaymentPicture(long userId, byte[] imageData)
        {
            var userSub = _subs.FirstOrDefault(s => s.UserId == userId);
            if (userSub != null)
            {
                userSub.PaymentImage = imageData;
                Console.WriteLine($"✅ عکس پرداخت برای کاربر {userId} ذخیره شد.");
            }
            else
            {
                Console.WriteLine($"❌ کاربر {userId} یافت نشد.");
            }
        }
    }
}
