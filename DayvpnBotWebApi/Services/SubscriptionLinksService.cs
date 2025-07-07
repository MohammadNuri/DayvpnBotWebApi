using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Services
{
    public class SubscriptionLinksService
    {
        private readonly AppDbContext _db;
        private readonly AppLogService _appLogService;

        public SubscriptionLinksService(AppDbContext db, AppLogService appLogService)
        {
            _db = db;
            _appLogService = appLogService;
        }

        public async Task<List<SubscriptionLinks>> GetAllAsync()
        {
            return await _db.SubscriptionLinks.ToListAsync();
        }

        public async Task<List<SubscriptionLinks>> GetBySubscriptionIdAsync(int subscriptionId)
        {
            return await _db.SubscriptionLinks.Where(x => x.SubscriptionId == subscriptionId).ToListAsync();
        }

        public async Task<SubscriptionLinks?> GetByIdAsync(int id)
        {
            return await _db.SubscriptionLinks.FindAsync(id);
        }

        public async Task<ServiceResult> CreateAsync(SubscriptionLinks model)
        {
            try
            {
                _db.SubscriptionLinks.Add(model);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("لینک اشتراک با موفقیت ایجاد شد.");
                return ServiceResult.Failed("خطا در ایجاد لینک اشتراک.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در ایجاد لینک اشتراک: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در ایجاد لینک اشتراک: {ex.Message}");
            }
        }

        public async Task<ServiceResult> UpdateAsync(SubscriptionLinks model)
        {
            try
            {
                _db.SubscriptionLinks.Update(model);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("لینک اشتراک با موفقیت بروزرسانی شد.");
                return ServiceResult.Failed("خطا در بروزرسانی لینک اشتراک.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در بروزرسانی لینک اشتراک: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در بروزرسانی لینک اشتراک: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var link = await _db.SubscriptionLinks.FindAsync(id);
                if (link == null)
                    return ServiceResult.Failed("لینک اشتراک یافت نشد.");

                _db.SubscriptionLinks.Remove(link);
                var affected = await _db.SaveChangesAsync();
                if (affected > 0)
                    return ServiceResult.Success("لینک اشتراک با موفقیت حذف شد.");
                return ServiceResult.Failed("خطا در حذف لینک اشتراک.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogErrorAsync(
                    $"خطا در حذف لینک اشتراک: {ex.Message}",
                    metadata: ex.ToString());

                return ServiceResult.Failed($"خطا در حذف لینک اشتراک: {ex.Message}");
            }
        }
    }


}
