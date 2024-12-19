using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types.InputFiles;
using File = System.IO.File;

namespace BlackSpartanBot.Handlers
{
    public static class YoutubeHandler
    {
        private static readonly YoutubeClient youtube = new YoutubeClient();
        private static readonly Dictionary<long, MuxedStreamInfo[]> UserStreamSelections = new();

        // نمایش کیفیت‌های موجود به کاربر
        public static async Task ShowAvailableQualities(ITelegramBotClient botClient, long chatId, string videoUrl, CancellationToken cancellationToken)
        {
            try
            {
                // تبدیل لینک کوتاه‌شده به لینک استاندارد
                if (Regex.IsMatch(videoUrl, @"youtu\.be\/"))
                {
                    videoUrl = videoUrl.Replace("youtu.be/", "www.youtube.com/watch?v=");
                }

                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoUrl);
                var streams = streamManifest.GetMuxedStreams()
                                            .OrderByDescending(s => s.VideoQuality.Label)
                                            .ToArray();

                if (!streams.Any())
                {
                    await botClient.SendTextMessageAsync(chatId, "کیفیتی برای این ویدیو یافت نشد.", cancellationToken: cancellationToken);
                    return;
                }

                UserStreamSelections[chatId] = streams;

                // ساخت دکمه‌ها برای هر کیفیت ویدیو
                var buttons = streams.Select((stream, index) =>
                    InlineKeyboardButton.WithCallbackData(
                        text: $"{stream.VideoQuality.Label} - {stream.Container} ({stream.Size.MegaBytes:F1} MB)",
                        callbackData: index.ToString()
                    )).ToList();

                var inlineKeyboard = new InlineKeyboardMarkup(buttons.Select(b => new[] { b }));

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "لطفاً یکی از کیفیت‌های زیر را انتخاب کنید:",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در دریافت کیفیت‌ها: {ex.Message}");
                await botClient.SendTextMessageAsync(chatId, "خطایی در پردازش ویدیو رخ داد.", cancellationToken: cancellationToken);
            }
        }

        // دانلود و ارسال ویدیو
        public static async Task DownloadAndSendVideo(ITelegramBotClient botClient, long chatId, string selectedStreamIndex, CancellationToken cancellationToken)
        {
            try
            {
                if (!UserStreamSelections.TryGetValue(chatId, out var streams) ||
                    !int.TryParse(selectedStreamIndex, out var index) || index >= streams.Length)
                {
                    await botClient.SendTextMessageAsync(chatId, "کیفیت انتخاب‌شده نامعتبر است.", cancellationToken: cancellationToken);
                    return;
                }

                var streamInfo = streams[index];
                var fileName = $"video_{streamInfo.VideoQuality.Label}.mp4";

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"در حال دانلود ویدیو با کیفیت {streamInfo.VideoQuality.Label}. لطفاً صبر کنید...",
                    cancellationToken: cancellationToken
                );

                // دانلود ویدیو
                await youtube.Videos.Streams.DownloadAsync(streamInfo, fileName);

                Console.WriteLine($"دانلود ویدیو به پایان رسید: {fileName}");

                // ارسال ویدیو به کاربر
                await using var videoStream = File.OpenRead(fileName);
                var inputFile = new InputOnlineFile(videoStream, fileName); // استفاده از InputOnlineFile به‌جای InputFile

                await botClient.SendVideoAsync(
                    chatId: chatId,
                    video: inputFile,
                    caption: $"ویدیو با کیفیت {streamInfo.VideoQuality.Label} آماده است!",
                    cancellationToken: cancellationToken
                );


                videoStream.Close();
                File.Delete(fileName);
                Console.WriteLine("فایل حذف شد.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در دانلود یا ارسال ویدیو: {ex.Message}");
                await botClient.SendTextMessageAsync(chatId, "مشکلی در دانلود یا ارسال ویدیو رخ داد.", cancellationToken: cancellationToken);
            }
        }
    }
}
