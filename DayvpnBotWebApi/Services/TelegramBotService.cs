using DayvpnBotWebApi.Shared;
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
                        await SetSubName(botClient, update);
                        break;
                }
            }
            else if (update?.Message?.Photo != null)
            {
                //Take Picture
                var message = update.Message;
                // بررسی اینکه کاربر اشتراک فعال داره
                if (SubscriptionHelper.HasSub(message.Chat.Id))
                {
                    var largestPhoto = message.Photo.Last();
                    var file = await botClient.GetFile(largestPhoto.FileId);

                    using var stream = new MemoryStream();
                    await botClient.DownloadFile(file.FilePath, stream);

                    // ذخیره عکس در حافظه برای این کاربر
                    SubscriptionHelper.SubmitPaymentPicture(message.From.Id, stream.ToArray());

                    // پیام به خود کاربر
                    await botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "✅ عکس پرداخت با موفقیت دریافت شد.\r\n🕓 لطفاً منتظر بمانید تا پرداخت شما توسط مدیریت بررسی و تأیید شود.\r\n📢 پس از تأیید، اطلاعات سرویس برای شما ارسال خواهد شد."
                    );

                    // اطلاعات کاربر برای ارسال به ادمین
                    string fullName = $"{message.Chat.FirstName} {message.Chat.LastName}";
                    string userId = message.Chat.Id.ToString();
                    string subInfo = SubscriptionHelper.GetSubCost(message.Chat.Id); // قیمت

                    string caption = $"📥 درخواست پرداخت جدید دریافت شد.\n\n👤 نام کاربر: {fullName}\n🆔 آیدی عددی: {userId}\n💳 پلن انتخابی: {subInfo}\n\n📌 لطفاً بررسی و تأیید کنید.";

                    // ارسال عکس به ادمین همراه با کپشن
                    using var adminStream = new MemoryStream(stream.ToArray()); // برای اطمینان دوباره بخونیم

                    await SendPhotoToAdminsAsync(botClient, adminStream, caption);
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

                    default:
                        Console.WriteLine("Unknown Callback Data: " + update.CallbackQuery.Data);
                        break;
                }
            }
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


            SubscriptionHelper.AddSub(message.Chat.Id, "", subMode);

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

            if (SubscriptionHelper.HasSub(message.Chat.Id))
            {
                const string SubNameRegex = "^(?i)[a-z0-9 ]{1,30}$";
                if (Regex.IsMatch(message.Text, SubNameRegex))
                {
                    // Submit Sub Name
                    SubscriptionHelper.SubmitSubName(message.Chat.Id, message.Text.Trim());

                    string subCost = SubscriptionHelper.GetSubCost(message.Chat.Id);

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
        private async Task SendPhotoToAdminsAsync(ITelegramBotClient botClient, Stream photoStream, string caption)
        {
            long[] adminIds = { (long)Admins.Nouri /*, Admins.OtherAdminId */ };

            foreach (var adminId in adminIds)
            {
                using var streamCopy = new MemoryStream();
                await photoStream.CopyToAsync(streamCopy);
                streamCopy.Position = 0;

                await botClient.SendPhoto(
                    chatId: adminId,
                    photo: new InputFileStream(streamCopy, "attachment.jpg"),
                    caption: caption,
                    parseMode: ParseMode.Markdown
                );
            }
        }

        private static readonly List<(string Name, string Volume)> Subscriptions = Enumerable.Range(1, 10)
            .Select(i => ($"{i * 2} گیگ", $"Sub {i}"))
            .ToList();

    }
}
