using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Services
{
    public class SubscriptionRequestService
    {
        private readonly AppDbContext _db;
        public SubscriptionRequestService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ServiceResult<SubscriptionRequest>> CreateAsync(SubscriptionRequest entity)
        {
            try
            {
                await _db.SubscriptionRequests.AddAsync(entity);
                await _db.SaveChangesAsync();
                return ServiceResult<SubscriptionRequest>.Success(entity, "");
            }
            catch (Exception ex)
            {
                return ServiceResult<SubscriptionRequest>.Failed(ex.Message);
                throw;
            }
        }

        public async Task<SubscriptionCacheClass?> GetByUserIdAsync(long userId)
        {
            var q = await _db.SubscriptionRequests
                .Include(c => c.Service)
                .Where(c => c.User.TelegramId == userId && c.Status == Status.InProgress)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (q != null)
                return new SubscriptionCacheClass()
                {
                    SubscriptionRequestId = q.Id,
                    ServiceId = q.ServiceId,
                    AllowedUsersCount = q.Service.AllowedUsersCount,
                    DataQuotaGB = q.Service.DataQuotaGB,
                    DurationInDays = q.Service.DurationInDays,
                    Name = q.Service.Name,
                    Price = q.Service.Price,
                    RequestedSubscriptioName = q.SubscriptionName,
                };

            return null;
        }

        public async Task<bool> ExistsAsync(long userId)
        {
            return await _db.SubscriptionRequests
                .AnyAsync(c => c.User.TelegramId == userId && c.Status == Status.InProgress);
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.SubscriptionRequests.FindAsync(id);
            if (entity != null)
            {
                _db.SubscriptionRequests.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
