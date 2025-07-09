using DayvpnBotWebApi.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;

namespace DayvpnBotWebApi.Core.Database
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<SubscriptionLinks> SubscriptionLinks { get; set; }
        public DbSet<AppLog> AppLogs { get; set; }
        public DbSet<Service> Services { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Subscription
            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UserId)
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.Service)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.ServiceId)
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionLinks>()
                .HasOne(s => s.Subscription)                   // هر SubscriptionLinks فقط به یک Subscription تعلق دارد
                .WithMany(u => u.SubscriptionLinks)           // هر Subscription دارای چند SubscriptionLinks است
                .HasForeignKey(s => s.SubscriptionId)         // FK در SubscriptionLinks
                .HasPrincipalKey(u => u.Id);                  // PK در Subscription

            modelBuilder.Entity<AppLog>(entity =>
            {
                // ارتباط با User (nullable)
                entity.HasOne(log => log.User)
                    .WithMany() // چون در User مجموعه‌ای از لاگ‌ها نداری
                    .HasForeignKey(log => log.UserId)
                    .OnDelete(DeleteBehavior.SetNull); // اگر کاربر حذف شد، مقدار UserId در لاگ null شود

                // ارتباط با Subscription (nullable)
                entity.HasOne(log => log.Subscription)
                    .WithMany() // چون در Subscription مجموعه‌ای از لاگ‌ها نداری
                    .HasForeignKey(log => log.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull); // اگر اشتراک حذف شد، مقدار SubscriptionId در لاگ null شود

                // تنظیمات Message
                entity.Property(log => log.Message)
                    .IsRequired()
                    .HasMaxLength(1000);

                // تنظیمات Metadata
                entity.Property(log => log.Metadata)
                    .HasMaxLength(2000);

                // تنظیمات ClientInfo
                entity.Property(log => log.ClientInfo)
                    .HasMaxLength(1000);
            });

            modelBuilder.Entity<User>()
                .Property(u => u.Balance)
                .HasPrecision(18, 0);

            modelBuilder.Entity<Service>()
                .Property(u => u.Price)
                .HasPrecision(18, 0);
        }
    }
}
