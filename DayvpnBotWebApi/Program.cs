using DayvpnBotWebApi.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// TelegramBot Service
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient("7720992933:AAF3Ektj8ICnQ92gJrIn0FKsYCxrgKqENeg"));
builder.Services.AddHostedService<TelegramBotService>(); // سرویس Long Polling

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
