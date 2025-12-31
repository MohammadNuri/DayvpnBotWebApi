using DayvpnBotWebApi.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DayvpnBotWebApi.Core.Database
{
    public static class AppDbSeeder
    {
        public static async Task SeedAsync(AppDbContext db)
        {
            // اگر دیتا وجود دارد، کاری نکن
            if (await db.Services.AnyAsync())
                return;

            var now = DateTime.UtcNow;

            var services = new List<Service>
        {
            new Service
            {
                Id = 1,
                Name = "Plan 1",
                DurationInDays = 30,
                DataQuotaGB = 5,
                AllowedUsersCount = 1,
                Price = 45000,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsRemoved = false
            },
            new Service
            {
                Id = 2,
                Name = "Plan 2",
                DurationInDays = 30,
                DataQuotaGB = 10,
                AllowedUsersCount = 1,
                Price = 65000,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsRemoved = false
            },
            new Service
            {
                Id = 3,
                Name = "Plan 3",
                DurationInDays = 30,
                DataQuotaGB = 20,
                AllowedUsersCount = 1,
                Price = 80000,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsRemoved = false
            },
            new Service
            {
                Id = 4,
                Name = "Plan 4",
                DurationInDays = 30,
                DataQuotaGB = 30,
                AllowedUsersCount = 1,
                Price = 100000,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsRemoved = false
            },
            new Service
            {
                Id = 5,
                Name = "Plan 5",
                DurationInDays = 30,
                DataQuotaGB = 75,
                AllowedUsersCount = 4,
                Price = 185000,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsRemoved = false
            },
            new Service
            {
                Id = 6,
                Name = "Plan 6",
                DurationInDays = 90,
                DataQuotaGB = 90,
                AllowedUsersCount = 1,
                Price = 215000,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsRemoved = false
            },
            new Service
            {
                Id = 7,
                Name = "Plan 7",
                DurationInDays = 90,
                DataQuotaGB = 100,
                AllowedUsersCount = 2,
                Price = 240000,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsRemoved = false
            },
            new Service
            {
                Id = 8,
                Name = "Plan 8",
                DurationInDays = 90,
                DataQuotaGB = 150,
                AllowedUsersCount = 4,
                Price = 300000,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsRemoved = false
            }
        };

            await db.Services.AddRangeAsync(services);
            await db.SaveChangesAsync();
        }
    }
}
