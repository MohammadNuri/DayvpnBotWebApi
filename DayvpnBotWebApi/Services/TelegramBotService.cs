using DayvpnBotWebApi.Shared;
using Microsoft.OpenApi.Extensions;
using System.IO;
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

        public TelegramBotService(ITelegramBotClient botClient)
        {
            _botClient = botClient;
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
            Console.WriteLine("ربات متوقف شد.");
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update?.Message?.Text != null)
            {
                var message = update.Message;
                Console.WriteLine($"New Message From {message.From?.FirstName}: {message.Text}");

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

                    // اطلاعات کاربر برای ادمین
                    string fullName = $"{message.Chat.FirstName} {message.Chat.LastName}";
                    string userId = message.Chat.Id.ToString();
                    string subInfo = SubscriptionHelper.GetSubCost(message.Chat.Id); // قیمت

                    string caption = $"📥 درخواست پرداخت جدید دریافت شد.\n\n👤 نام کاربر: {fullName}\n🆔 آیدی عددی: {userId}\n💳 پلن انتخابی: {subInfo}\n\n📌 لطفاً بررسی و تأیید کنید.";

                    // ارسال عکس به ادمین همراه با کپشن
                    using var adminStream = new MemoryStream(stream.ToArray()); // برای اطمینان دوباره بخونیم
                    await botClient.SendPhoto(
                        chatId: (long)Admins.Nouri,
                        photo: new InputFileStream(adminStream, "payment.jpg"),
                        caption: caption,
                        parseMode: ParseMode.Markdown
                    );
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
                    default:
                        Console.WriteLine("Unknown Callback Data: " + update.CallbackQuery.Data);
                        break;
                }
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Error: {exception.Message}");
            return Task.CompletedTask;
        }


        // start
        private async Task Start(ITelegramBotClient botClient, Update update)
        {
            var message = update.Message;

            string photoPath = Path.Combine(Directory.GetCurrentDirectory(), "Src", "Images", "DayVPN.jpg");
            await using var stream = File.OpenRead(photoPath);
            await botClient.SendPhoto(
                chatId: message!.Chat.Id,
                photo: new InputFileStream(stream, "DayVPN.jpg"),
                caption: "🤖 خوش آمدید به ربات DayVPN!\r\n\r\n🔹 با این ربات، شما می‌توانید اشتراک وی‌پی‌ان خریداری کرده و از سرورهای متعدد با پروتکل‌های مختلف برای تغییر مکان خود استفاده کنید. \r\n\r\n📱 امکان اتصال در سیستم‌های اندروید، ویندوز، آیفون و دیگر دستگاه‌ها\r\n🌐 قابل استفاده بر روی تمامی انواع اینترنت‌ها\r\n             «🇩🇪 🇳🇱 🇺🇸 🇫🇷 🇹🇷 🇫🇮»\r\n\r\n🔻 برای شروع، یکی از گزینه‌های زیر را انتخاب کنید:",
                parseMode: ParseMode.Markdown,
                replyMarkup: new[] {InlineKeyboardButton.WithCallbackData("مدیریت اشتراک ها 🌐", "manage_subscriptions"),
                                    InlineKeyboardButton.WithCallbackData("🛒 خرید اشتراک", "buy_subscription") }
            );
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

            string photoPath = Path.Combine(Directory.GetCurrentDirectory(), "Src", "Images", "DayVPN.jpg");
            await using var stream = File.OpenRead(photoPath);
            await botClient.SendPhoto(
                chatId: message!.Chat.Id,
                photo: new InputFileStream(stream, "DayVPN.jpg"),
                caption: "🤖 خوش آمدید به ربات DayVPN!\r\n\r\n🔹 با این ربات، شما می‌توانید اشتراک وی‌پی‌ان خریداری کرده و از سرورهای متعدد با پروتکل‌های مختلف برای تغییر مکان خود استفاده کنید. \r\n\r\n📱 امکان اتصال در سیستم‌های اندروید، ویندوز، آیفون و دیگر دستگاه‌ها\r\n🌐 قابل استفاده بر روی تمامی انواع اینترنت‌ها\r\n             «🇩🇪 🇳🇱 🇺🇸 🇫🇷 🇹🇷 🇫🇮»\r\n\r\n🔻 برای شروع، یکی از گزینه‌های زیر را انتخاب کنید:",
                parseMode: ParseMode.Markdown,
                replyMarkup: new[] {InlineKeyboardButton.WithCallbackData("مدیریت اشتراک ها 🌐", "manage_subscriptions"),
                                    InlineKeyboardButton.WithCallbackData("🛒 خرید اشتراک", "buy_subscription") }
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
                Console.WriteLine($"Message Received From: {message.From.FirstName} {message.From.FirstName} \n Message: {message.Text}");
            }
        }
    }
}
