
using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Services
{
    public class ServicesService
    {
        private readonly AppDbContext _db;
        public ServicesService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<Service>> GetAllAsync()
        {
            return await _db.Services.Where(c => c.IsActive).ToListAsync();
        }

        public async Task<Service> GetByIdAsync(int id)
        {
            return await _db.Services.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
        }

        public async Task<decimal> GetPriceAsync(int id)
        {
            return await _db.Services
                .Where(c => c.Id == id && c.IsActive)
                .Select(c => c.Price)
                .FirstOrDefaultAsync();
        }
    }
}
