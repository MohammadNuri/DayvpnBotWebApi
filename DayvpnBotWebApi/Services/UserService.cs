using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Services
{
    public class UserService
    {
        private readonly AppDbContext _context;
        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult> RegisterUser(User user)
        {
            await _context.User.AddAsync(user);

            try
            {
                await _context.SaveChangesAsync();
                return ServiceResult.Success($"{user.FirstName} {user.LastName} Registered Successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error registering user: {ex.Message} \n InnerExeption:{ex.InnerException?.Message}");
            }
        }

        public async Task<bool> CheckUserExists(long telegramId)
        {
            return await _context.User.AnyAsync(c => c.TelegramId == telegramId);
        }
    }
}
