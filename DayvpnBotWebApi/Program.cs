using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MihaZupan;
using StackExchange.Redis;
using Telegram.Bot;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// TelegramBot Service
var botToken = builder.Configuration["Developer:BotToken"];
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));
builder.Services.AddHostedService<TelegramBotService>(); // سرویس Long Polling
builder.Services.AddScoped<UserService>(); // User Service
builder.Services.AddScoped<AppLogService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<SubscriptionLinksService>();
builder.Services.AddScoped<ServicesService>();
builder.Services.AddScoped<RedisCacheManager>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<TransactionRequestService>();
builder.Services.AddScoped<SubscriptionRequestService>();


// Redis Cache
var redisHost = Environment.GetEnvironmentVariable("Redis__Host") ?? "redis:6379";
var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisHost;
    options.InstanceName = "DayVPN_";
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisHost));

// PosgreSQL Server Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.Urls.Add("http://0.0.0.0:80");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{

}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
