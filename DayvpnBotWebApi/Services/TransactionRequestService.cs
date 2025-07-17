using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Services
{
    public class TransactionRequestService
    {
        private readonly AppDbContext _db;

        public TransactionRequestService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ServiceResult<TransactionRequest>> CreateAsync(TransactionRequest entity)
        {
            await _db.TransactionRequests.AddAsync(entity);
            await _db.SaveChangesAsync();
            return ServiceResult<TransactionRequest>.Success(entity, "");
        }

        public async Task<TransactionRequest?> GetByIdAsync(int id)
        {
            return await _db.TransactionRequests.FindAsync(id);
        }

        public async Task<List<TransactionRequest>> GetAllByUserIdAsync(int userId)
        {
            return await _db.TransactionRequests
                .Where(x => x.UserId == userId && !x.IsRemoved)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<TransactionRequest>> GetAllByUserIdAsync(long userId)
        {
            return await _db.TransactionRequests
                .Where(x => x.User.TelegramId == userId && !x.IsRemoved)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<TransactionRequest?> GetByTrackingCodeAsync(string trackingCode)
        {
            return await _db.TransactionRequests
                .FirstOrDefaultAsync(c => c.TrackingCode == trackingCode && !c.IsRemoved);
        }

        public async Task<ServiceResult> UpdateStatusAsync(int id, TransactionRequestStatus status)
        {
            var request = await _db.TransactionRequests.FindAsync(id);
            if (request == null) return ServiceResult.Failed("درخواست یافت نشد.");

            request.Status = status;
            request.UpdatedAt = DateTime.UtcNow;

            _db.Update(request);
            await _db.SaveChangesAsync();
            return ServiceResult.Success();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.TransactionRequests.FindAsync(id);
            if (entity == null)
                throw new Exception("Transaction request not found");

            _db.TransactionRequests.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}
