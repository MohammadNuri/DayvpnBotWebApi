using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.VisualBasic;
using StackExchange.Redis;
using System.Text.Json;
using Telegram.Bot.Types;

namespace DayvpnBotWebApi.Services
{
    public class RedisCacheManager
    {
        private readonly IDistributedCache _cache;
        private readonly IDatabase _redisDb;
        private readonly AppDbContext _db;

        public RedisCacheManager(IDistributedCache cache, AppDbContext db, IConnectionMultiplexer redis)
        {
            _cache = cache;
            _db = db;
            _redisDb = redis.GetDatabase();
        }

        public async Task<List<T>> GetAllAsync<T>(string key, TimeSpan? cacheDuration = null)
            where T : BaseEntity
        {
            var cached = await _cache.GetStringAsync(key);
            if (!string.IsNullOrEmpty(cached))
                return JsonSerializer.Deserialize<List<T>>(cached)!;

            var data = await _db.Set<T>().ToListAsync();

            var json = JsonSerializer.Serialize(data);

            await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration ?? TimeSpan.FromMinutes(10),
            });

            return data;
        }

        public async Task<T?> GetByIdAsync<T>(string key, int id, TimeSpan? cacheDuration = null)
            where T : BaseEntity
        {
            var cached = await _cache.GetStringAsync(key);
            if (!string.IsNullOrEmpty(cached))
            {
                var list = JsonSerializer.Deserialize<List<T>>(cached)!;
                return list.FirstOrDefault(x => x.Id == id);
            }

            var data = await _db.Set<T>().ToListAsync();
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(data), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration ?? TimeSpan.FromMinutes(10),
            });

            return data.FirstOrDefault(c => c.Id == id);
        }

        public async Task<ServiceResult<Service>> CacheSelectedServiceForUserAsync(long userId, int serviceId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(c => c.TelegramId == userId);
            var subscriptionCount = await _db.Subscriptions.Where(c => c.UserId == user.Id).CountAsync();
            if (user == null)
                return ServiceResult<Service>.Failed("کاربر یافت نشد.");

            var service = await GetByIdAsync<Service>("services:list", serviceId);
            if (service == null)
                return ServiceResult<Service>.Failed("سرویس یافت نشد.");

            await SetAsync<UserCacheClass>(RedisKeys.User(userId), new UserCacheClass()
            {
                RealUserId = user.Id,
                UserId = userId,
                FullName = user.FirstName + " " + user.LastName,
                State = UserState.Buy_Subscription,
            });

            await SetAsync<SubscriptionCacheClass>(RedisKeys.Subscription(userId), new SubscriptionCacheClass()
            {
                ServiceId = serviceId,
                AllowedUsersCount = service.AllowedUsersCount,
                DataQuotaGB = service.DataQuotaGB,
                DurationInDays = service.DurationInDays,
                Name = service.Name,
                Price = service.Price,
            });

            return ServiceResult<Service>.Success(service);
        }

        public async Task<ServiceResult<T>> UpdateCacheAsync<T>(string key, Func<T, Task> updateFunc)
            where T : class
        {
            var data = await GetAsync<T>(key);
            if (data == null)
                return ServiceResult<T>.Failed();

            await updateFunc(data);
            await SetAsync(key, data);
            return ServiceResult<T>.Success(data);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? cacheDuration = null)
        {
            var json = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration ?? TimeSpan.FromMinutes(10)
            });
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            var json = await _cache.GetStringAsync(key);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json);
        }


        public async Task InvalidateAsync(string cacheKey)
        {
            await _cache.RemoveAsync(cacheKey);
        }

        public async Task InvalidateByIdAsync(string cacheKeyPrefix, object id)
        {
            string key = $"{cacheKeyPrefix}:{id}";
            await _cache.RemoveAsync(key);
        }

        public async Task<bool> ExistsAsync(string key)
        {
            var value = await _cache.GetStringAsync(key);
            return !string.IsNullOrEmpty(value);
        }

        public async Task<ServiceResult<T>> ExistsAsync<T>(string key)
        {
            var value = await _cache.GetStringAsync(key);
            if (!string.IsNullOrEmpty(value))
            {
                var data = JsonSerializer.Deserialize<T>(value);
                return ServiceResult<T>.Success(data, "");
            }
            return ServiceResult<T>.Failed();
        }

        public async Task<UserState?> GetUserStateAsync(long userId)
        {
            var user = await GetAsync<UserCacheClass>(RedisKeys.User(userId));
            if (user != null)
                return user.State;
            return null;
        }

        public async Task<bool> IsUserRegisteredAsync(long telegramId, TimeSpan? cacheDuration = null)
        {
            var count = await _redisDb.SetLengthAsync(RedisKeys.UserIds);
            if (count == 0)
            {
                var allUserIds = await _db.Users.Select(c => c.TelegramId).ToListAsync();
                if (allUserIds.Any())
                    await _redisDb.SetAddAsync(RedisKeys.UserIds, allUserIds.Select(x => (RedisValue)x).ToArray());
            }

            var q = await _redisDb.SetMembersAsync(RedisKeys.UserIds);

            return await _redisDb.SetContainsAsync(RedisKeys.UserIds, telegramId);
        }
    }
}
