using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Telegram.Bot.Types;
using User = DayvpnBotWebApi.Core.Entities.User;

namespace DayvpnBotWebApi.Services
{
    public class UserService
    {
        private readonly AppDbContext _db;
        private readonly AppLogService _appLogService;
        private readonly TransactionService _transactionService;
        private readonly TransactionRequestService _transactionRequestService;
        private readonly RedisCacheManager _redisCache;

        public UserService(AppDbContext db, 
            AppLogService appLogService,
            TransactionService transactionService,
            TransactionRequestService transactionRequestService,
            RedisCacheManager redisCache)
        {
            _db = db;
            _appLogService = appLogService;
            _transactionService = transactionService;
            _transactionRequestService = transactionRequestService;
            _redisCache = redisCache;
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
                    return await _appLogService.LogUserRegisteredAsync(model.FirstName, model.LastName, model.Id, model.TelegramId);
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

        public async Task<ServiceResult> RegisterUser(Message message)
        {
            if (!await CheckUserExists(message.Chat.Id))
            {
                Core.Entities.User user = new Core.Entities.User()
                {
                    TelegramId = message.Chat.Id,
                    FirstName = message.Chat.FirstName ?? string.Empty,
                    LastName = message.Chat.LastName ?? string.Empty,
                    RegistrationDate = DateTime.UtcNow,
                    Balance = 0,
                };

                var result = await CreateAsync(user);

                Console.Write($"Success: {result.IsSuccess}");
                Console.Write($"Message: {result.Message}");

                return result;
            }

            return ServiceResult.Failed("کاربر وجود دارد.");
        }

        public async Task<bool> CheckUserExists(long telegramId)
        {
            return await _db.Users.AnyAsync(c => c.TelegramId == telegramId);
        }

        public async Task<ServiceResult<decimal>> AddUserBalanceAsync(WalletCacheClass walletCache, long userId, int transactionRequestId)
        {
            var requestBalance = walletCache.RequestBalance;

            var user = await _db.Users.FirstOrDefaultAsync(c => c.TelegramId == userId);
            if (user == null)
            {
                await _appLogService.LogAddBalanceFailureAsync(user, requestBalance, "کاربر در دیتابیس یافت نشد.");
                return ServiceResult<decimal>.Failed("❌ کاربر مورد نظر برای افزایش موجودی پیدا نشد.");
            }

            var oldBalance = user.Balance;
            user.Balance += requestBalance;

            try
            {
                await _db.SaveChangesAsync();
                await _appLogService.LogAddBalanceSuccessAsync(user, oldBalance, requestBalance, user.Balance);
                var transactionRequestReult = await _transactionRequestService.UpdateStatusAsync(transactionRequestId,TransactionRequestStatus.Approved);
                var transactionResult = await _transactionService.CreateAsync(new Transaction
                {
                    UserId = user.Id,
                    Type = TransactionType.Deposit,
                    Amount = requestBalance,
                    Description = $"افزایش موجودی به مبلغ {requestBalance:N0} تومان برای کاربر با شناسه {user.TelegramId} (UserId: {user.Id}) " +
                        $"از طریق {walletCache.PaymentMethod?.ToString() ?? "نامشخص"}، مربوط به درخواست پرداخت شماره #{transactionRequestId}",
                    PaymentMethod = walletCache.PaymentMethod,
                    PaymentImage = walletCache.PaymentImage,
                    Status = TransactionStatus.Approved
                });

                Console.WriteLine("=== Balance Top-Up Operation Results ===");

                Console.WriteLine("1. Transaction Request Status Update:");
                Console.WriteLine(transactionRequestReult.IsSuccess
                    ? $"✅ Success: {transactionRequestReult.Message}"
                    : $"❌ Failed: {transactionRequestReult.Message}");

                Console.WriteLine();

                Console.WriteLine("2. New Transaction Record Creation:");
                Console.WriteLine(transactionResult.IsSuccess
                    ? $"✅ Success: {transactionResult.Message}"
                    : $"❌ Failed: {transactionResult.Message}");

                Console.WriteLine("========================================");


                await _redisCache.InvalidateAsync(RedisKeys.Wallet(userId));

                return ServiceResult<decimal>.Success(user.Balance, "✅ موجودی کاربر با موفقیت افزایش یافت.");
            }
            catch (Exception ex)
            {
                await _appLogService.LogAddBalanceFailureAsync(user, requestBalance, $"خطا در ذخیره اطلاعات: {ex.Message}");
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
                Id = user.Id,
                FullName = user.FirstName + " " + user.LastName,
                TelegramId = user.TelegramId,
                Balance = user.Balance,
                RegisterDate = user.RegistrationDate,
                SubscriptionCount = subscriptionCount
            };
        }
    }
}
