using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Services
{
    public class AppLogService
    {
        private readonly AppDbContext _db;

        public AppLogService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<AppLog>> GetAllAsync()
        {
            return await _db.AppLogs.Include(x => x.User)
                                    .Include(x => x.Subscription)
                                    .OrderByDescending(x => x.CreatedAt)
                                    .ToListAsync();
        }

        public async Task<AppLog?> GetByIdAsync(int id)
        {
            return await _db.AppLogs.Include(x => x.User)
                                    .Include(x => x.Subscription)
                                    .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<ServiceResult> CreateLogAsync(AppLog model)
        {
            try
            {
                _db.AppLogs.Add(model);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("لاگ با موفقیت ثبت شد.");
                return ServiceResult.Failed("خطا در ثبت لاگ.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"خطا در ثبت لاگ: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var log = await _db.AppLogs.FindAsync(id);
                if (log == null)
                    return ServiceResult.Failed("لاگ یافت نشد.");

                _db.AppLogs.Remove(log);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("لاگ با موفقیت حذف شد.");
                return ServiceResult.Failed("خطا در حذف لاگ.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"خطا در حذف لاگ: {ex.Message}");
            }
        }

        public async Task<ServiceResult> LogErrorAsync(string message, string? metadata = null, int? userId = null, int? subscriptionId = null)
        {
            try
            {
                var log = new AppLog
                {
                    EventType = LogEventType.Error,
                    Message = message,
                    Metadata = metadata,
                    UserId = userId,
                    SubscriptionId = subscriptionId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.AppLogs.Add(log);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("خطای لاگ شده.");
                return ServiceResult.Failed("خطا در ثبت لاگ.");
            }
            catch (Exception ex)
            {
                // اگر اینجا خطا بود، احتمالاً مشکل جدی هست و میشه فیلتر کرد یا لاگ فایلی زد
                return ServiceResult.Failed($"خطا در ثبت لاگ: {ex.Message}");
            }
        }


        public async Task<ServiceResult> LogUserRegisteredAsync(string firstName, string lastName, int userId, long telegramId, string? metadata = null)
        {
            try
            {
                string fullName = $"{firstName} {lastName}".Trim();
                string message = $"✅ کاربر جدید با موفقیت ثبت‌نام شد!\n\n" +
                                 $"👤 نام: {fullName}\n" +
                                 $"🆔 شناسه داخلی: {userId}\n" +
                                 $"📱 شناسه تلگرام: {telegramId}\n\n";

                var log = new AppLog
                {
                    EventType = LogEventType.UserRegistered,
                    Message = message,
                    Metadata = metadata,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.AppLogs.Add(log);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success(message);
                return ServiceResult.Failed($"خطا در ثبت لاگ ثبت نام. {firstName + lastName + userId}");
            }
            catch (Exception ex)
            {
                // اگر اینجا خطا بود، احتمالاً مشکل جدی هست و میشه فیلتر کرد یا لاگ فایلی زد
                return ServiceResult.Failed($"خطا در ثبت لاگ: {ex.Message}");
            }
        }

        public async Task LogAddBalanceSuccessAsync(User user, decimal oldBalance, decimal requestedBalance, decimal newBalance)
        {
            var log = new AppLog
            {
                UserId = user.Id,
                EventType = LogEventType.BalanceIncreased,
                Message = $"""
                    ✅ افزایش موجودی با موفقیت انجام شد.

                    👤 کاربر: {user.FirstName} {user.LastName}
                    🆔 Telegram ID: {user.TelegramId}
                    📱 شماره موبایل: {user.MobileNumber}

                    🔢 مبلغ درخواستی: {requestedBalance:N0} تومان
                    💰 موجودی قبلی: {oldBalance:N0} تومان
                    ➕ افزایش داده شده: {requestedBalance:N0} تومان
                    💳 موجودی جدید: {newBalance:N0} تومان

                    📅 زمان ثبت: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    """,
                CreatedAt = DateTime.Now
            };

            await CreateLogAsync(log);
        }

        public async Task LogDeductBalanceSuccessAsync(User user, decimal oldBalance, decimal deductedAmount, decimal newBalance, string reason)
        {
            var log = new AppLog
            {
                UserId = user.Id,
                EventType = LogEventType.BalanceDecreased,
                Message = $"""
                    ⚠️ کاهش موجودی با موفقیت انجام شد.

                    👤 کاربر: {user.FirstName} {user.LastName}
                    🆔 Telegram ID: {user.TelegramId}
                    📱 شماره موبایل: {user.MobileNumber}

                    💳 موجودی قبلی: {oldBalance:N0} تومان
                    ➖ مبلغ کسر شده: {deductedAmount:N0} تومان
                    💰 موجودی جدید: {newBalance:N0} تومان

                    📘 دلیل: {reason}

                    📅 زمان ثبت: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    """,
                CreatedAt = DateTime.Now
            };

            await CreateLogAsync(log);
        }

        public async Task LogAddBalanceFailureAsync(User user, decimal requestedBalance, string errorMessage)
        {
            var log = new AppLog
            {
                UserId = user.Id,
                EventType = LogEventType.Error,
                Message = $"""
                ❌ خطا در افزایش موجودی!

                👤 کاربر: {user.FirstName} {user.LastName}
                🆔 Telegram ID: {user.TelegramId}
                📱 شماره موبایل: {user.MobileNumber}

                🔢 مبلغ درخواستی: {requestedBalance:N0} تومان
                ⚠️ خطا: {errorMessage}

                📅 زمان ثبت: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                """,
                CreatedAt = DateTime.Now
            };

            await CreateLogAsync(log);
        }

        public async Task LogAddSubscriptionSuccessAsync(User user, Subscription subscription, decimal oldBalance, decimal reducedBalance, decimal newBalance)
        {
            var log = new AppLog
            {
                UserId = user.Id,
                EventType = LogEventType.SubscriptionActivated,
                Message = $"""
                ✅ خرید اشتراک با موفقیت انجام شد.

                👤 کاربر: {user.FirstName} {user.LastName}
                🆔 Telegram ID: {user.TelegramId}
                📱 شماره موبایل: {user.MobileNumber}

                🔖 نام اشتراک: {subscription.SubscriptionName}
                📦 حجم: {subscription.SubscriptionVolumeGb} گیگابایت
                👥 تعداد کاربران مجاز: {subscription.Service.AllowedUsersCount} نفر
                ⏳ مدت زمان اشتراک: {subscription.Service.DurationInDays} روز
                📅 تاریخ فعال‌سازی: {subscription.ActivationDate?.ToString("yyyy-MM-dd HH:mm:ss")}
                📅 تاریخ انقضا: {subscription.ExpirationDate:yyyy-MM-dd HH:mm:ss}

                💳 مبلغ کسر شده: {reducedBalance:N0} تومان
                💰 موجودی قبلی: {oldBalance:N0} تومان
                💰 موجودی جدید: {newBalance:N0} تومان

                📅 زمان ثبت: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                """,
                CreatedAt = DateTime.Now
            };

            await CreateLogAsync(log);
        }

        public async Task LogAddSubscriptionFailureAsync(User user, string errorMessage)
        {
            var log = new AppLog
            {
                UserId = user.Id,
                EventType = LogEventType.Error,
                Message = $"""
                ❌ خطا در ثبت اشتراک برای کاربر!

                👤 کاربر: {user.FirstName} {user.LastName}
                🆔 Telegram ID: {user.TelegramId}
                📱 شماره موبایل: {user.MobileNumber}

                ⚠️ خطا: {errorMessage}

                📅 زمان ثبت: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                """,
                CreatedAt = DateTime.Now
            };

            await CreateLogAsync(log);
        }
    }
}
