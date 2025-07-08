using DayvpnBotWebApi.Shared;
using System.Diagnostics.Eventing.Reader;

namespace DayvpnBotWebApi.Services
{
    public static class CustomMemoryCash
    {
        private static List<UserClass> _users = new List<UserClass>();

        private class UserClass
        {
            public long TelegramId { get; set; }
            public UserState State { get; set; } = UserState.None;
            public BalanceClass? BalanceRequest { get; set; }
            public SubscriptionClass? Subscription { get; set; } = null;
        }

        private class SubscriptionClass
        {
            public string SubName { get; set; } = string.Empty;
            public SubMode SubMode { get; set; }
        }

        private class BalanceClass
        {
            public decimal Balance { get; set; } = 0;
            public byte[]? PaymentImage { get; set; }        }

        public static void AddSubscription(long userId, string subName, SubMode subMode)
        {
            _users.Add(new UserClass
            {
                TelegramId = userId,
                State = UserState.Buy_Subscription,
                Subscription = new SubscriptionClass()
                {
                    SubMode = subMode,
                    SubName = subName,
                }
            });
        }

        public static void AddBalance(long userId)
        {
            _users.Add(new UserClass
            {
                TelegramId = userId,
                State = UserState.Increase_Balance,
            });
        }

        public static bool HasSubscription(long userId)
        {
            return _users.Any(x => x.TelegramId == userId && x.Subscription != null);
        }

        public static bool HasBalanceRequest(long userId)
        {
            return _users.Any(x => x.TelegramId == userId && x.BalanceRequest != null);
        }

        public static void SubmitSubscriptionName(long userId, string subName)
        {
            var sub = _users.Where(c => c.TelegramId == userId).Select(c => c.Subscription).FirstOrDefault();
            if (sub != null)
                sub.SubName = subName;
        }

        public static string GetRequestedBalance(long userId)
        {
            var balanceRequest = _users.Where(c => c.TelegramId == userId).Select(c => c.BalanceRequest).FirstOrDefault();
            if (balanceRequest != null)
                return balanceRequest.Balance.ToString("N0");
            return "مبلغ نا مشخص! لطفا با ادمین هماهنگ کنید.. \n\n @DarvyXe";
        }

        public static void SubmitPaymentPicture(long userId, byte[] imageData)
        {
            var userSub = _users.Where(c => c.TelegramId == userId).Select(c => c.BalanceRequest).FirstOrDefault();
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

        public static void SetUserState(long userId, UserState state)
        {
            var user = _users.Where(c => c.TelegramId == userId).FirstOrDefault();
            if (user != null)
                user.State = state;

            Console.WriteLine($"SetUserState has been Called | UserId: {userId} | State: {state}");
        }

        public static UserState? GetUserState(long userId)
        {
            var user = _users.Where(c => c.TelegramId == userId).FirstOrDefault();
            if (user != null)
                return user.State;
            return null;
        }
        
        public static void SetBalance(long userId, decimal balance)
        {
            var user = _users.Where(c => c.TelegramId == userId).FirstOrDefault();
            if(user != null && user.State == UserState.Increase_Balance)
            {
                user.BalanceRequest = new BalanceClass
                {
                    Balance = balance,
                };
            }
        }

        public static BalanceClassDTO? GetBalanceRequest(long userId)
        {
            var user = _users.Where(c => c.TelegramId == userId).FirstOrDefault();
            if (user != null && user.State == UserState.Increase_Balance && user.BalanceRequest != null)
            {
                return new BalanceClassDTO()
                {
                    UserId = user.TelegramId,
                    Balance = user.BalanceRequest.Balance,
                };
            }
            return null;
        }
    }
}
