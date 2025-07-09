using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DayvpnBotWebApi.Services
{
    public class UserService
    {
        private readonly AppDbContext _db;
        private readonly AppLogService _appLogService;
        public UserService(AppDbContext db, AppLogService appLogService)
        {
            _db = db;
            _appLogService = appLogService;
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _db.Users.Include(u => u.Subscriptions).ToListAsync();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _db.Users.Include(u => u.Subscriptions)
                                  .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<ServiceResult> CreateAsync(User model)
        {
            try
            {
                _db.Users.Add(model);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                {
                    return await _appLogService.LogUserRegisteredAsync(model.FirstName, model.LastName, model.Id);
                }
                return ServiceResult.Failed("خطا در ایجاد کاربر.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در ایجاد کاربر: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در ایجاد کاربر: {ex.Message}");
            }
        }

        public async Task<ServiceResult> UpdateAsync(User model)
        {
            try
            {
                _db.Users.Update(model);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("کاربر با موفقیت بروزرسانی شد.");
                return ServiceResult.Failed("خطا در بروزرسانی کاربر.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در بروزرسانی کاربر: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در بروزرسانی کاربر: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return ServiceResult.Failed("کاربر یافت نشد.");

                _db.Users.Remove(user);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("کاربر با موفقیت حذف شد.");
                return ServiceResult.Failed("خطا در حذف کاربر.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در حذف کاربر: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در حذف کاربر: {ex.Message}");
            }
        }

        public async Task<ServiceResult> RegisterUser(User user)
        {
            return await CreateAsync(user);
        }

        public async Task<bool> CheckUserExists(long telegramId)
        {
            return await _db.Users.AnyAsync(c => c.TelegramId == telegramId);
        }

        public async Task<ServiceResult<decimal>> AddUserBalanceAsync(BalanceClassDTO balanceRequest)
        {
            var user = await _db.Users.FirstOrDefaultAsync(c => c.TelegramId == balanceRequest.UserId);
            if (user == null)
            {
                await _appLogService.LogAddBalanceFailureAsync(user, balanceRequest.Balance, "کاربر در دیتابیس یافت نشد.");
                return ServiceResult<decimal>.Failed("❌ کاربر مورد نظر برای افزایش موجودی پیدا نشد.");
            }

            var oldBalance = user.Balance;
            user.Balance += balanceRequest.Balance;

            try
            {
                await _db.SaveChangesAsync();
                await _appLogService.LogAddBalanceSuccessAsync(user, oldBalance, balanceRequest.Balance, user.Balance);
                CustomMemoryCash.ClearCash(user.TelegramId);
                return ServiceResult<decimal>.Success(user.Balance, "✅ موجودی کاربر با موفقیت افزایش یافت.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogAddBalanceFailureAsync(user, balanceRequest.Balance, $"خطا در ذخیره اطلاعات: {ex.Message}");
                return ServiceResult<decimal>.Failed("❌ خطا هنگام افزایش موجودی کاربر.");
            }
        }

        public async Task<UserProfileDTO?> GetUserProfileByTelegramIdAsync(long telegramId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(c => c.TelegramId == telegramId);

            if (user == null)
                return null;

            var subscriptionCount = await _db.Subscriptions.Where(c => c.UserId == user.Id).CountAsync();

            return new UserProfileDTO()
            {
                FullName = user.FirstName + " " + user.LastName,
                TelegramId = user.TelegramId,
                Balance = user.Balance,
                RegisterDate = user.RegistrationDate,
                SubscriptionCount = subscriptionCount
            };
        }
    }
}
