using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Services
{
    public class SubscriptionService
    {
        private readonly AppDbContext _db;
        private readonly AppLogService _appLogService;

        public SubscriptionService(AppDbContext db, AppLogService appLogService)
        {
            _db = db;
            _appLogService = appLogService;
        }

        public async Task<List<Subscription>> GetAllAsync()
        {
            return await _db.Subscriptions.Include(s => s.SubscriptionLinks).ToListAsync();
        }

        public async Task<Subscription?> GetByIdAsync(int id)
        {
            return await _db.Subscriptions.Include(s => s.SubscriptionLinks)
                                          .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<ServiceResult> CreateAsync(Subscription model)
        {
            try
            {
                _db.Subscriptions.Add(model);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("اشتراک با موفقیت ایجاد شد.");
                return ServiceResult.Failed("خطا در ایجاد اشتراک.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در ایجاد اشتراک: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در ایجاد اشتراک: {ex.Message}");
            }
        }

        public async Task<ServiceResult> UpdateAsync(Subscription model)
        {
            try
            {
                _db.Subscriptions.Update(model);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("اشتراک با موفقیت بروزرسانی شد.");
                return ServiceResult.Failed("خطا در بروزرسانی اشتراک.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در بروزرسانی اشتراک: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در بروزرسانی اشتراک: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var sub = await _db.Subscriptions.FindAsync(id);
                if (sub == null)
                    return ServiceResult.Failed("اشتراک یافت نشد.");

                _db.Subscriptions.Remove(sub);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("اشتراک با موفقیت حذف شد.");
                return ServiceResult.Failed("خطا در حذف اشتراک.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در حذف اشتراک: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در حذف اشتراک: {ex.Message}");
            }
        }
    }


}
