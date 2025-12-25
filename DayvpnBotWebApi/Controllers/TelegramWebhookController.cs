using DayvpnBotWebApi.Services;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;

[ApiController]
[Route("telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramBotService _telegramBotService;

    public TelegramWebhookController(
        ITelegramBotClient botClient,
        IServiceScopeFactory scopeFactory,
        TelegramBotService telegramBotService)
    {
        _botClient = botClient;
        _scopeFactory = scopeFactory;
        _telegramBotService = telegramBotService;
    }

    [HttpPost("update")]
    public async Task<IActionResult> Post(
        [FromBody] Update update,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var redis = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();
        var trxService = scope.ServiceProvider.GetRequiredService<TransactionRequestService>();

        // 👇 reuse your existing logic
        await _telegramBotService.HandleUpdateAsync(_botClient, update, cancellationToken);

        return Ok();
    }
}
