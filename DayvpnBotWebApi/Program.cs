using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// TelegramBot Service
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient("7720992933:AAF3Ektj8ICnQ92gJrIn0FKsYCxrgKqENeg")); // Production
//builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient("7859129571:AAHZAi8AXpMSjvSPHFQ433fJYAE-sdvPNG4")); // Developer
builder.Services.AddHostedService<TelegramBotService>(); // سرویس Long Polling
builder.Services.AddScoped<UserService>(); // User Service
builder.Services.AddScoped<AppLogService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<SubscriptionLinksService>();
builder.Services.AddScoped<ServicesService>();

// Main DataBase
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Auto-Migration
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
