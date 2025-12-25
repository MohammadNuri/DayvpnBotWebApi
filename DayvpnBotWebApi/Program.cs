using DayvpnBotWebApi.Core.Database;
using DayvpnBotWebApi.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using Telegram.Bot;

// =======================
// ENV LOAD
// =======================
Env.Load();

// =======================
// EARLY SERILOG (BEFORE BUILDER)
// =======================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        path: "Logs/startup-health-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

try
{
    Log.Information("==========================================");
    Log.Information("🚀 DayVPN Bot - Startup Health Check BEGIN");

    // =======================
    // BUILDER
    // =======================
    Log.Information("Creating WebApplicationBuilder...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    Log.Information("Environment: {Env}", builder.Environment.EnvironmentName);

    // =======================
    // SERVICES
    // =======================
    Log.Information("Registering services...");

    builder.Services.AddControllers();

    // Telegram Bot
    Log.Information("Registering Telegram Bot client...");
    builder.Services.AddSingleton<ITelegramBotClient>(
        new TelegramBotClient(
            "7859129571:AAHZAi8AXpMSjvSPHFQ433fJYAE-sdvPNG4" // Developer Bot
        )
    );

    builder.Services.AddHostedService<TelegramBotService>();
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<AppLogService>();
    builder.Services.AddScoped<SubscriptionService>();
    builder.Services.AddScoped<SubscriptionLinksService>();
    builder.Services.AddScoped<ServicesService>();
    builder.Services.AddScoped<RedisCacheManager>();
    builder.Services.AddScoped<TransactionService>();
    builder.Services.AddScoped<TransactionRequestService>();
    builder.Services.AddScoped<SubscriptionRequestService>();

    Log.Information("Service registration completed");

    // =======================
    // REDIS CHECK
    // =======================
    Log.Information("Configuring Redis...");

    var redisHost = Environment.GetEnvironmentVariable("Redis__Host") ?? "redis:6379";
    var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");

    Log.Information("Redis Host: {RedisHost}", redisHost);

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisHost;
        options.InstanceName = "DayVPN_";
    });

    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        Log.Information("Connecting to Redis...");
        return ConnectionMultiplexer.Connect(redisHost);
    });

    Log.Information("Redis configuration OK");

    // =======================
    // DATABASE CHECK
    // =======================
    Log.Information("Configuring PostgreSQL...");

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new Exception("❌ PostgreSQL connection string is NULL or EMPTY");

    Log.Information("PostgreSQL connection string found");

    var rawUrl = builder.Configuration.GetConnectionString("DefaultConnection");

    if (!string.IsNullOrWhiteSpace(rawUrl) && rawUrl.StartsWith("postgres"))
    {
        var uri = new Uri(rawUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var npgsql = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = userInfo[0],
            Password = userInfo[1],
            SslMode = Npgsql.SslMode.Require,
            TrustServerCertificate = true
        }.ToString();

        builder.Configuration["ConnectionStrings:DefaultConnection"] = npgsql;
    }


    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // =======================
    // BUILD APP
    // =======================
    Log.Information("Building application...");
    var app = builder.Build();

    // =======================
    // DATABASE MIGRATION HEALTH CHECK
    // =======================
    Log.Information("Running database migrations...");
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (app.Environment.IsDevelopment())
        {
            db.Database.Migrate();
        }
    }
    Log.Information("Database migration completed");

    // =======================
    // MIDDLEWARE
    // =======================
    Log.Information("Configuring middleware...");

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    // =======================
    // RUN
    // =======================
    Log.Information("✅ Startup Health Check PASSED");
    Log.Information("🚀 Application is RUNNING");
    Log.Information("==========================================");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "🔥 Startup Health Check FAILED");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
