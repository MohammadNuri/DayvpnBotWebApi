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


        public async Task<ServiceResult> LogUserRegisteredAsync(string firstName, string lastName, int userId, string? metadata = null)
        {
            try
            {
                string fullName = $"{firstName} {lastName}".Trim();
                string message = $"کاربر جدید با نام {fullName} و شناسه {userId} با موفقیت ثبت‌نام کرد.";

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

        public async Task LogAddBalanceSuccessAsync(int userId, decimal balance)
        {
            var log = new AppLog
            {
                UserId = userId,
                EventType = LogEventType.BalanceIncreased,
                Message = $"موجودی کاربر با موفقیت به {balance:N0} تومان افزایش یافت.",
                CreatedAt = DateTime.Now
            };

            await CreateLogAsync(log);
        }

        public async Task LogAddBalanceFailureAsync(int userId, string errorMessage)
        {
            var log = new AppLog
            {
                UserId = userId,
                EventType = LogEventType.Error,
                Message = $"خطا در افزایش موجودی: {errorMessage}",
                CreatedAt = DateTime.Now
            };

            await CreateLogAsync(log);
        }
    }
}
