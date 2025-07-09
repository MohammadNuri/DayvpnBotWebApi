using DayvpnBotWebApi.Core.Entities;
using DayvpnBotWebApi.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using System.Runtime.CompilerServices;
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
            CustomMemoryCash.ClearExpiredCash();

            if (update?.Message?.Text != null)
            {
                var message = update.Message;

                await SignupUserAsync(botClient, message);

                Console.OutputEncoding = System.Text.Encoding.UTF8; // فعال کردن UTF-8

                var originalColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("📩 Message Received From: ");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{message.From.FirstName} {message.From.LastName}");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("📝 Text: ");

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message.Text);

                Console.ForegroundColor = originalColor;

                switch (message.Text.ToLower())
                {
                    case "/start":
                        await Start(botClient, update);
                        break;

                    default:
                        var state = CustomMemoryCash.GetUserState(message.Chat.Id);
                        if (state != null && state == UserState.Buy_Subscription)
                            await SetSubName(botClient, update);
                        else if (state != null && state == UserState.Increase_Balance)
                            await SetBalance(botClient, update);
                        break;
                }
            }
            else if (update?.Message?.Photo != null)
            {
                //Take Pictures
                var message = update.Message;
                if (CustomMemoryCash.GetUserState(message.Chat.Id) == UserState.Increase_Balance)
                {
                    // بررسی اینکه کاربر اشتراک فعال داره
                    if (CustomMemoryCash.HasBalanceRequest(message.Chat.Id))
                    {
                        var largestPhoto = message.Photo.Last();
                        var file = await botClient.GetFile(largestPhoto.FileId);

                        try
                        {
                            using var stream = new MemoryStream();
                            await botClient.DownloadFile(file.FilePath, stream);

                            // ذخیره عکس در حافظه برای این کاربر
                            CustomMemoryCash.SubmitPaymentPicture(message.From.Id, stream.ToArray());

                            // اطلاعات کاربر برای ارسال به ادمین
                            string fullName = $"{message.Chat.FirstName} {message.Chat.LastName ?? ""}".Trim();
                            string userId = message.Chat.Id.ToString();
                            string balance = CustomMemoryCash.GetRequestedBalance(message.Chat.Id); // قیمت

                            string caption = $"📥 درخواست پرداخت جدید دریافت شد.\n\n👤 نام کاربر: {fullName}\n🆔 آیدی عددی: {userId}\n💳 مبلغ: {balance}\n\n📌 لطفاً بررسی و تأیید کنید.";

                            // ارسال عکس به ادمین همراه با کپشن
                            using var adminStream = new MemoryStream(stream.ToArray()); // برای اطمینان دوباره بخونیم

                            await SendConfirmPhotoToAdminsAsync(botClient, adminStream, caption, message.Chat.Id);

                            // پیام به خود کاربر
                            await botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "✅ عکس پرداخت با موفقیت دریافت شد.\r\n🕓 لطفاً منتظر بمانید تا پرداخت شما توسط مدیریت بررسی و تأیید شود.\r\n📢."
                            );
                        }
                        catch (Exception)
                        {
                            await botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "❌ مشکلی در دریافت تصویر رخ داد. لطفاً دوباره تلاش کنید یا با پشتیبانی در تماس باشید."
                            );
                        }
                    }
                }
                else
                {
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ خطای رخ داده لطفا مراحل رو از ابتدا شروع کنید و یا با ادمین هماهنگ کنید"
                    );
                }
            }
            else if (update?.CallbackQuery != null)
            {
                // تایید پرداخت
                if (update.CallbackQuery.Data.StartsWith("confirm_payment"))
                {
                    await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);

                    if (!long.TryParse(update.CallbackQuery.Data.Split(':')[1], out long userId))
                    {
                        await SendTextToAdminsAsync(botClient, "در تایید پرداخت خطایی رخ داده!!!");
                        await SendRestartMessageToUser(botClient, update.CallbackQuery);
                        return;
                    }

                    if (!CustomMemoryCash.HasBalanceRequest(userId))
                    {
                        await SendTextToAdminsAsync(botClient, "کاربر در کش برای تایید پرداخت وجود ندارد!!!");
                        await SendRestartMessageToUser(botClient, update.CallbackQuery);
                        return;
                    }

                    var balanceRequest = CustomMemoryCash.GetBalanceRequest(userId);

                    using var scope = _scopeFactory.CreateScope();
                    var _userService = scope.ServiceProvider.GetRequiredService<UserService>();

                    var result = await _userService.AddUserBalanceAsync(balanceRequest);
                    if (!result.IsSuccess)
                    {
                        await SendTextToAdminsAsync(botClient,
                             $"❌ افزایش موجودی *ناموفق* بود.\n\n👤 کاربر با آیدی عددی: `{balanceRequest.UserId}` در کش یافت نشد یا مشکلی رخ داده است.\n💳 مبلغ درخواستی: `{balanceRequest.Balance:N0}` تومان");

                        await botClient.SendMessage(
                            chatId: balanceRequest.UserId,
                            text: """
❌ متأسفانه افزایش موجودی شما با مشکل مواجه شد.

لطفاً مجدداً تلاش کنید یا برای بررسی دقیق‌تر با پشتیبانی در ارتباط باشید.

🆘 آیدی پشتیبانی: @DarvyXe
""",
                            parseMode: ParseMode.Markdown
                        );
                    }
                    else
                    {
                        await SendTextToAdminsAsync(botClient,
                            $"✅ افزایش موجودی با موفقیت انجام شد.\n\n👤 کاربر با آیدی عددی: `{balanceRequest.UserId}`\n💳 مبلغ افزوده شده: `{balanceRequest.Balance:N0}` تومان");

                        await botClient.SendMessage(
                            chatId: balanceRequest.UserId,
                            text: $"🎉 موجودی شما با موفقیت افزایش یافت!\n\n💳 مبلغ: `{balanceRequest.Balance:N0}` تومان\n\nاز خرید شما سپاسگزاریم 🙏\nاکنون می‌توانید از خدمات ما استفاده کنید.",
                            parseMode: ParseMode.Markdown
                        );
                    }
                    CustomMemoryCash.ClearCash(userId);
                }
                // عدم تایید پرداخت
                if (update.CallbackQuery.Data.StartsWith("reject_payment"))
                {
                    await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);

                    if (!long.TryParse(update.CallbackQuery.Data.Split(':')[1], out long userId))
                    {
                        await SendTextToAdminsAsync(botClient, "در تایید پرداخت خطایی رخ داده!!!");
                        await SendRestartMessageToUser(botClient, update.CallbackQuery);
                        return;
                    }

                    if (!CustomMemoryCash.HasBalanceRequest(userId))
                    {
                        await SendTextToAdminsAsync(botClient, "کاربر در کش برای عدم تایید پرداخت وجود ندارد!!!");
                        await SendRestartMessageToUser(botClient, update.CallbackQuery);
                        return;
                    }

                    await botClient.SendMessage(
                        chatId: update.CallbackQuery.Message.Chat.Id, // این مقدار باید از callbackData استخراج بشه
                        text: """
❌ پرداخت شما تأیید نشد.

ممکن است رسید ارسال‌شده نامعتبر یا ناقص بوده باشد، یا پرداختی در سیستم ثبت نشده باشد.

🛠 لطفاً مجدداً رسید معتبر ارسال کنید یا برای بررسی بیشتر با پشتیبانی در تماس باشید.

🆘 آیدی پشتیبانی: @DarvyXe
""",
                        parseMode: ParseMode.Markdown
                    );
                    CustomMemoryCash.ClearCash(update.CallbackQuery.Message.Chat.Id);
                }

                switch (update.CallbackQuery.Data)
                {
                    case "buy_subscription":
                        await BuySubscription(botClient, update.CallbackQuery, false);
                        break;

                    case "back-to-home":
                        await Start(botClient, update.CallbackQuery, true);
                        break;

                    case "back-to-buy-subscription":
                        await BuySubscription(botClient, update.CallbackQuery, true);
                        break;

                    case "my_subscriptions":
                        await MySubscriptions(botClient, update.CallbackQuery);
                        break;

                    case "increase_balance":
                        await IncreaseBalance(botClient, update.CallbackQuery);
                        break;

                    case $"sub_1":
                        await HandleBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "1️⃣ انتخاب پلن\r\n🟢 پلن 5 گیگ\r\n\r\n⏱️ مدت اعتبار: 30 روز\r\n📥 حجم سرویس: 5 گیگابایت\r\n👤 تعداد کاربران: 1 نفر\r\n💸 قیمت نهایی: 45,000 تومان\r\n\r\n💳 برای تکمیل خرید و دریافت کانفیگ، دکمه زیر را فشار دهید:",
                            "confirm-buy-sub-1");
                        break;

                    case "sub_2":
                        await HandleBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "2️⃣ انتخاب پلن\r\n🔵 پلن 10 گیگ\r\n\r\n⏱️ مدت اعتبار: 30 روز\r\n📥 حجم سرویس: 10 گیگابایت\r\n👤 تعداد کاربران: 1 نفر\r\n💸 قیمت نهایی: 65,000 تومان\r\n\r\n💳 برای تکمیل خرید و دریافت کانفیگ، دکمه زیر را فشار دهید:",
                            "confirm-buy-sub-2");
                        break;

                    case "sub_3":
                        await HandleBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "3️⃣ انتخاب پلن\r\n🟤 پلن 20 گیگ\r\n\r\n⏱️ مدت اعتبار: 30 روز\r\n📥 حجم سرویس: 20 گیگابایت\r\n👤 تعداد کاربران: 1 نفر\r\n💸 قیمت نهایی: 80,000 تومان\r\n\r\n💳 برای تکمیل خرید و دریافت کانفیگ، دکمه زیر را فشار دهید:",
                            "confirm-buy-sub-3");
                        break;

                    case "sub_4":
                        await HandleBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "4️⃣ انتخاب پلن\r\n⚪ پلن 30 گیگ\r\n\r\n⏱️ مدت اعتبار: 30 روز\r\n📥 حجم سرویس: 30 گیگابایت\r\n👤 تعداد کاربران: 1 نفر\r\n💸 قیمت نهایی: 100,000 تومان\r\n\r\n💳 برای تکمیل خرید و دریافت کانفیگ، دکمه زیر را فشار دهید:",
                            "confirm-buy-sub-4");
                        break;

                    case "sub_5":
                        await HandleBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "5️⃣ انتخاب پلن\r\n🟡 پلن 75 گیگ - 4 کاربر\r\n\r\n⏱️ مدت اعتبار: 30 روز\r\n📥 حجم سرویس: 75 گیگابایت\r\n👥 تعداد کاربران: 4 نفر\r\n💸 قیمت نهایی: 185,000 تومان\r\n\r\n💳 برای تکمیل خرید و دریافت کانفیگ، دکمه زیر را فشار دهید:",
                            "confirm-buy-sub-5");
                        break;

                    case "sub_6":
                        await HandleBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "6️⃣ انتخاب پلن\r\n🔶 پلن 90 گیگ - 90 روزه\r\n\r\n⏱️ مدت اعتبار: 90 روز\r\n📥 حجم سرویس: 90 گیگابایت\r\n👤 تعداد کاربران: 1 نفر\r\n💸 قیمت نهایی: 215,000 تومان\r\n\r\n💳 برای تکمیل خرید و دریافت کانفیگ، دکمه زیر را فشار دهید:",
                            "confirm-buy-sub-6");
                        break;

                    case "sub_7":
                        await HandleBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "7️⃣ انتخاب پلن\r\n🔷 پلن 100 گیگ - 90 روزه\r\n\r\n⏱️ مدت اعتبار: 90 روز\r\n📥 حجم سرویس: 100 گیگابایت\r\n👥 تعداد کاربران: 2 نفر\r\n💸 قیمت نهایی: 240,000 تومان\r\n\r\n💳 برای تکمیل خرید و دریافت کانفیگ، دکمه زیر را فشار دهید:",
                            "confirm-buy-sub-7");
                        break;

                    case "sub_8":
                        await HandleBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "8️⃣ انتخاب پلن\r\n💎 پلن 150 گیگ - 90 روزه\r\n\r\n⏱️ مدت اعتبار: 90 روز\r\n📥 حجم سرویس: 150 گیگابایت\r\n👥 تعداد کاربران: 4 نفر\r\n💸 قیمت نهایی: 300,000 تومان\r\n\r\n💳 برای تکمیل خرید و دریافت کانفیگ، دکمه زیر را فشار دهید:",
                            "confirm-buy-sub-8");
                        break;

                    case "confirm-buy-sub-1":
                        await HandleConfirmBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "sub_1");
                        break;
                    case "confirm-buy-sub-2":
                        await HandleConfirmBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "sub_2");
                        break;
                    case "confirm-buy-sub-3":
                        await HandleConfirmBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "sub_3");
                        break;
                    case "confirm-buy-sub-4":
                        await HandleConfirmBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "sub_4");
                        break;
                    case "confirm-buy-sub-5":
                        await HandleConfirmBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "sub_5");
                        break;
                    case "confirm-buy-sub-6":
                        await HandleConfirmBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "sub_6");
                        break;
                    case "confirm-buy-sub-7":
                        await HandleConfirmBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "sub_7");
                        break;
                    case "confirm-buy-sub-8":
                        await HandleConfirmBuySubscription(
                            botClient,
                            update.CallbackQuery,
                            "sub_8");
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
                    default:
                        Console.WriteLine("Unknown Callback Data: " + update.CallbackQuery.Data);
                        break;
                }
            }
        }

        private async Task SendRestartMessageToUser(ITelegramBotClient botClient, Message message)
        {
            string text = "❗ خطایی رخ داده یا زمان انجام عملیات شما منقضی شده است.\n\n" +
                          "لطفاً با ارسال دستور /start مجدداً فرایند را از ابتدا آغاز کنید.\n\n" +
                          "در صورت بروز مشکل، با پشتیبانی تماس بگیرید:\n" +
                          "@DarvyXe";

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
                          "@DarvyXe";

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: text
            );
        }


        private async Task SignupUserAsync(ITelegramBotClient botClient, Message message)
        {
            using var scope = _scopeFactory.CreateScope();
            var _userService = scope.ServiceProvider.GetRequiredService<UserService>();

            // Register User
            if (!await _userService.CheckUserExists(message.Chat.Id))
            {
                Core.Entities.User user = new Core.Entities.User()
                {
                    TelegramId = message.Chat.Id,
                    FirstName = message.Chat.FirstName ?? string.Empty,
                    LastName = message.Chat.LastName ?? string.Empty,
                    RegistrationDate = DateTime.UtcNow,
                    Balance = 0,
                };

                ServiceResult result = await _userService.RegisterUser(user);

                await SendTextToAdminsAsync(botClient, result.Message);

                Console.Write($"Success: {result.IsSuccess}");
                Console.Write($"Message: {result.Message}");
            }
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
        private async Task Start(ITelegramBotClient botClient, Update update)
        {
            var message = update.Message;
            await GlobalStart(botClient, message);
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
                await GlobalStart(botClient, message);
            }
        }

        private async Task GlobalStart(ITelegramBotClient botClient, Message message)
        {
            string fullName = $"{message.Chat.FirstName} {message.Chat.LastName}";

            string welcomeText = $"👋 سلام {fullName} عزیز!\n\n" +
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
                    InlineKeyboardButton.WithCallbackData("👤 پروفایل من", "my_profile"),
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
        private async Task BuySubscription(ITelegramBotClient botClient, CallbackQuery callBackQuery, bool removeLastMessage)
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

            await botClient.SendMessage(
                chatId: message!.Chat.Id,
                text: "🎯 خرید سرویس DayVPN\r\n\r\n\U0001f6d2 در 2 مرحله، اشتراک اختصاصی خود را دریافت کنید!\r\n💡 همه سرویس‌ها شامل تمامی سرورهای DayVPN هستند.\r\n🌍 قابلیت اتصال به هر لوکیشن، در هر زمان!\r\n\r\n🔻 یکی از پلن‌های زیر را انتخاب کنید:",
                parseMode: ParseMode.Markdown,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("🟢 30 روزه - 5 گیگ - 1 کاربر - 45,000 تومان", "sub_1") },
                    new[] { InlineKeyboardButton.WithCallbackData("🔵 30 روزه - 10 گیگ - 1 کاربر - 65,000 تومان", "sub_2") },
                    new[] { InlineKeyboardButton.WithCallbackData("🟤 30 روزه - 20 گیگ - 1 کاربر - 80,000 تومان", "sub_3") },
                    new[] { InlineKeyboardButton.WithCallbackData("⚪ 30 روزه - 30 گیگ - 1 کاربر - 100,000 تومان", "sub_4") },
                    new[] { InlineKeyboardButton.WithCallbackData("🟡 30 روزه - 75 گیگ - 4 کاربر - 185,000 تومان", "sub_5") },
                    new[] { InlineKeyboardButton.WithCallbackData("🔶 90 روزه - 90 گیگ - 1 کاربر - 215,000 تومان", "sub_6") },
                    new[] { InlineKeyboardButton.WithCallbackData("🔷 90 روزه - 100 گیگ - 2 کاربر - 240,000 تومان", "sub_7") },
                    new[] { InlineKeyboardButton.WithCallbackData("💎 90 روزه - 150 گیگ - 4 کاربر - 300,000 تومان", "sub_8") },
                    new[] { InlineKeyboardButton.WithCallbackData("بازگشت به صفحه اصلی 🏠", "back-to-home") }
                }));

        }

        private async Task HandleBuySubscription(ITelegramBotClient botClient, CallbackQuery callBackQuery, string confirmText, string subscriptionFunction)
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

            await botClient.SendMessage(
                chatId: message!.Chat.Id,
                text: confirmText,
                parseMode: ParseMode.Markdown,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("تایید و خرید ✅", subscriptionFunction) },
                    new[] { InlineKeyboardButton.WithCallbackData("بازگشت 🔙", "back-to-buy-subscription") },
                    new[] { InlineKeyboardButton.WithCallbackData("بازگشت به صفحه اصلی 🏠", "back-to-home") },
                }));
        }

        private async Task HandleConfirmBuySubscription(ITelegramBotClient botClient, CallbackQuery callBackQuery, string callBackFunction)
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

            if (!Enum.TryParse<SubMode>("sub_1", out SubMode subMode))
                Console.WriteLine("Cannot Find SubMode");

            CustomMemoryCash.AddSubscription(message.Chat.Id, "", subMode);

            await botClient.SendMessage(
                chatId: message!.Chat.Id,
                text: "📝 لطفاً نام کانفیگ یا پیکربندی خود را به صورت انگلیسی وارد کنید:\r\n\r\n📌 این نام برای ارسال تنظیمات اختصاصی شما استفاده خواهد شد.",
                parseMode: ParseMode.Markdown,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("بازگشت 🔙", callBackFunction) },
                }));
        }

        private async Task SetSubName(ITelegramBotClient botClient, Update update)
        {
            var message = update.Message;

            if (CustomMemoryCash.HasSubscription(message.Chat.Id))
            {
                const string SubNameRegex = "^(?i)[a-z0-9 ]{1,30}$";
                if (Regex.IsMatch(message.Text, SubNameRegex))
                {
                    // Submit Sub Name
                    CustomMemoryCash.SubmitSubscriptionName(message.Chat.Id, message.Text.Trim());

                    string subCost = CustomMemoryCash.GetRequestedBalance(message.Chat.Id);

                    string paymentText = $"2️⃣ پرداخت و تأیید نهایی\r\n\r\n📛 نام کانفیگ شما با موفقیت ثبت شد.\r\n📦 حالا نوبت پرداخت مبلغ سرویس شماست.\r\n\r\n💳 لطفاً مبلغ {subCost} تومان را به کارت زیر واریز فرمایید:\r\n\r\n🏦 بانک: بلو (BluBank)\r\n👤 نام صاحب حساب: محمد نوری\r\n💳 شماره کارت: 6219 8619 6361 0935\r\n\r\n\U0001f9fe پس از واریز، تصویر فیش پرداختی را برای ما ارسال کنید تا کانفیگ شما فعال شود.\r\n\r\n⚠️ لطفاً فیش واریزی را ارسال فرمایید.";

                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: paymentText,
                        parseMode: ParseMode.Markdown
                        );
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
                Console.OutputEncoding = System.Text.Encoding.UTF8; // فعال کردن UTF-8

                var originalColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("📩 Trash Message Received From: ");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{message.From.FirstName} {message.From.LastName}");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("📝 Text: ");

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message.Text);

                Console.ForegroundColor = originalColor;
            }
        }

        private async Task IncreaseBalance(ITelegramBotClient botClient, CallbackQuery callbackQuery)
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id);

            var chatId = callbackQuery.Message.Chat.Id;

            string message = """
    💳 افزایش موجودی

    💰 لطفاً مبلغ مورد نظر خود را *به تومان* وارد کنید:
    🔹 مثال: `100000` (معادل ۱۰۰ هزار تومان)

    ⚠️ لطفاً مبلغ را *کاملاً دقیق* و فقط به عدد وارد نمایید.
    در صورتی که مبلغ نادرست وارد شود، پردازش افزایش موجودی ممکن است با *تأخیر* انجام شود.

    🔐 پرداخت شما کاملاً امن بوده و اطلاعات محرمانه محفوظ می‌ماند.
    """;

            await botClient.SendMessage(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown
            );

            // به‌روزرسانی وضعیت کاربر برای مرحله بعد
            CustomMemoryCash.AddBalance(chatId);
        }


        private async Task SetBalance(ITelegramBotClient botClient, Update update)
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

            CustomMemoryCash.SetBalance(chatId, amount);

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

            foreach (var adminId in adminIds)
            {
                await botClient.SendMessage(
                    chatId: adminId,
                    text: message,
                    parseMode: ParseMode.Markdown
                );
            }
        }

        // ارسال عکس و متن به ادمین ها
        private async Task SendConfirmPhotoToAdminsAsync(ITelegramBotClient botClient, Stream photoStream, string caption, long userId)
        {
            long[] adminIds = { (long)Admins.Nouri /*, Admins.OtherAdminId */ };

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ تأیید پرداخت", $"confirm_payment:{userId}"),
                    InlineKeyboardButton.WithCallbackData("❌ رد پرداخت", $"reject_payment:{userId}")
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

            string message = $"""
👤 *پروفایل شما*

🧑‍💼 نام: *{user.FullName}*
🆔 آیدی تلگرام: `{user.TelegramId}`

🗓 تاریخ ثبت‌نام: {user.RegisterDate:yyyy/MM/dd}
💳 موجودی: `{user.Balance:N0}` تومان
📦 تعداد اشتراک‌ها: {user.SubscriptionCount}

برای بروزرسانی اطلاعات یا دریافت پشتیبانی با ما در ارتباط باشید.
""";

            await botClient.SendMessage(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown
            );
        }

        private static readonly List<(string Name, string Volume)> Subscriptions = Enumerable.Range(1, 10)
            .Select(i => ($"{i * 2} گیگ", $"Sub {i}"))
            .ToList();

    }
}
