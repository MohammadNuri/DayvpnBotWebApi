using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// TelegramBot Service
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient("7720992933:AAF3Ektj8ICnQ92gJrIn0FKsYCxrgKqENeg"));
builder.Services.AddHostedService<TelegramBotService>(); // سرویس Long Polling
builder.Services.AddScoped<UserService>(); // User Service
builder.Services.AddScoped<AppLogService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<SubscriptionLinksService>();

// Main DataBase
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
