using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using BlackSpartanBot.Handlers;

partial class Program
{
    private static readonly string BotToken = "7813073080:AAH3EFArgpyKxRcr4hU-Br0Cn-6dhXE_qQw"; // توکن ربات خود را وارد کنید.

    static async Task Main()
    {
        var botClient = new TelegramBotClient(BotToken);

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"ربات {me.Username} آماده به کار است.");

        using var cts = new CancellationTokenSource();

        // حلقه برای پردازش پیام‌ها
        await ProcessUpdates(botClient, cts.Token);

        Console.WriteLine("برای توقف ربات کلید Enter را فشار دهید.");
        Console.ReadLine();
        cts.Cancel();
    }

    private static async Task ProcessUpdates(ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        int offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await botClient.GetUpdatesAsync(offset, cancellationToken: cancellationToken);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;

                    if (update.Type == UpdateType.Message && update.Message?.Text != null)
                    {
                        await HandleMessage(botClient, update.Message, cancellationToken);
                    }
                    else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                    {
                        await HandleCallbackQuery(botClient, update.CallbackQuery, cancellationToken);
                    }
                }

                await Task.Delay(1000, cancellationToken); // یک ثانیه تاخیر برای کاهش فشار روی API
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در پردازش آپدیت‌ها: {ex.Message}");
            }
        }
    }

    private static async Task HandleMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var messageText = message.Text;

        // مدیریت پیام‌های متنی کاربر
        if (PinterestHandler.IsWaitingForPhotoLink(chatId))
        {
            await PinterestHandler.HandlePinterestMessage(botClient, chatId, messageText, cancellationToken);
        }
        else if (messageText == "/start")
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🎥 دانلود از یوتیوب 🎥", "youtube") },
                new[] { InlineKeyboardButton.WithCallbackData("🐦 دانلود از توییتر 🐦", "twitter") },
                new[] { InlineKeyboardButton.WithCallbackData("📸 دانلود از اینستاگرام 📸", "instagram") },
                new[] { InlineKeyboardButton.WithCallbackData("📌 دانلود از پینترست 📌", "pinterest") },
                new[] { InlineKeyboardButton.WithCallbackData("🎵 دانلود از اسپاتیفای 🎵", "spotify") },
                new[] { InlineKeyboardButton.WithCallbackData("💎 دانلود از تیدال 💎", "tidal") },
                new[] { InlineKeyboardButton.WithCallbackData("🎧 دانلود از ساندکلاد 🎧", "soundcloud") },
                new[] { InlineKeyboardButton.WithCallbackData("🍏 دانلود از اپل موزیک 🍏", "apple_music") }
            });

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "به ربات ما خوش آمدید! لطفاً یکی از گزینه‌های زیر را انتخاب کنید:",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "دستور نامعتبر است! لطفاً از /start استفاده کنید.",
                cancellationToken: cancellationToken
            );
        }
    }

    private static async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        var callbackData = callbackQuery.Data;

        if (callbackData == "pinterest")
        {
            // انتقال به منوی پینترست
            await PinterestHandler.ShowPinterestMenu(botClient, chatId, cancellationToken);
        }
        else if (callbackData.StartsWith("pinterest_"))
        {
            // مدیریت انتخاب‌های پینترست
            await PinterestHandler.HandlePinterestCallback(botClient, chatId, callbackData, cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "این گزینه هنوز پشتیبانی نمی‌شود.",
                cancellationToken: cancellationToken
            );
        }
    }
}
