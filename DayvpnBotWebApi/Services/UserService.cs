using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;

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
                    return await _appLogService.LogUserRegisteredAsync(model.FirstName, model.LastName, (int)model.TelegramId);
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
    }
}
