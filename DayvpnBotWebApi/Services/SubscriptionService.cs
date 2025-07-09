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
        private readonly ServicesService _servicesService;
        public SubscriptionService(AppDbContext db, AppLogService appLogService, ServicesService servicesService)
        {
            _db = db;
            _appLogService = appLogService;
            _servicesService = servicesService;
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

        public async Task<ServiceResult<SubscriptionResultDto>> InsertSubscription(long telegramId)
        {
            var subscriptionRequest = CustomMemoryCash.GetSubscriptionRequest(telegramId);
            if (subscriptionRequest == null)
                return ServiceResult<SubscriptionResultDto>.Failed("""
                ❌ *خطا در پردازش درخواست شما!*

                ℹ️ اطلاعات شما در حافظه یافت نشد.  
                این ممکن است به دلیل گذشت زمان یا بروز خطا در مراحل قبلی باشد.

                لطفاً مجدداً تلاش کنید یا با پشتیبانی تماس بگیرید.

                🆘 پشتیبانی: @DarvyXe
                """);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.TelegramId == telegramId);
            if (user == null)
                return ServiceResult<SubscriptionResultDto>.Failed("""
                ❌ *کاربر یافت نشد!*

                🔍 ما نتوانستیم اطلاعات شما را در سیستم پیدا کنیم.  
                ممکن است ثبت‌نام شما کامل نشده باشد یا مشکلی در ارتباط با سرور رخ داده باشد.

                لطفاً برای بررسی بیشتر با پشتیبانی در تماس باشید.

                🆘 پشتیبانی: @DarvyXe
                """);

            var service = await _servicesService.GetByIdAsync(subscriptionRequest.ServiceId);

            if (service.Price > user.Balance)
                return ServiceResult<SubscriptionResultDto>.Failed("""
                ❌ *موجودی کافی نیست!*

                💳 موجودی فعلی شما برای خرید این سرویس کافی نیست.

                لطفاً ابتدا *موجودی خود را افزایش دهید* و سپس دوباره اقدام به خرید کنید.

                برای افزایش موجودی، از گزینه‌های موجود در منوی ربات استفاده کنید یا به پشتیبانی پیام دهید.  
                🆘 پشتیبانی: @DarvyXe
                """);

            try
            {
                var subscription = new Subscription
                {
                    SubscriptionCode = Guid.NewGuid().ToString(),
                    SubscriptionName = subscriptionRequest.SubscriptionName,
                    SubscriptionVolumeGb = service.DataQuotaGB,
                    UserId = user.Id,
                    ServiceId = subscriptionRequest.ServiceId,
                    ActivationDate = DateTime.UtcNow,
                    ExpirationDate = DateTime.UtcNow.AddDays(service.DurationInDays),
                    IsActive = true
                };

                await _db.Subscriptions.AddAsync(subscription);

                user.Balance -= service.Price;

                await _db.SaveChangesAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Subscription خریداری شد: UserId={user.Id}, ServiceId={service.Id}, Volume={service.DataQuotaGB}GB");
                Console.ResetColor();

                await _appLogService.LogAddSubscriptionSuccessAsync(user.Id, subscription.SubscriptionName, subscription.ExpirationDate);

                CustomMemoryCash.ClearCash(telegramId);
                return ServiceResult<SubscriptionResultDto>.Success(new SubscriptionResultDto()
                {
                    UserFullName = user.FirstName + " " + user.LastName,
                    TelegramId = user.TelegramId,
                    NewBalance = user.Balance,
                    PurchasedAt = DateTime.UtcNow,
                    ServiceName = service.Name,
                    VolumeGb = service.DataQuotaGB,
                    DurationDays = service.DurationInDays,
                    UserCount = service.AllowedUsersCount,
                    Price = service.Price,
                }, """
                🎉 *درخواست خرید شما با موفقیت ثبت شد!*

                🕓 لطفاً کمی صبر کنید تا سرویس اختصاصی شما ارسال شود.  
                در صورت بروز تأخیر یا مشکل، حتماً با پشتیبانی در تماس باشید.

                🆘 پشتیبانی: @DarvyXe
                """);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ خطا در خرید Subscription: {ex.Message}");
                Console.ResetColor();

                await _appLogService.LogAddSubscriptionFailureAsync(user?.Id ?? 0, ex.Message);
                CustomMemoryCash.ClearCash(telegramId);

                return ServiceResult<SubscriptionResultDto>.Failed("""
                ❌ *خطایی هنگام ثبت خرید شما رخ داد!*

                لطفاً مجدداً تلاش کنید.  
                در صورت تکرار خطا، مشکل را با پشتیبانی در میان بگذارید.

                🆘 پشتیبانی: @DarvyXe
                """);
            }
        }
    }
}
