using DayvpnBotWebApi.Shared;
using System.Collections.Concurrent;

namespace DayvpnBotWebApi.Services
{
    public static class CustomMemoryCash
    {
        private static ConcurrentDictionary<long, UserClass> _users = new();
        private static readonly int _expireTimeMinutes = 10;
        private class UserClass
        {
            public UserState State { get; set; } = UserState.None;
            public BalanceClass? BalanceRequest { get; set; } = null;
            public SubscriptionClass? Subscription { get; set; } = null;
            public long? TelegramId { get; set; } = null;
            public DateTime CashExpireDateTime { get; set; } = DateTime.Now.AddMinutes(_expireTimeMinutes);
        }

        private class SubscriptionClass
        {
            public string SubscriptionName { get; set; } = string.Empty;
            public int ServiceId { get; set; }
        }

        private class BalanceClass
        {
            public decimal Balance { get; set; } = 0;
            public byte[]? PaymentImage { get; set; }
        }

        public static long? GetAssignedTelegramIdForSendConfig(long userId)
        {
            if (_users.TryGetValue(userId, out var user) && user.TelegramId.HasValue)
                return user.TelegramId.Value;
            return null;
        }

        public static void AssignAdminToSendConfig(long adminUserId, long userTelegramId)
        {
            var user = new UserClass
            {
                State = UserState.Send_User_Config,
                TelegramId = userTelegramId,
            };

            _users.AddOrUpdate(adminUserId, user, (key, existing) => user);
        }

        public static void ClearExpiredCash()
        {
            foreach (var item in _users)
            {
                if (item.Value.CashExpireDateTime < DateTime.Now)
                {
                    if (_users.TryRemove(item.Key, out _))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("🧹 Expired Cash Removed: ");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"UserId = {item.Key}, ExpiredAt = {item.Value.CashExpireDateTime:yyyy-MM-dd HH:mm:ss}");
                        Console.ResetColor();
                    }
                }
            }
        }

        public static void ClearCash(long userId)
        {
            if (_users.TryRemove(userId, out _))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("🧹 User Cash Removed: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"UserId = {userId}");
                Console.ResetColor();
            }
        }

        public static void RefreshCashExpireTime(long userId)
        {
            if (_users.TryGetValue(userId, out var user))
            {
                user.CashExpireDateTime = DateTime.Now.AddMinutes(_expireTimeMinutes);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"⏳ CashExpireTime Refreshed | UserId: {userId} | New Expire: {user.CashExpireDateTime:HH:mm:ss}");
                Console.ResetColor();
            }
        }

        public static void AddSubscription(long userId, int serivceId)
        {
            var user = new UserClass
            {
                State = UserState.Buy_Subscription,
                Subscription = new SubscriptionClass()
                {
                    ServiceId = serivceId
                }
            };

            _users.AddOrUpdate(userId, user, (key, existing) => user);
            RefreshCashExpireTime(userId);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"📦 Subscription Request Added | UserId: {userId} | ServiceId: {serivceId}");
            Console.ResetColor();
        }

        public static void AddBalance(long userId)
        {
            var user = new UserClass
            {
                State = UserState.Increase_Balance,
            };

            _users.AddOrUpdate(userId, user, (key, existing) => user);
            RefreshCashExpireTime(userId);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"💰 Balance Request Added | UserId: {userId}");
            Console.ResetColor();
        }

        public static bool HasSubscription(long userId)
        {
            var result = _users.TryGetValue(userId, out var user) && user.Subscription != null;

            Console.WriteLine($"🔎 HasSubscription Check | UserId: {userId} | Result: {result}");
            return result;
        }

        public static bool HasBalanceRequest(long userId)
        {
            var result = _users.TryGetValue(userId, out var user) && user.BalanceRequest != null;

            Console.WriteLine($"🔎 HasBalanceRequest Check | UserId: {userId} | Result: {result}");
            return result;
        }

        public static void SubmitSubscriptionName(long userId, string subName)
        {
            if (_users.TryGetValue(userId, out var user) && user.Subscription != null)
            {
                user.Subscription.SubscriptionName = subName;
                RefreshCashExpireTime(userId);

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"✏️ Subscription Name Updated | UserId: {userId} | New Name: {subName}");
                Console.ResetColor();
            }
        }

        public static string GetRequestedBalanceAmount(long userId)
        {
            if (_users.TryGetValue(userId, out var user) && user.BalanceRequest != null)
            {
                Console.WriteLine($"📤 GetRequestedBalance | UserId: {userId} | Amount: {user.BalanceRequest.Balance}");
                return user.BalanceRequest.Balance.ToString("N0");
            }

            Console.WriteLine($"⚠️ GetRequestedBalance | UserId: {userId} | No Balance Found");
            return "مبلغ نا مشخص! لطفا با ادمین هماهنگ کنید.. \n\n @DarvyXe";
        }

        public static void SubmitPaymentPicture(long userId, byte[] imageData)
        {
            if (_users.TryGetValue(userId, out var user) && user.BalanceRequest != null)
            {
                user.BalanceRequest.PaymentImage = imageData;
                RefreshCashExpireTime(userId);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Payment Image Submitted | UserId: {userId}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Payment Image Submit Failed | UserId: {userId} Not Found");
                Console.ResetColor();
            }
        }

        public static void SetUserState(long userId, UserState state)
        {
            if (_users.TryGetValue(userId, out var user))
            {
                user.State = state;
                RefreshCashExpireTime(userId);

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"🔄 User State Set | UserId: {userId} | State: {state}");
                Console.ResetColor();
            }
        }

        public static UserState? GetUserState(long userId)
        {
            if (_users.TryGetValue(userId, out var user))
            {
                Console.WriteLine($"📍 GetUserState | UserId: {userId} | State: {user.State}");
                return user.State;
            }

            Console.WriteLine($"📍 GetUserState | UserId: {userId} Not Found");
            return null;
        }

        public static void SetBalance(long userId, decimal balance)
        {
            if (_users.TryGetValue(userId, out var user) && user.State == UserState.Increase_Balance)
            {
                user.BalanceRequest = new BalanceClass
                {
                    Balance = balance,
                };

                RefreshCashExpireTime(userId);

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"💳 Balance Set | UserId: {userId} | Amount: {balance}");
                Console.ResetColor();
            }
        }

        public static BalanceClassDTO? GetBalanceRequest(long userId)
        {
            if (_users.TryGetValue(userId, out var user) &&
                user.State == UserState.Increase_Balance &&
                user.BalanceRequest != null)
            {
                Console.WriteLine($"📤 GetBalanceRequest | UserId: {userId} | Amount: {user.BalanceRequest.Balance}");
                return new BalanceClassDTO()
                {
                    UserId = userId,
                    Balance = user.BalanceRequest.Balance,
                };
            }

            Console.WriteLine($"⚠️ GetBalanceRequest | UserId: {userId} | No Balance Request Found");
            return null;
        }

        public static SubscriptionClassDTO? GetSubscriptionRequest(long userId)
        {
            if (_users.TryGetValue(userId, out var user) && user.Subscription != null)
            {
                Console.WriteLine($"📦 GetSubscriptionRequest | UserId: {userId} | ServiceId: {user.Subscription.ServiceId} | Name: {user.Subscription.SubscriptionName}");
                return new SubscriptionClassDTO()
                {
                    UserId = userId,
                    ServiceId = user.Subscription.ServiceId,
                    SubscriptionName = user.Subscription.SubscriptionName
                };
            }
            Console.WriteLine($"⚠️ GetSubscriptionRequest | UserId: {userId} | No Subscription Request Found");
            return null;
        }
    }
}
