using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Services
{
    public class TransactionService
    {
        private readonly AppDbContext _db;

        public TransactionService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ServiceResult> CreateAsync(Transaction entity)
        {
            await _db.Transactions.AddAsync(entity);
            await _db.SaveChangesAsync();
            return ServiceResult.Success();
        }

        public async Task<Transaction?> GetByIdAsync(int id)
        {
            return await _db.Transactions.FindAsync(id);
        }

        public async Task<List<Transaction>> GetAllByUserIdAsync(int userId)
        {
            return await _db.Transactions
                .Where(x => x.UserId == userId && !x.IsRemoved)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<ServiceResult> UpdateStatusAsync(int id, TransactionStatus status)
        {
            var transaction = await _db.Transactions.FindAsync(id);
            if (transaction == null) return ServiceResult.Failed("تراکنش یافت نشد.");

            transaction.Status = status;
            transaction.UpdatedAt = DateTime.UtcNow;

            _db.Update(transaction);
            await _db.SaveChangesAsync();
            return ServiceResult.Success();
        }
    }
}
