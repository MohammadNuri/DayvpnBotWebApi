using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.Extensions.Caching.Memory;
using System.Runtime.InteropServices.Marshalling;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DayvpnBotWebApi.Services
{
    public class TelegramBotService : IHostedService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _memoryCache;

        public TelegramBotService(ITelegramBotClient botClient, IServiceScopeFactory scopeFactory)
        {
            _botClient = botClient;
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // تنظیمات Long Polling
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // همه نوع آپدیت رو دریافت کن
            };

            _botClient.DeleteWebhook(dropPendingUpdates: true);

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cancellationToken
            );

            Console.WriteLine("DayVPN Bot Started..!");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Bot Shutted Down");
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();
            var _transactionRequestService = scope.ServiceProvider.GetRequiredService<TransactionRequestService>();

            if (update?.Message?.Text != null)
            {
                var message = update.Message;
                var telegramId = message.Chat.Id;

                // ثبت نام کاربر در صورت وجود
                bool isUserRegistered = await _redisCache.IsUserRegisteredAsync(telegramId);
                if (!isUserRegistered)
                    await SignupUserAsync(botClient, message);

                // لاگ کنسول برای دریافت هر نوع مسیج از بات
                await ConsoleLogActions.ConsoleLogReceivedMessageAsync(message);

                switch (message.Text.ToLower())
                {
                    case "/start":
                        await StartAsync(botClient, update);
                        break;

                    case "/me":
                        await SendProfileInfoAsync(botClient, update.CallbackQuery);
                        break;

                    case string data when data.StartsWith("/subscription_"):
                        if (message.Chat.Id == (long)Admins.Nouri)
                            await NotifyAdminOfActivatedSubscriptionAsync(botClient, message, data);
                        break;

                    default:
                        var state = await _redisCache.GetUserStateAsync(message.Chat.Id);
                        if (state != null && state == UserState.Buy_Subscription)
                            await SetSubNameAsync(botClient, update);
                        else if (state != null && state == UserState.Increase_Balance)
                            await SetBalanceAsync(botClient, update);

                        if (message.ReplyToMessage != null && message.Chat.Id == (long)Admins.Nouri)
                        {
                            var text = message.ReplyToMessage.Text;
                            var safeMessageText = EscapeMarkdown(message.Text);
                            var match = Regex.Match(text, @"شناسه عددی:\s*(\d+)");
                            if (match.Success && long.TryParse(match.Groups[1].Value, out long userId))
                            {
                                await botClient.SendMessage(
                                        chatId: userId,
                                        text: safeMessageText,
                                        parseMode: ParseMode.Markdown
                                    );

                                await SendTextToAdminsAsync(botClient, $"✅ پیام شما با موفقیت برای کاربر با شناسه {userId} ارسال شد.");
                            }
                        }

                        break;
                }
            }
            else if (update?.Message?.Photo != null)
            {
                //Take Pictures
                var message = update.Message;
                var userId = message.Chat.Id;
                if (await _redisCache.GetUserStateAsync(userId) == UserState.Increase_Balance)
                {
                    // بررسی اینکه کاربر اشتراک فعال داره
                    if (await _redisCache.ExistsAsync(RedisKeys.Wallet(userId)))
                    {
                        var largestPhoto = message.Photo.Last();
                        var file = await botClient.GetFile(largestPhoto.FileId);

                        try
                        {
                            using var stream = new MemoryStream();
                            await botClient.DownloadFile(file.FilePath, stream);

                            // اطلاعات کاربر برای ارسال به ادمین
                            string fullName = $"{message.Chat.FirstName} {message.Chat.LastName ?? ""}".Trim();
                            string safeFullName = EscapeMarkdown(fullName);
                            string userIdStr = message.Chat.Id.ToString();
                            var walletCache = await _redisCache.GetAsync<WalletCacheClass>(RedisKeys.Wallet(message.Chat.Id));
                            if (walletCache == null)
                            {
                                await SendRestartMessageToUser(botClient, message);
                                return;
                            }

                            string caption = $"📥 درخواست پرداخت جدید دریافت شد.\n\n👤 نام کاربر: {safeFullName}\n🆔 آیدی عددی: {userIdStr}\n💳 مبلغ: {walletCache.RequestBalance:N0} تومان\n\n📌 لطفاً بررسی و تأیید کنید.";

                            // ارسال عکس به ادمین همراه با کپشن
                            using var adminStream = new MemoryStream(stream.ToArray()); // برای اطمینان دوباره بخونیم

                            var user = await _redisCache.GetAsync<UserCacheClass>(RedisKeys.User(userId));

                            Random rnd = new Random();

                            // دخیره درخواست پرداخت در دیتابیس
                            var transactionRequestResult = await _transactionRequestService.CreateAsync(new TransactionRequest()
                            {
                                Amount = walletCache.RequestBalance,
                                PaymentMethod = walletCache.PaymentMethod.Value,
                                UserId = user.RealUserId,
                                TrackingCode = $"{rnd.Next(1000, 9999)}{userId}",
                            });

                            await _redisCache.UpdateCacheAsync<WalletCacheClass>(RedisKeys.Wallet(userId), async c =>
                            {
                                c.TransactionRequestId = transactionRequestResult.Data.Id;
                                await Task.CompletedTask;
                            });

                            await SendConfirmPhotoToAdminsAsync(botClient, adminStream, caption, message.Chat.Id, transactionRequestResult.Data.TrackingCode);

                            // پیام به خود کاربر
                            await botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: $"✅ عکس پرداخت با موفقیت دریافت شد.\n" +
                                      $"📌 کد پیگیری پرداخت شما: *{transactionRequestResult.Data?.TrackingCode}*\n\n" +
                                      $"🕓 لطفاً منتظر بمانید تا پرداخت شما توسط مدیریت بررسی و تأیید شود.\n" +
                                      $"📢 پس از تأیید، موجودی شما به‌روزرسانی خواهد شد و اطلاع‌رسانی انجام می‌گردد.",
                                parseMode: ParseMode.Markdown
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"message:{ex.Message} | innermessage:{ex.InnerException?.Message}");

                            await botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "❌ مشکلی در دریافت تصویر رخ داد. لطفاً دوباره تلاش کنید یا با پشتیبانی در تماس باشید."
                            );
                        }
                    }
                    else
                    {
                        await SendRestartMessageToUser(botClient, message);
                    }
                }
            }
            else if (update?.CallbackQuery != null)
            {
                // تایید پرداخت
                if (update.CallbackQuery.Data.StartsWith("confirm_payment"))
                {
                    var trackingCode = update.CallbackQuery.Data.Split(':')[1];
                    var userId = long.Parse(trackingCode.Substring(4));

                    var walletCacheResult = await GetTransactionRequestAsync(update);
                    if (!walletCacheResult.IsSuccess)
                    {
                        await SendTextToAdminsAsync(botClient, walletCacheResult.Message);
                        await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                        return;
                    }

                    var walletCache = walletCacheResult.Data;

                    var _userService = scope.ServiceProvider.GetRequiredService<UserService>();

                    var result = await _userService.AddUserBalanceAsync(walletCache, userId, walletCache.TransactionRequestId.Value);
                    if (!result.IsSuccess)
                    {
                        await SendTextToAdminsAsync(botClient,
                             $"❌ افزایش موجودی *ناموفق* بود.\n\n👤 کاربر با آیدی عددی: `{userId}` در کش یافت نشد یا مشکلی رخ داده است.\n💳 مبلغ درخواستی: `{walletCache.RequestBalance:N0}` تومان");

                        await botClient.SendMessage(
                            chatId: userId,
                            text: """
❌ متأسفانه افزایش موجودی شما با مشکل مواجه شد.

لطفاً مجدداً تلاش کنید یا برای بررسی دقیق‌تر با پشتیبانی در ارتباط باشید.

🆘 آیدی پشتیبانی: @DayvpnSupport
""",
                            parseMode: ParseMode.Markdown
                        );
                        await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                    }
                    else
                    {
                        decimal newBalance = result.Data;
                        var paymentMethod = walletCache.PaymentMethod;
                        if (paymentMethod != null && paymentMethod.Value == PaymentMethod.DirectPay)
                        {
                            await ApplyDirectSubscription(botClient, update.CallbackQuery, userId);
                        }
                        else
                        {
                            await SendTextToAdminsAsync(botClient,
                                $"✅ افزایش موجودی با موفقیت انجام شد.\n\n" +
                                $"👤 کاربر با آیدی عددی: `{userId}`\n" +
                                $"💳 مبلغ افزوده شده: `{walletCache.RequestBalance:N0}` تومان\n" +
                                $"💰 موجودی جدید: `{newBalance:N0}` تومان\n" +
                                $"🧾 کد پیگیری پرداخت: `{walletCache.TrackingCode}`");

                            await botClient.SendMessage(
                                chatId: userId,
                                text: $"🎉 موجودی شما با موفقیت افزایش یافت!\n\n💳 مبلغ افزوده شده: `{walletCache.RequestBalance:N0}` تومان\n💰 موجودی جدید شما: `{newBalance:N0}` تومان\n\nاز خرید شما سپاسگزاریم 🙏\nاکنون می‌توانید از خدمات ما استفاده کنید.",
                                parseMode: ParseMode.Markdown,
                                replyMarkup: new InlineKeyboardMarkup(new[]
                                {
                                    new[]
                                    {
                                        InlineKeyboardButton.WithCallbackData("👛 کیف پول", "my_profile"),
                                        InlineKeyboardButton.WithCallbackData("📦 خرید اشتراک", "buy_subscription")
                                    }
                                })
                            );
                        }
                        await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                    }
                }
                // عدم تایید پرداخت
                if (update.CallbackQuery.Data.StartsWith("reject_payment"))
                {
                    var trackingCode = update.CallbackQuery.Data.Split(':')[1];
                    var userId = long.Parse(trackingCode.Substring(4));

                    var walletCacheResult = await GetTransactionRequestAsync(update);
                    if (!walletCacheResult.IsSuccess)
                    {
                        await SendTextToAdminsAsync(botClient, walletCacheResult.Message);
                        await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                        return;
                    }

                    var walletCache = walletCacheResult.Data;

                    await _transactionRequestService.UpdateStatusAsync(walletCache.TransactionRequestId.Value, TransactionRequestStatus.Rejected);
                    await _redisCache.InvalidateAsync(RedisKeys.Wallet(userId));
                    await _redisCache.InvalidateAsync(RedisKeys.User(userId));

                    await botClient.SendMessage(
                        chatId: userId,
                        text: """
                    ❌ پرداخت شما تأیید نشد.
                    
                    ممکن است رسید ارسال‌شده نامعتبر یا ناقص بوده باشد، یا پرداختی در سیستم ثبت نشده باشد.
                    
                    🔄 لطفاً فرآیند افزایش موجودی را از ابتدا مجدداً انجام دهید.
                    
                    🛠 برای راهنمایی بیشتر می‌توانید با پشتیبانی در تماس باشید.
                    
                    🆘 آیدی پشتیبانی: @DayvpnSupport
                    """,
                        parseMode: ParseMode.Markdown,
                        replyMarkup: new InlineKeyboardMarkup(new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("🏠 صفحه اصلی", "back-to-home"),
                                InlineKeyboardButton.WithCallbackData("🔁 شروع مجدد", "increase_balance")
                            }
                        })
                    );

                    // 🔔 پیام اطلاع‌رسانی به ادمین
                    await SendTextToAdminsAsync(botClient, $"""
🚫 درخواست افزایش موجودی با کد پیگیری `{trackingCode}` رد شد و پیام مربوطه برای کاربر ارسال گردید.
""");

                    await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                }

                switch (update.CallbackQuery.Data)
                {
                    case "buy_subscription":
                        await BuySubscriptionAsync(botClient, update.CallbackQuery, false);
                        break;

                    case "back-to-home":
                        await Start(botClient, update.CallbackQuery, false);
                        break;

                    case "back-to-buy-subscription":
                        await BuySubscriptionAsync(botClient, update.CallbackQuery, true);
                        break;

                    case "my_subscriptions":
                        //Todo 
                        //await MySubscriptions(botClient, update.CallbackQuery);
                        await SendDevelopingTextToUserAsync(botClient, update.CallbackQuery);
                        break;
                    //Todo
                    case "help":
                        await SendDevelopingTextToUserAsync(botClient, update.CallbackQuery);
                        break;
                    //Todo
                    case "contact_support":
                        await SendDevelopingTextToUserAsync(botClient, update.CallbackQuery);
                        break;

                    case "increase_balance":
                        await IncreaseBalance(botClient, update.CallbackQuery);
                        break;

                    case string data when data.StartsWith("request_buy"):
                        await HandleBuySubscriptionAsync(botClient, update.CallbackQuery, data);
                        break;

                    case string data when data.StartsWith("confirm_buy"):
                        if (await _redisCache.ExistsAsync(RedisKeys.Subscription(update.CallbackQuery.Message.Chat.Id)))
                            await HandleConfirmBuySubscription(botClient, update.CallbackQuery, data);
                        else
                            await SendRestartMessageToUser(botClient, update.CallbackQuery);
                        break;

                    case string data when data.StartsWith("subscriptions_page_"):
                        {
                            int page = int.Parse(data.Replace("subscriptions_page_", ""));
                            await ShowSubscriptionPage(botClient, update.CallbackQuery, page);
                            break;
                        }

                    case string data when data.StartsWith("subscription_detail_"):
                        {
                            int index = int.Parse(data.Replace("subscription_detail_", ""));
                            var sub = Subscriptions[index];

                            await botClient.SendMessage(
                                chatId: update.CallbackQuery.Message.Chat.Id,
                                text: $"📄 اطلاعات اشتراک:\n\n🔹 نام: {sub.Name}\n📦 حجم: {sub.Volume}"
                            );
                            break;
                        }

                    case string data when data.StartsWith("send_config"):
                        if (update.CallbackQuery.Message.Chat.Id == (long)Admins.Nouri)
                            await InitiateConfigSendToUserAsync(botClient, update.CallbackQuery, data);
                        break;

                    case "subscriptions_close":
                        {
                            await botClient.DeleteMessage(
                                chatId: update.CallbackQuery.Message.Chat.Id,
                                messageId: update.CallbackQuery.Message.MessageId
                            );
                            break;
                        }
                    case "my_profile":
                        await SendProfileInfoAsync(botClient, update.CallbackQuery);
                        break;
                    // پرداخت مستقیم
                    case "pay_direct":
                        await ConfirmPurchaseSubscriptionPayDirect(botClient, update.CallbackQuery);
                        break;
                    // پرداخت از کیف پول
                    case "pay_from_wallet":
                        await ConfirmPurchaseSubscriptionPayWallet(botClient, update.CallbackQuery);
                        break;
                    default:
                        Console.WriteLine("Unknown Callback Data: " + update.CallbackQuery.Data);
                        break;
                }
            }
        }


        private async Task<ServiceResult<WalletCacheClass>> GetTransactionRequestAsync(Update update)
        {
            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();
            var _transactionRequestService = scope.ServiceProvider.GetRequiredService<TransactionRequestService>();

            var trackingCode = update.CallbackQuery.Data.Split(':')[1];
            var userId = long.Parse(trackingCode.Substring(4));

            var walletCache = new WalletCacheClass();

            if (await _redisCache.ExistsAsync(RedisKeys.Wallet(userId)))
            {
                walletCache = await _redisCache.GetAsync<WalletCacheClass>(RedisKeys.Wallet(userId));
                walletCache.TrackingCode = trackingCode;
            }
            else
            {
                var transanctionRequest = await _transactionRequestService.GetByTrackingCodeAsync(trackingCode);
                if (transanctionRequest == null)
                {
                    return ServiceResult<WalletCacheClass>.Failed($"""
                            ❗️درخواست افزایش موجودی یافت نشد.

                            🔎 کد پیگیری: `{trackingCode}`

                            لطفاً از صحت کد وارد شده اطمینان حاصل فرمایید.
                            """);
                }


                if (transanctionRequest.Status == TransactionRequestStatus.Approved ||
                    transanctionRequest.Status == TransactionRequestStatus.Rejected)
                {
                    string statusMessage = transanctionRequest.Status switch
                    {
                        TransactionRequestStatus.Approved => "⚠️ این درخواست افزایش موجودی قبلاً *تأیید* شده است و نیاز به اقدام مجدد ندارد.",
                        TransactionRequestStatus.Rejected => "❌ این درخواست افزایش موجودی قبلاً *رد* شده است و امکان تأیید مجدد آن وجود ندارد.",
                        _ => "درخواست افزایش موجودی معتبر نیست."
                    };

                    return ServiceResult<WalletCacheClass>.Failed(statusMessage);
                }

                walletCache = new WalletCacheClass()
                {
                    PaymentMethod = transanctionRequest.PaymentMethod,
                    RequestBalance = transanctionRequest.Amount,
                    TransactionRequestId = transanctionRequest.Id,
                    TrackingCode = trackingCode,
                };
            }

            if (walletCache == null)
            {
                return ServiceResult<WalletCacheClass>.Failed($"""
                            ❗️درخواست افزایش موجودی یافت نشد.

                            🔎 کد پیگیری: `{trackingCode}`

                            لطفاً از صحت کد وارد شده اطمینان حاصل فرمایید.
                            """);
            }

            return ServiceResult<WalletCacheClass>.Success(walletCache, "");
        }
        private async Task InitiateConfigSendToUserAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, string data)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);

            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();

            if (!long.TryParse(data.Split('_')[2], out long userTelegramId))
            {
                // پیام خطا برای ادمین
                await SendTextToAdminsAsync(botClient, """
                ⚠️ *خطا در ارسال کانفیگ!*

                ❌ شناسه کاربر به درستی استخراج نشد.  
                لطفاً مجدداً تلاش کنید یا با پشتیبانی تماس بگیرید.
                """);
                return;
            }

            await _redisCache.UpdateCacheAsync<UserCacheClass>(RedisKeys.User(userTelegramId), async c =>
            {
                c.State = UserState.Send_User_Config;
                await Task.CompletedTask;
            });

            // پیام راهنما برای ادمین
            await SendTextToAdminsAsync(botClient, $"""
            ✅ *حالت ارسال کانفیگ فعال شد!*

            لطفاً *متن کامل کانفیگ* را ارسال کنید.  
            ربات به صورت خودکار آن را برای کاربر مورد نظر فوروارد خواهد کرد.

            👤 Telegram ID: `{userTelegramId}`
            """);
        }

        private async Task DeliverConfigToUserAsync(ITelegramBotClient botClient, Update update)
        {
            //var message = update.Message;

            //using var scope = _scopeFactory.CreateScope();
            //var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();

            //var userTelegramId = await _redisCache.GetAsync(RedisKeys.User(message.Chat.Id));

            //if (userTelegramId.HasValue)
            //{
            //    await botClient.SendMessage(
            //        chatId: userTelegramId,
            //        text: message.Text!
            //    );

            //    await botClient.SendMessage(
            //        chatId: message.Chat.Id,
            //        text: "✅ *کانفیگ با موفقیت برای کاربر ارسال شد.*\n\nممنون از همکاری شما 🙏",
            //        parseMode: ParseMode.Markdown
            //    );
            //}
            //else
            //{
            //    await botClient.SendMessage(
            //        chatId: message.Chat.Id,
            //        text: """
            //        ⚠️ *خطا در ارسال کانفیگ!*

            //        اطلاعات کاربری برای ارسال کانفیگ در حافظه یافت نشد.  
            //        احتمالاً زمان شما منقضی شده یا مرحله‌ی قبل به‌درستی انجام نشده است.

            //        لطفاً ابتدا دکمه‌ی `ارسال کانفیگ` را در پیام خرید کلیک کرده و سپس پیام را ارسال کنید.

            //        🆘 در صورت مشکل، با توسعه‌دهنده تماس بگیرید.
            //        """,
            //        parseMode: ParseMode.Markdown
            //    );
            //}
        }

        private async Task SendRestartMessageToUser(ITelegramBotClient botClient, Message message)
        {
            string text = "❗ خطایی رخ داده یا زمان انجام عملیات شما منقضی شده است.\n\n" +
                          "لطفاً با ارسال دستور /start مجدداً فرایند را از ابتدا آغاز کنید.\n\n" +
                          "در صورت بروز مشکل، با پشتیبانی تماس بگیرید:\n" +
                          "@DayvpnSupport";

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: text
            );
        }

        private async Task SendRestartMessageToUser(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            var message = callbackQuery.Message;

            string text = "❗ خطایی رخ داده یا زمان انجام عملیات شما منقضی شده است.\n\n" +
                          "لطفاً با ارسال دستور /start مجدداً فرایند را از ابتدا آغاز کنید.\n\n" +
                          "در صورت بروز مشکل، با پشتیبانی تماس بگیرید:\n" +
                          "@DayvpnSupport";

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: text
            );
        }

        // ثبت نام کاربر
        private async Task SignupUserAsync(ITelegramBotClient botClient, Message message)
        {
            using var scope = _scopeFactory.CreateScope();
            var _userService = scope.ServiceProvider.GetRequiredService<UserService>();

            ServiceResult result = await _userService.RegisterUser(message);

            await SendTextToAdminsAsync(botClient, result.Message);
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8; // برای پشتیبانی یونیکد

            var originalColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Error Occurred!");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("📄 Message: ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(exception.Message);

            if (exception.InnerException != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("🔁 Inner Exception Message: ");

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(exception.InnerException.Message);

                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("🔁 Inner Exception: ");

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(exception.InnerException);
            }

            // بازگرداندن رنگ اولیه
            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }


        // start
        private async Task StartAsync(ITelegramBotClient botClient, Update update)
        {
            var message = update.Message;
            await GlobalStartAsync(botClient, message);
        }

        // start again
        private async Task Start(ITelegramBotClient botClient, CallbackQuery callBackQuery, bool removeLastMessage)
        {
            await botClient.AnswerCallbackQuery(callBackQuery.Id);

            var message = callBackQuery.Message;

            if (message != null && removeLastMessage)
            {
                try
                {
                    await botClient.DeleteMessage(message.Chat.Id, message.MessageId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error in removing last message " + ex.Message);
                    throw;
                }
            }
            else
            {
                await GlobalStartAsync(botClient, message);
            }
        }

        private async Task GlobalStartAsync(ITelegramBotClient botClient, Message message)
        {
            string fullName = $"{message.Chat.FirstName} {message.Chat.LastName}";

            string safeFullName = EscapeMarkdown(fullName);

            string welcomeText = $"👋 سلام {safeFullName} عزیز!\n\n" +
                     "🤖 به **ربات DayVPN** خوش اومدی!\n\n" +
                     "📶 با DayVPN می‌تونی اشتراک VPN تهیه کنی و از سرورهای پرسرعت در کشورهای مختلف استفاده کنی.\n\n" +
                     "📱 قابل استفاده در: اندروید، ویندوز، آیفون و سایر دستگاه‌ها\n" +
                     "🌐 مناسب برای همه اینترنت‌ها: همراه اول، ایرانسل، رایتل، ADSL و ...\n" +
                     "🌍 کشورهای پشتیبانی‌شده: 🇩🇪 🇳🇱 🇺🇸 🇫🇷 🇹🇷 🇫🇮\n\n" +
                     "👇 برای شروع یکی از گزینه‌های زیر رو انتخاب کن:";

            string photoPath = Path.Combine(Directory.GetCurrentDirectory(), "Src", "Images", "DayVPN.jpg");

            await using var stream = File.OpenRead(photoPath);

            var buttons = new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📦 اشتراک‌های من", "my_subscriptions"),
                    InlineKeyboardButton.WithCallbackData("🛒 خرید اشتراک", "buy_subscription")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("👛 کیف پول", "my_profile"),
                    InlineKeyboardButton.WithCallbackData("💰 افزایش موجودی", "increase_balance")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("❓ راهنما و کمک", "help"),
                    InlineKeyboardButton.WithCallbackData("💬 پیام به پشتیبانی", "contact_support")
                },
            };

            var keyboard = new InlineKeyboardMarkup(buttons);

            await botClient.SendPhoto(
                chatId: message!.Chat.Id,
                photo: new InputFileStream(stream, "DayVPN.jpg"),
                caption: welcomeText,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard
            );
        }

        // buy subscription
        private async Task BuySubscriptionAsync(ITelegramBotClient botClient, CallbackQuery callBackQuery, bool removeLastMessage)
        {
            await botClient.AnswerCallbackQuery(callBackQuery.Id);

            var message = callBackQuery.Message;

            if (message != null && removeLastMessage)
            {
                try
                {
                    await botClient.DeleteMessage(message.Chat.Id, message.MessageId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error in removing last message " + ex.Message);
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();
            var services = await _redisCache.GetAllAsync<Service>(RedisKeys.ServicesList);

            var inlineKeyboardButtons = new List<InlineKeyboardButton[]>();

            foreach (var service in services)
            {
                string text = $"🟢 {service.DurationInDays} روزه - {service.DataQuotaGB} گیگ - {service.AllowedUsersCount} کاربر - {service.Price:N0} تومان";
                string callbackData = $"request_buy_{service.Id}";

                var button = InlineKeyboardButton.WithCallbackData(text, callbackData);
                inlineKeyboardButtons.Add(new[] { button });
            }

            inlineKeyboardButtons.Add(new[] { InlineKeyboardButton.WithCallbackData("صفحه اصلی 🏠", "back-to-home") });

            await botClient.SendMessage(
                chatId: message!.Chat.Id,
                text: "🎯 خرید سرویس DayVPN\r\n\r\n🛒 در 2 مرحله، اشتراک اختصاصی خود را دریافت کنید!\r\n💡 همه سرویس‌ها شامل تمامی سرورهای DayVPN هستند.\r\n🌍 قابلیت اتصال به هر لوکیشن، در هر زمان!\r\n\r\n🔻 یکی از پلن‌های زیر را انتخاب کنید:",
                parseMode: ParseMode.Markdown,
                replyMarkup: new InlineKeyboardMarkup(inlineKeyboardButtons));
        }

        private async Task HandleBuySubscriptionAsync(ITelegramBotClient botClient, CallbackQuery callBackQuery, string data)
        {
            await botClient.AnswerCallbackQuery(callBackQuery.Id);

            var message = callBackQuery.Message;

            if (message != null)
            {
                try
                {
                    await botClient.DeleteMessage(message.Chat.Id, message.MessageId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error in removing last message " + ex.Message);
                }
            }

            if (!int.TryParse(data.Split('_')[2], out int serviceId))
            {
                await SendRestartMessageToUser(botClient, callBackQuery);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();

            // اضافه کردن سرویس به کش
            var cacheResult = await _redisCache.CacheSelectedServiceForUserAsync(message.Chat.Id, serviceId);
            if (!cacheResult.IsSuccess || cacheResult.Data == null)
            {
                await SendRestartMessageToUser(botClient, callBackQuery);
                return;
            }

            string confirmText = $"""
🛒 *پیش‌نمایش خرید سرویس DayVPN*

🔹 *نام پلن:* {cacheResult.Data.Name}
📅 *مدت زمان:* {cacheResult.Data.DurationInDays} روز
📦 *حجم:* {cacheResult.Data.DataQuotaGB} گیگ
👥 *تعداد کاربران مجاز:* {cacheResult.Data.AllowedUsersCount} نفر
💳 *قیمت:* {cacheResult.Data.Price:N0} تومان

آیا از انتخاب خود مطمئن هستید؟ 😊
برای تایید، دکمه زیر را لمس کنید.
""";
            string callBackData = $"confirm_buy_{cacheResult.Data.Id}";

            await botClient.SendMessage(
                chatId: message!.Chat.Id,
                text: confirmText,
                parseMode: ParseMode.Markdown,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("تایید و خرید ✅", callBackData) },
                    new[] { InlineKeyboardButton.WithCallbackData("بازگشت 🔙", "back-to-buy-subscription") },
                    new[] { InlineKeyboardButton.WithCallbackData("بازگشت به صفحه اصلی 🏠", "back-to-home") },
                }));
        }

        private async Task HandleConfirmBuySubscription(ITelegramBotClient botClient, CallbackQuery callBackQuery, string data)
        {
            await botClient.AnswerCallbackQuery(callBackQuery.Id);

            var message = callBackQuery.Message;

            if (message != null)
            {
                try
                {
                    await botClient.DeleteMessage(message.Chat.Id, message.MessageId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error removing last message " + ex.Message);
                }
            }

            if (!int.TryParse(data.Split('_')[2], out int serviceId))
            {
                await SendRestartMessageToUser(botClient, callBackQuery);
                return;
            }

            string callBackFunction = $"request_buy_{serviceId}";

            await botClient.SendMessage(
                chatId: message!.Chat.Id,
                text: "📝 لطفاً نام کانفیگ یا پیکربندی خود را به صورت انگلیسی وارد کنید:\r\n\r\n📌 این نام برای ارسال تنظیمات اختصاصی شما استفاده خواهد شد.",
                parseMode: ParseMode.Markdown,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("بازگشت 🔙", callBackFunction) },
                }));
        }

        private async Task SetSubNameAsync(ITelegramBotClient botClient, Update update)
        {
            var message = update.Message;
            var userId = message.Chat.Id;

            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();
            var _subscriptionRequestService = scope.ServiceProvider.GetRequiredService<SubscriptionRequestService>();
            var _userService = scope.ServiceProvider.GetRequiredService<UserService>();

            if (await _redisCache.ExistsAsync(RedisKeys.Subscription(userId)))
            {
                const string SubNameRegex = "^(?i)[a-z0-9 ]{1,30}$";
                if (Regex.IsMatch(message.Text, SubNameRegex))
                {
                    var updateResult = await _redisCache.UpdateCacheAsync<SubscriptionCacheClass>(RedisKeys.Subscription(userId), async c =>
                    {
                        c.RequestedSubscriptioName = message.Text;
                        await Task.CompletedTask;
                    });

                    if (!updateResult.IsSuccess)
                    {
                        await SendRestartMessageToUser(botClient, message);
                        return;
                    }

                    var subscriptionRequest = updateResult.Data;

                    var user = await _userService.GetUserProfileByTelegramIdAsync(userId);

                    // Save the SubscriptionRequest in database
                    var result = await _subscriptionRequestService.CreateAsync(new SubscriptionRequest()
                    {
                        ServiceId = subscriptionRequest.ServiceId,
                        UserId = user.Id,
                        SubscriptionName = subscriptionRequest.RequestedSubscriptioName,
                        Status = Status.InProgress
                    });

                    if (subscriptionRequest != null)
                    {
                        var previewText = $"""
                        📝 *بررسی نهایی و انتخاب نحوه پرداخت*

                        📛 نام انتخابی: `{subscriptionRequest.RequestedSubscriptioName}`
                        🔹 سرویس: *{subscriptionRequest.Name}*
                        📦 حجم: `{subscriptionRequest.DataQuotaGB}` گیگابایت  
                        ⏳ مدت زمان: `{subscriptionRequest.DurationInDays}` روز  
                        👥 کاربران مجاز: `{subscriptionRequest.AllowedUsersCount}` نفر  
                        💳 مبلغ قابل پرداخت: `{subscriptionRequest.Price:N0}` تومان

                        لطفاً نحوه پرداخت خود را انتخاب نمایید.
                        """;

                        await botClient.SendMessage(
                            chatId: message.Chat.Id,
                            text: previewText,
                            parseMode: ParseMode.Markdown,
                            replyMarkup: new InlineKeyboardMarkup(new[]
                            {
                                InlineKeyboardButton.WithCallbackData("👛 پرداخت از کیف پول", "pay_from_wallet"),
                                InlineKeyboardButton.WithCallbackData("💳 پرداخت مستقیم", "pay_direct")
                            })
                        );
                    }
                }
                else
                {
                    await botClient.SendMessage(
                        chatId: message!.Chat.Id,
                        text: "❌ نام وارد شده نامعتبر است.\r\nفقط حروف انگلیسی (A-Z)، اعداد (0-9) و فاصله مجاز هستند.\r\nحداکثر طول مجاز: 30 کاراکتر.\r\n\r\nلطفا مجدد وارد کنید..",
                        parseMode: ParseMode.Markdown);
                }
            }
            else
            {
                await SendRestartMessageToUser(botClient, message);
            }
        }

        private async Task ConfirmPurchaseSubscriptionPayWallet(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);
            var message = callbackQuery.Message;
            var userId = message.Chat.Id;

            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();

            if (await _redisCache.ExistsAsync(RedisKeys.Subscription(userId)))
            {
                // Validate Balance and Insert Subscription into Database
                var _subscriptionService = scope.ServiceProvider.GetRequiredService<SubscriptionService>();
                var result = await _subscriptionService.InsertSubscription(message.Chat.Id);

                if (result.IsSuccess && result is ServiceResult<SubscriptionResultDto> successResult)
                {
                    await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: result.Message,
                    parseMode: ParseMode.Markdown
                    );

                    var data = successResult.Data!;

                    string safeFullName = EscapeMarkdown(data.UserFullName);

                    string adminMessage = $"""
                    ✅ *خرید سرویس جدید با موفقیت ثبت شد!*
                    
                    👤 کاربر: *{safeFullName}*  
                    🆔 شناسه عددی: `{data.TelegramId}`  
                    💰 موجودی جدید: `{data.NewBalance:N0}` تومان  
                    📅 زمان خرید: {PersianHelper.GetPersianCalendar(data.PurchasedAt)}
                    
                    🔖 نام اشتراک: *{data.SubscriptionName}*  
                    📡 سرویس: *{data.ServiceName}*  
                    📦 حجم: *{data.VolumeGb} گیگ*  
                    👥 کاربران مجاز: *{data.UserCount} نفر*  
                    ⏳ مدت زمان: *{data.DurationDays} روز*  
                    💳 قیمت: `{data.Price:N0}` تومان  
                    📎 کد پیگیری اشتراک: `{data.TrackingCode}`
                    
                    📌 لطفاً با ریپلای این پیام، کانفیگ سرویس را برای کاربر ارسال کنید.
                    """;

                    await SendTextToAdminsAsync(botClient,
                        adminMessage);
                }
                else
                {
                    await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: result.Message,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("💰 افزایش موجودی", "increase_balance")
                        }
                    }));
                }
            }
            else
            {
                await SendRestartMessageToUser(botClient, callbackQuery);
            }
        }

        private async Task ConfirmPurchaseSubscriptionPayDirect(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);
            var message = callbackQuery.Message;
            var userId = message.Chat.Id;

            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();

            var result = await _redisCache.ExistsAsync<SubscriptionCacheClass>(RedisKeys.Subscription(userId));
            if (!result.IsSuccess || result.Data == null)
            {
                await SendRestartMessageToUser(botClient, callbackQuery);
                return;
            }

            var service = result.Data;

            await _redisCache.SetAsync<WalletCacheClass>(RedisKeys.Wallet(userId), new WalletCacheClass()
            {
                PaymentMethod = PaymentMethod.DirectPay,
                RequestBalance = service.Price,
            });

            await _redisCache.UpdateCacheAsync<UserCacheClass>(RedisKeys.User(userId), async c =>
            {
                c.State = UserState.Increase_Balance;
                await Task.CompletedTask;
            });

            await ConfirmUserAmountAsync(botClient, null, service.Price, callbackQuery);
        }

        private async Task ApplyDirectSubscription(ITelegramBotClient botClient, CallbackQuery callbackQuery, long userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();
            var _subscriptionRequestService = scope.ServiceProvider.GetRequiredService<SubscriptionRequestService>();

            bool isValid = false;

            if (await _redisCache.ExistsAsync(RedisKeys.Subscription(userId)))
                isValid = true;

            SubscriptionRequest? subscriptionRequest = null;

            if (!isValid)
            {
                isValid = await _subscriptionRequestService.ExistsAsync(userId);
            }

            if (isValid)
            {
                var _subscriptionService = scope.ServiceProvider.GetRequiredService<SubscriptionService>();

                // Validate Balance and Insert Subscription into Database
                var result = await _subscriptionService.InsertSubscription(userId);

                await botClient.SendMessage(
                    chatId: userId,
                    text: result.Message,
                    parseMode: ParseMode.Markdown
                    );

                if (result.IsSuccess && result is ServiceResult<SubscriptionResultDto> successResult)
                {
                    var data = successResult.Data!;

                    string safeFullName = EscapeMarkdown(data.UserFullName);

                    string adminMessage = $"""
                    ✅ *خرید سرویس جدید با موفقیت ثبت شد!*
                    
                    👤 کاربر: *{safeFullName}*  
                    🆔 شناسه عددی: `{data.TelegramId}`  
                    💰 موجودی جدید: `{data.NewBalance:N0}` تومان  
                    📅 زمان خرید: {PersianHelper.GetPersianCalendar(data.PurchasedAt)}
                    
                    🔖 نام اشتراک: *{data.SubscriptionName}*  
                    📡 سرویس: *{data.ServiceName}*  
                    📦 حجم: *{data.VolumeGb} گیگ*  
                    👥 کاربران مجاز: *{data.UserCount} نفر*  
                    ⏳ مدت زمان: *{data.DurationDays} روز*  
                    💳 قیمت: `{data.Price:N0}` تومان  
                    📎 کد پیگیری اشتراک: `{data.TrackingCode}`
                    
                    📌 لطفاً با ریپلای این پیام، کانفیگ سرویس را برای کاربر ارسال کنید.
                    """;

                    await SendTextToAdminsAsync(botClient,
                        adminMessage);
                }
                else
                {
                    await SendRestartMessageToUser(botClient, callbackQuery);
                }
            }
            else
            {
                await SendRestartMessageToUser(botClient, callbackQuery);
            }
        }

        private async Task IncreaseBalance(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);
            var userId = callbackQuery.Message.Chat.Id;

            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();
            var _userService = scope.ServiceProvider.GetRequiredService<UserService>();

            var user = await _userService.GetUserProfileByTelegramIdAsync(userId);

            await _redisCache.SetAsync<UserCacheClass>(RedisKeys.User(userId), new UserCacheClass()
            {
                RealUserId = user.Id,
                FullName = user.FullName,
                State = UserState.Increase_Balance,
                UserId = userId,
            });

            await _redisCache.SetAsync<WalletCacheClass>(RedisKeys.Wallet(userId), new WalletCacheClass()
            {
                PaymentMethod = PaymentMethod.WalletPay,
            });

            string message = """
    💳 افزایش موجودی

    💰 لطفاً مبلغ مورد نظر خود را *به تومان* وارد کنید:
    🔹 مثال: `100000` (معادل ۱۰۰ هزار تومان)

    ⚠️ لطفاً مبلغ را *کاملاً دقیق* و فقط به عدد وارد نمایید.
    در صورتی که مبلغ نادرست وارد شود، پردازش افزایش موجودی ممکن است با *تأخیر* انجام شود.

    🔐 پرداخت شما کاملاً امن بوده و اطلاعات محرمانه محفوظ می‌ماند.
    """;


            await botClient.SendMessage(
                chatId: userId,
                text: message,
                parseMode: ParseMode.Markdown
            );
        }


        private async Task SetBalanceAsync(ITelegramBotClient botClient, Update update)
        {
            var message = update.Message;
            var chatId = message.Chat.Id;

            if (!long.TryParse(message.Text, out long amount))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ لطفاً فقط مبلغ را *به صورت عددی* وارد کنید.\nمثال: `100000`",
                    parseMode: ParseMode.Markdown
                );
                return;
            }

            if (amount < 50000)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "⚠️ حداقل مبلغ قابل واریز *۵۰٬۰۰۰ تومان* است.\nلطفاً مبلغ بیشتری وارد کنید.",
                    parseMode: ParseMode.Markdown
                );
                return;
            }

            await ConfirmUserAmountAsync(botClient, update, amount, null);

        }

        private async Task ConfirmUserAmountAsync(ITelegramBotClient botClient, Update? update, decimal amount, CallbackQuery? callbackQuery)
        {
            var message = new Message();
            var chatId = new long();

            if (update != null)
            {
                message = update.Message;
                chatId = message.Chat.Id;
            }
            else
            {
                message = callbackQuery.Message;
                chatId = callbackQuery.Message.Chat.Id;
            }

            using var scope = _scopeFactory.CreateScope();
            var _redisCache = scope.ServiceProvider.GetRequiredService<RedisCacheManager>();

            if (!await _redisCache.ExistsAsync(RedisKeys.Wallet(chatId)))
            {
                if (callbackQuery != null)
                    await SendRestartMessageToUser(botClient, callbackQuery);
                else
                    await SendRestartMessageToUser(botClient, message);
                return;
            }

            await _redisCache.UpdateCacheAsync<WalletCacheClass>(RedisKeys.Wallet(chatId), async c =>
            {
                c.RequestBalance = amount;
                await Task.CompletedTask;
            });

            string rialAmount = (amount * 10).ToString("N0"); // چون هر تومان = 10 ریال

            string chatMessage = $"""
💳 مرحله پرداخت دستی

کاربر گرامی، لطفاً مبلغ `{amount:N0}` تومان (معادل `{rialAmount}` ریال) را به کارت زیر واریز نمایید:

🏦 *شماره کارت:* `0935 6361 8619 6219`  
👤 *نام صاحب کارت:* محمد نوری

⛔ *توجه مهم:*  
از روش‌های «پل» خصوصاً با بانک‌هایی مانند *بلو بانک* استفاده نکنید.  
در صورت استفاده از این روش‌ها، ممکن است پرداخت شما *ثبت نشود یا با تأخیر تایید شود.*

📸 پس از انجام واریز، لطفاً *تصویر رسید واریز* را ارسال نمایید تا افزایش موجودی شما تأیید گردد.
""";
            await botClient.SendMessage(
                chatId: chatId,
                text: chatMessage,
                parseMode: ParseMode.Markdown
            );
        }

        private async Task MySubscriptions(ITelegramBotClient botClient, CallbackQuery callBackQuery)
        {

            await botClient.AnswerCallbackQuery(callBackQuery.Id);

            const int page = 0;
            var totalPages = (int)Math.Ceiling((double)Subscriptions.Count / 10);
            var text = $"📦 اشتراک‌های شما - صفحه {page + 1} از {totalPages}";

            var sentMessage = await botClient.SendMessage(
                chatId: callBackQuery.Message.Chat.Id,
                text: text,
                replyMarkup: new InlineKeyboardMarkup(Array.Empty<InlineKeyboardButton[]>())
            );

            // ساخت CallbackQuery جدید با پیام واقعی که ارسال شده
            var fakeCallback = new CallbackQuery
            {
                Message = sentMessage
            };

            await ShowSubscriptionPage(botClient, fakeCallback, page);
        }

        private async Task ShowSubscriptionPage(ITelegramBotClient botClient, CallbackQuery callbackQuery, int page = 0)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);

            const int pageSize = 10;
            var totalPages = (int)Math.Ceiling((double)Subscriptions.Count / pageSize);
            page = Math.Clamp(page, 0, totalPages - 1);

            // گرفتن 10 اشتراک صفحه فعلی
            var pageItems = Subscriptions
                .Skip(page * pageSize)
                .Take(pageSize)
                .Select((sub, index) => InlineKeyboardButton.WithCallbackData(
                    $"{sub.Name} - {sub.Volume}",
                    $"subscription_detail_{page * pageSize + index}"
                ))
                .ToList();

            // ساخت دکمه‌ها: ۲ ستون × ۵ ردیف
            var subscriptionRows = Enumerable.Range(0, 5)
                .Select(i => new[]
                {
                    pageItems.ElementAtOrDefault(i),
                    pageItems.ElementAtOrDefault(i + 5)
                }
                .Where(b => b != null).ToArray()) // فیلتر دکمه‌های null
                .Where(row => row.Length > 0)
                .ToList();

            // دکمه‌های ناوبری
            var navigationRow = new List<InlineKeyboardButton>();

            if (page > 0)
                navigationRow.Add(InlineKeyboardButton.WithCallbackData("◀️ قبلی", $"subscriptions_page_{page - 1}"));

            if (page < totalPages - 1)
                navigationRow.Add(InlineKeyboardButton.WithCallbackData("▶️ بعدی", $"subscriptions_page_{page + 1}"));

            navigationRow.Add(InlineKeyboardButton.WithCallbackData("❌ بستن لیست", "subscriptions_close"));

            subscriptionRows.Add(navigationRow.ToArray());

            var markup = new InlineKeyboardMarkup(subscriptionRows);

            // ویرایش پیام قبلی
            await botClient.EditMessageText(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                text: $"📦 اشتراک‌های شما - صفحه {page + 1} از {totalPages}",
                replyMarkup: markup
            );
        }

        // ارسال پیام به ادمین ها
        private async Task SendTextToAdminsAsync(ITelegramBotClient botClient, string message)
        {
            long[] adminIds = { (long)Admins.Nouri /*, Admins.OtherAdminId if needed */ };

            string safeMessage = EscapeMarkdown(message);

            foreach (var adminId in adminIds)
            {
                await botClient.SendMessage(
                    chatId: adminId,
                    text: safeMessage,
                    parseMode: ParseMode.Markdown
                );
            }
        }

        public record AdminActionButton(string Text, string CallbackData);
        private async Task SendTextToAdminsAsync(ITelegramBotClient botClient, string message, List<AdminActionButton> replyMarkups)
        {
            long[] adminIds = { (long)Admins.Nouri };

            var buttons = replyMarkups
                .Select(btn => new[] { InlineKeyboardButton.WithCallbackData(btn.Text, btn.CallbackData) })
                .ToList();

            foreach (var adminId in adminIds)
            {
                await botClient.SendMessage(
                    chatId: adminId,
                    text: message,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: new InlineKeyboardMarkup(buttons)
                );
            }
        }

        // ارسال عکس و متن به ادمین ها
        private async Task SendConfirmPhotoToAdminsAsync(ITelegramBotClient botClient, Stream photoStream, string caption, long userId, string data)
        {
            long[] adminIds = { (long)Admins.Nouri /*, Admins.OtherAdminId */ };

            var trackingCode = data;

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ تأیید پرداخت", $"confirm_payment:{trackingCode}"),
                    InlineKeyboardButton.WithCallbackData("❌ رد پرداخت", $"reject_payment:{trackingCode}")
                }
            });

            foreach (var adminId in adminIds)
            {
                using var streamCopy = new MemoryStream();
                await photoStream.CopyToAsync(streamCopy);
                streamCopy.Position = 0;

                await botClient.SendPhoto(
                    chatId: adminId,
                    photo: new InputFileStream(streamCopy, "attachment.jpg"),
                    caption: caption,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: keyboard
                );
            }
        }

        private async Task SendProfileInfoAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);

            var chatId = callbackQuery.Message.Chat.Id;

            using var scope = _scopeFactory.CreateScope();
            var _userService = scope.ServiceProvider.GetRequiredService<UserService>();

            var user = await _userService.GetUserProfileByTelegramIdAsync(chatId);
            if (user == null)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ پروفایل شما یافت نشد.",
                    parseMode: ParseMode.Markdown
                );
                return;
            }

            string safeFullName = EscapeMarkdown(user.FullName ?? "کاربر ناشناس");

            string message =
                "👤 *نام کاربری*: *" + safeFullName + "*\n" +
                "\u200F🆔 *شناسه کاربری*: `" + user.TelegramId + "`\n" +
                "📦 *کل سرویس‌ها*: " + user.SubscriptionCount + " عدد\n\n" +
                "🕒 *تاریخ عضویت*: " + PersianHelper.GetPersianCalendar(user.RegisterDate) + "\n" +
                "💳 *موجودی*: `" + user.Balance.ToString("N0") + "` تومان";

            var keyboard = new InlineKeyboardMarkup(new[]
                                {
                                     new[]
                                     {
                                         InlineKeyboardButton.WithCallbackData("➕ افزایش موجودی", "increase_balance")
                                     }
                                 });

            await botClient.SendMessage(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard
            );
        }

        private async Task SendDevelopingTextToUserAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);

            var message = callbackQuery.Message;

            var textMessage = """
             🚧 *این بخش در حال توسعه است!*

             این قابلیت هنوز آماده نشده و به‌زودی در دسترس شما قرار خواهد گرفت.  
             تیم ما با تمام توان در حال آماده‌سازی این بخش است. ⏳

             در حال حاضر می‌توانید از بخش‌های زیر استفاده کنید:
             🔹 خرید اشتراک  
             🔹 افزایش موجودی  
             🔹 پروفایل من

             ممنون از همراهی و شکیبایی شما 💙
             """;

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: textMessage,
                parseMode: ParseMode.Markdown
            );
        }

        public async Task NotifyAdminOfActivatedSubscriptionAsync(ITelegramBotClient botClient, Message message, string data)
        {
            var adminId = message.Chat.Id;

            var trackingCode = data.Split('_')[1];

            using var scope = _scopeFactory.CreateScope();
            var _subscriptionService = scope.ServiceProvider.GetRequiredService<SubscriptionService>();

            var subscription = await _subscriptionService.GetByTrackingCodeAsync(trackingCode);

            if (subscription == null)
            {
                string notFoundMessage = $"""
        ⚠️ *خطا در یافتن اشتراک*

        هیچ اشتراکی با کد پیگیری `{trackingCode}` پیدا نشد.

        لطفاً از صحت کد وارد شده اطمینان حاصل فرمایید.
        """;

                await SendTextToAdminsAsync(botClient, notFoundMessage);
                return;
            }

            string adminMessage = $"""
            ✅ *سرویس خریداری شده*
            
            👤 کاربر: *{subscription.User.FirstName + " " + (subscription.User.LastName ?? "")}*  
            🆔 شناسه عددی: `{subscription.User.TelegramId}`  
            📅 زمان خرید: {PersianHelper.GetPersianCalendar(subscription.CreatedAt)}
            
            🔹 سرویس: *{subscription.SubscriptionName}*
            📦 حجم: *{subscription.SubscriptionVolumeGb} گیگ*
            👥 کاربران مجاز: *{subscription.Service.AllowedUsersCount} نفر*
            📆 مدت اعتبار: *{subscription.Service.DurationInDays} روز*  
            💳 قیمت پرداختی: `{subscription.Service.Price:N0}` تومان
            
            📌 لطفاً کانفیگ مربوط به این سرویس را در پاسخ (ریپلای) به همین پیام برای کاربر ارسال نمایید.
            """;

            await SendTextToAdminsAsync(botClient, adminMessage);
        }

        private string EscapeMarkdown(string text)
        {
            return text
                .Replace("_", "-");
        }

        private static readonly List<(string Name, string Volume)> Subscriptions = Enumerable.Range(1, 10)
            .Select(i => ($"{i * 2} گیگ", $"Sub {i}"))
            .ToList();

    }
}
