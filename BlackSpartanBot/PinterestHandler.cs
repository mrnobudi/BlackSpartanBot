using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using File = System.IO.File;

namespace BlackSpartanBot.Handlers
{
    public static class PinterestHandler
    {
        private static readonly ConcurrentDictionary<long, string> UserSelections;
        private static readonly HttpClient HttpClient;

        static PinterestHandler()
        {
            try
            {
                Console.WriteLine("[INFO] Initializing UserSelections...");
                UserSelections = new ConcurrentDictionary<long, string>();
                Console.WriteLine("[INFO] UserSelections initialized successfully.");

                Console.WriteLine("[INFO] Initializing HttpClient with custom HttpClientHandler...");
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };

                HttpClient = new HttpClient(handler);
                Console.WriteLine("[INFO] HttpClient initialized successfully.");
                Console.WriteLine("       - AllowAutoRedirect: true");
                Console.WriteLine("       - AutomaticDecompression: GZip, Deflate");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to initialize static members: {ex.Message}");
                throw;
            }
        }
        
        public static bool IsWaitingForPhotoLink(long chatId)
        {
            bool isWaiting = UserSelections.TryGetValue(chatId, out var selection) && selection == "photo";
            Console.WriteLine($"[INFO] IsWaitingForPhotoLink called for chatId: {chatId}, IsWaiting: {isWaiting}, CurrentSelection: {(selection ?? "None")}");
            return isWaiting;
        }

        public static bool IsWaitingForVideoLink(long chatId)
        {
            bool isWaiting = UserSelections.TryGetValue(chatId, out var selection) && selection == "video";
            Console.WriteLine($"[INFO] IsWaitingForVideoLink called for chatId: {chatId}, IsWaiting: {isWaiting}, CurrentSelection: {(selection ?? "None")}");
            return isWaiting;
        }
        
        public static async Task ShowPinterestMenu(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"[INFO] ShowPinterestMenu called for chatId: {chatId}");

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("📷 دانلود عکس", "pinterest_photo") },
                    new[] { InlineKeyboardButton.WithCallbackData("🎥 دانلود فیلم", "pinterest_video") }
                });

                Console.WriteLine($"[INFO] InlineKeyboard generated for chatId: {chatId}");

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "لطفاً یکی از گزینه‌های زیر را انتخاب کنید:",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken
                );

                Console.WriteLine($"[SUCCESS] Pinterest menu sent to chatId: {chatId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to send Pinterest menu to chatId: {chatId}. Error: {ex.Message}");
            }
        }
        
        public static async Task HandlePinterestCallback(ITelegramBotClient botClient, long chatId, string callbackData, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[INFO] HandlePinterestCallback called for chatId: {chatId}, callbackData: {callbackData}");

            if (callbackData == "pinterest_photo")
            {
                Console.WriteLine($"[INFO] User selected photo download for chatId: {chatId}");
                await botClient.SendTextMessageAsync(
                    chatId,
                    "لینک پست مد نظر خود را وارد کنید:",
                    cancellationToken: cancellationToken
                );
                UserSelections[chatId] = "photo";
                Console.WriteLine($"[SUCCESS] User selection updated to 'photo' for chatId: {chatId}");
            }
            else if (callbackData == "pinterest_video")
            {
                Console.WriteLine($"[INFO] User selected video download for chatId: {chatId}");
                await botClient.SendTextMessageAsync(
                    chatId,
                    "لینک ویدیوی مد نظر خود را وارد کنید:",
                    cancellationToken: cancellationToken
                );
                UserSelections[chatId] = "video"; // ذخیره انتخاب برای ویدیو
                Console.WriteLine($"[SUCCESS] User selection updated to 'video' for chatId: {chatId}");
            }
            else
            {
                Console.WriteLine($"[WARNING] Unknown callbackData received for chatId: {chatId}, callbackData: {callbackData}");
                await botClient.SendTextMessageAsync(
                    chatId,
                    "گزینه انتخاب‌شده نامعتبر است. لطفاً دوباره تلاش کنید.",
                    cancellationToken: cancellationToken
                );
            }
        }
        
        public static async Task HandlePinterestMessage(ITelegramBotClient botClient, long chatId, string messageText, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[INFO] HandlePinterestMessage called for chatId: {chatId}, message: {messageText}, UserSelections exists: {UserSelections.ContainsKey(chatId)}");

            if (IsWaitingForPhotoLink(chatId) || IsWaitingForVideoLink(chatId))
            {
                Console.WriteLine($"[INFO] Processing started for chatId: {chatId}");

                // بررسی و تبدیل لینک کوتاه
                if (messageText.StartsWith("https://pin.it"))
                {
                    Console.WriteLine($"[INFO] Shortened URL detected: {messageText}");
                    messageText = await ResolveShortenedPinterestLink(messageText);
                    Console.WriteLine($"[INFO] Resolved URL: {messageText}");
                    if (string.IsNullOrEmpty(messageText))
                    {
                        Console.WriteLine($"[ERROR] Failed to resolve shortened URL for chatId: {chatId}");
                        await botClient.SendTextMessageAsync(
                            chatId,
                            "خطا در تبدیل لینک کوتاه. لطفاً لینک کامل پینترست را ارسال کنید.",
                            cancellationToken: cancellationToken
                        );
                        return;
                    }
                }

                // استخراج لینک معتبر
                var validUrl = ExtractValidPinterestLink(messageText);
                Console.WriteLine($"[DEBUG] Extracted valid URL: {validUrl}");
                if (string.IsNullOrEmpty(validUrl))
                {
                    Console.WriteLine("[ERROR] No valid URL was extracted.");
                }
                Console.WriteLine($"[INFO] Extracted valid URL: {validUrl}");
                if (string.IsNullOrEmpty(validUrl))
                {
                    Console.WriteLine($"[ERROR] No valid URL extracted for chatId: {chatId}");
                    await botClient.SendTextMessageAsync(
                        chatId,
                        "لینک ارسال‌شده معتبر نیست. لطفاً لینک صحیح وارد کنید.",
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                // پردازش لینک عکس
                if (IsWaitingForPhotoLink(chatId))
                {
                    Console.WriteLine($"[INFO] Processing photo download for chatId: {chatId}");
                    await DownloadPinterestPhoto(botClient, chatId, validUrl, cancellationToken);
                    Console.WriteLine($"[SUCCESS] Photo download process completed for chatId: {chatId}");
                }
                // پردازش لینک ویدیو
                else if (IsWaitingForVideoLink(chatId))
                {
                    Console.WriteLine($"[INFO] Processing video download for chatId: {chatId}");
                    await DownloadPinterestVideo(botClient, chatId, validUrl, cancellationToken);
                    Console.WriteLine($"[SUCCESS] Video download process completed for chatId: {chatId}");
                }

                // حذف انتخاب کاربر از دیکشنری
                if (UserSelections.TryRemove(chatId, out var removedValue))
                {
                    Console.WriteLine($"[INFO] User selection removed for chatId: {chatId}, selection: {removedValue}");
                }
                else
                {
                    Console.WriteLine($"[WARNING] Failed to remove user selection for chatId: {chatId}");
                }
            }
            else
            {
                Console.WriteLine($"[INFO] No valid selection found for chatId: {chatId}");
                await botClient.SendTextMessageAsync(
                    chatId,
                    "لطفاً ابتدا از منوی پینترست، گزینه مورد نظر خود را انتخاب کنید.",
                    cancellationToken: cancellationToken
                );
            }
        }

        private static async Task<string> ResolveShortenedPinterestLink(string shortUrl)
        {
            try
            {
                Console.WriteLine($"[INFO] Resolving shortened Pinterest URL: {shortUrl}");

                // ایجاد درخواست HTTP
                Console.WriteLine("[INFO] Creating HTTP request for resolving shortened URL.");
                HttpRequestMessage request = new(HttpMethod.Get, shortUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                Console.WriteLine("[INFO] HTTP request created successfully.");

                // ارسال درخواست
                Console.WriteLine("[INFO] Sending HTTP request to resolve shortened URL...");
                var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Console.WriteLine($"[DEBUG] Response Status Code: {response.StatusCode}");
                Console.WriteLine($"[DEBUG] Response Headers: {string.Join("; ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                Console.WriteLine("[INFO] HTTP request sent successfully.");

                // بررسی وضعیت پاسخ
                Console.WriteLine($"[DEBUG] Response Status Code: {response.StatusCode}");
                Console.WriteLine($"[DEBUG] Response Headers: {string.Join("; ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");

                response.EnsureSuccessStatusCode();
                Console.WriteLine($"[DEBUG] Response Status Code: {response.StatusCode}");
                Console.WriteLine($"[DEBUG] Response Headers: {string.Join("; ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                Console.WriteLine("[SUCCESS] HTTP response indicates success.");

                // گرفتن لینک مقصد پس از ریدایرکت
                var resolvedUri = response.RequestMessage?.RequestUri?.ToString();
                if (string.IsNullOrEmpty(resolvedUri))
                {
                    Console.WriteLine("[ERROR] Resolved URI is null or empty. Input URL might be invalid.");
                    return null;
                }

                Console.WriteLine($"[SUCCESS] Final resolved URL: {resolvedUri}");
                return resolvedUri;
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"[ERROR] HTTP request error while resolving shortened URL: {httpEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An unexpected error occurred: {ex.Message}");
                return null;
            }
        }

        private static async Task DownloadPinterestPhoto(ITelegramBotClient botClient, long chatId, string photoUrl, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine("[INFO] Starting photo download process.");
                Console.WriteLine($"[INFO] Chat ID: {chatId}, Photo URL: {photoUrl}");

                // دانلود عکس با کیفیت اصلی
                Console.WriteLine("[INFO] Attempting to download photo...");
                string filePath = await DownloadPhotoFromUrl(photoUrl);
                Console.WriteLine($"[DEBUG] Downloaded photo file path: {filePath}");
                if (string.IsNullOrEmpty(filePath))
                {
                    Console.WriteLine("[ERROR] Failed to download photo.");
                }
                Console.WriteLine($"[SUCCESS] Photo downloaded successfully. File saved at: {filePath}");

                // ارسال عکس به‌صورت پیام تصویری
                Console.WriteLine("[INFO] Sending photo as a message...");
                await using var photoStreamForPhoto = File.OpenRead(filePath);
                await botClient.SendPhotoAsync(
                    chatId: chatId,
                    photo: new InputOnlineFile(photoStreamForPhoto),
                    caption: "📷 عکس با موفقیت دانلود شد و آماده است! \n🔙 برای برگشت به منوی اصلی روی /start کلیک کنید",
                    cancellationToken: cancellationToken
                );
                Console.WriteLine("[SUCCESS] Photo sent as a message successfully.");

                // ارسال عکس به‌صورت فایل
                Console.WriteLine("[INFO] Sending photo as a document...");
                await using var photoStreamForDocument = File.OpenRead(filePath); // باز کردن استریم جدید برای ارسال فایل
                await botClient.SendDocumentAsync(
                    chatId: chatId,
                    document: new InputOnlineFile(photoStreamForDocument, Path.GetFileName(filePath)),
                    caption: "📂 فایل عکس با کیفیت اصلی برای شما ارسال شد. \n🔙 برای برگشت به منوی اصلی روی /start کلیک کنید",
                    cancellationToken: cancellationToken
                );
                Console.WriteLine("[SUCCESS] Photo sent as a document successfully.");

                // حذف فایل از سرور
                Console.WriteLine("[INFO] Deleting temporary photo file...");
                File.Delete(filePath);
                Console.WriteLine("[SUCCESS] Temporary photo file deleted successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An error occurred during photo download or sending process: {ex.Message}");
                await botClient.SendTextMessageAsync(chatId, "خطایی در دانلود یا ارسال عکس رخ داد. لطفاً دوباره تلاش کنید.", cancellationToken: cancellationToken);
            }
        }
        
        private static async Task DownloadPinterestVideo(ITelegramBotClient botClient, long chatId, string videoUrl, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"[INFO] Starting download process for video. ChatId: {chatId}, Video URL: {videoUrl}");

                // بررسی لینک ویدیو
                if (string.IsNullOrEmpty(videoUrl))
                {
                    Console.WriteLine($"[ERROR] No valid video URL provided. ChatId: {chatId}");
                    await botClient.SendTextMessageAsync(
                        chatId,
                        "ویدیوی مد نظر شما قابل دانلود نیست یا لینک معتبر نیست.",
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                Console.WriteLine($"[INFO] Valid video URL provided. Initiating download. ChatId: {chatId}");

                string filePath = await DownloadVideoFromUrl(videoUrl);
                Console.WriteLine($"[DEBUG] Downloaded video file path: {filePath}");
                if (string.IsNullOrEmpty(filePath))
                {
                    Console.WriteLine("[ERROR] Failed to download video.");
                }
                Console.WriteLine($"[SUCCESS] Video downloaded successfully. File saved at: {filePath}");

                // ارسال ویدیو به‌صورت فایل
                Console.WriteLine($"[INFO] Preparing to send video file to user. ChatId: {chatId}, FilePath: {filePath}");
                await using var videoStream = File.OpenRead(filePath);
                await botClient.SendDocumentAsync(
                    chatId: chatId,
                    document: new InputOnlineFile(videoStream, Path.GetFileName(filePath)),
                    caption: "🎥 ویدیوی شما با موفقیت دانلود شد!",
                    cancellationToken: cancellationToken
                );
                Console.WriteLine($"[SUCCESS] Video sent successfully to ChatId: {chatId}");

                // حذف فایل موقت از سرور
                File.Delete(filePath);
                Console.WriteLine($"[INFO] Temporary video file deleted. FilePath: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception during video download or send process. ChatId: {chatId}, Error: {ex.Message}");
                await botClient.SendTextMessageAsync(
                    chatId,
                    "خطایی در دانلود یا ارسال ویدیو رخ داد. لطفاً دوباره تلاش کنید.",
                    cancellationToken: cancellationToken
                );
            }
        }

        private static string ExtractValidPinterestLink(string input)
        {
            try
            {
                Console.WriteLine("[INFO] Starting Pinterest link extraction process.");
                Console.WriteLine($"[INFO] Input provided: {input}");

                // تعریف الگوهای Regex
                string pattern1 = @"https://pin\.it/[A-Za-z0-9]+"; // لینک کوتاه
                string pattern2 = @"https://[\w\.]+/pin/\d+/"; // لینک کامل پینترست
                Console.WriteLine($"[INFO] Using patterns:\n - Pattern1: {pattern1}\n - Pattern2: {pattern2}");

                // بررسی الگوی لینک کوتاه
                Console.WriteLine($"[DEBUG] Input for matching: {input}");
                Console.WriteLine($"[DEBUG] Matching against pattern1: {pattern1}");

                if (Regex.IsMatch(input, pattern1))
                {
                    string shortUrl = Regex.Match(input, pattern1).Value;
                    Console.WriteLine($"[SUCCESS] Short Pinterest link matched: {shortUrl}");
                    return shortUrl;
                }
                else
                {
                    Console.WriteLine("[INFO] Input did not match pattern1.");
                }

                // بررسی الگوی لینک کامل
                Console.WriteLine($"[DEBUG] Matching against pattern2: {pattern2}");

                if (Regex.IsMatch(input, pattern2))
                {
                    string fullUrl = Regex.Match(input, pattern2).Value;
                    Console.WriteLine($"[SUCCESS] Full Pinterest link matched: {fullUrl}");
                    return fullUrl;
                }
                else
                {
                    Console.WriteLine("[INFO] Input did not match pattern2.");
                }

                // در صورت عدم تطابق با الگوها
                Console.WriteLine("[WARNING] No valid Pinterest link found in the input.");
                return null;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception occurred while extracting Pinterest link: {ex.Message}");
                throw;
            }
        }
        
        private static async Task<string> DownloadPhotoFromUrl(string url)
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await HttpClient.SendAsync(request);
                Console.WriteLine("Response received for video download request.");
                response.EnsureSuccessStatusCode();

                // دریافت محتوای HTML
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] HTML Response Body for photo download: {responseBody}");
                Console.WriteLine($"HTML content length: {responseBody.Length} characters.");
                Console.WriteLine($"HTML Response: {responseBody}");
                
                // استخراج لینک تصویر با کیفیت اصلی از HTML
                string imageUrl = ExtractHighQualityImageUrl(responseBody);
                Console.WriteLine($"[DEBUG] Extracted high-quality image URL: {imageUrl}");
                if (string.IsNullOrEmpty(imageUrl))
                {
                    Console.WriteLine("[ERROR] Failed to extract high-quality image URL.");
                }
                if (string.IsNullOrEmpty(imageUrl))
                {
                    throw new InvalidDataException("No valid high-quality image URL found in the response.");
                }

                // دانلود تصویر با کیفیت اصلی
                HttpRequestMessage imageRequest = new(HttpMethod.Get, imageUrl);
                imageRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var imageResponse = await HttpClient.SendAsync(imageRequest);
                imageResponse.EnsureSuccessStatusCode();

                var contentType = imageResponse.Content.Headers.ContentType?.MediaType;
                string fileExtension = contentType?.Split('/')[1] ?? "jpg";
                string fileName = Path.Combine(Path.GetTempPath(), $"pinterest_photo.{fileExtension}");

                await using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                await imageResponse.Content.CopyToAsync(fileStream);

                return fileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا در دانلود فایل: {ex.Message}");
                throw;
            }
        }

        private static string ExtractHighQualityImageUrl(string html)
        {
            try
            {
                Console.WriteLine("[INFO] Starting image URL extraction process.");

                // تعریف الگوی Regex برای استخراج لینک تصویر
                string pattern = @"<img.*?src=[""'](https://i\.pinimg\.com/[^""']+)[""']";
                Console.WriteLine($"[INFO] Using pattern: {pattern} to extract image URL.");

                // اجرای Regex برای پیدا کردن URL تصویر
                var match = Regex.Match(html, pattern);
                Console.WriteLine($"[DEBUG] Regex match for image URL: {match.Success}");
                if (!match.Success)
                {
                    Console.WriteLine($"[ERROR] No image URL matched the pattern: {pattern}");
                }

                if (!match.Success)
                {
                    Console.WriteLine("[ERROR] No image URL found in the provided HTML content.");
                    Console.WriteLine($"[DEBUG] HTML content: {html}");
                    return null;
                }

                // اگر URL پیدا شد
                string imageUrl = match.Groups[1].Value;
                Console.WriteLine($"[SUCCESS] Extracted image URL: {imageUrl}");

                // تغییر لینک به نسخه اصلی در صورت وجود
                if (imageUrl.Contains("/236x/") || imageUrl.Contains("/474x/"))
                {
                    Console.WriteLine("[INFO] Modifying extracted URL to high-quality version.");
                    imageUrl = imageUrl.Replace("/236x/", "/originals/").Replace("/474x/", "/originals/");
                    Console.WriteLine($"[SUCCESS] Modified high-quality image URL: {imageUrl}");
                }

                return imageUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception occurred while extracting image URL: {ex.Message}");
                throw;
            }
        }
        
        private static async Task<string> DownloadVideoFromUrl(string url)
        {
            try
            {
                Console.WriteLine($"[INFO] Starting download process for video URL: {url}");

                // ساخت درخواست HTTP برای دانلود HTML صفحه
                Console.WriteLine("[INFO] Creating HTTP request for video page.");
                HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                // ارسال درخواست و دریافت پاسخ
                Console.WriteLine("[INFO] Sending HTTP request to fetch video page HTML.");
                var response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                Console.WriteLine("[SUCCESS] Video page HTML fetched successfully.");

                // خواندن محتوای HTML
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] HTML Response Body for video download: {responseBody}");
                Console.WriteLine($"[INFO] HTML content length: {responseBody.Length} characters.");

                // استخراج لینک ویدیوی با کیفیت بالا
                Console.WriteLine("[INFO] Attempting to extract high-quality video URL from HTML.");
                string videoUrl = ExtractHighQualityVideoUrl(responseBody);
                Console.WriteLine($"[DEBUG] Extracted high-quality video URL: {videoUrl}");
                if (string.IsNullOrEmpty(videoUrl))
                {
                    Console.WriteLine("[ERROR] Failed to extract high-quality video URL.");
                }
                if (string.IsNullOrEmpty(videoUrl))
                {
                    Console.WriteLine("[ERROR] No valid video URL found in the extracted HTML.");
                    throw new InvalidDataException("No valid high-quality video URL found in the response.");
                }
                Console.WriteLine($"[SUCCESS] Extracted video URL: {videoUrl}");

                // ساخت درخواست HTTP برای دانلود ویدیو
                Console.WriteLine("[INFO] Creating HTTP request to download the video.");
                HttpRequestMessage videoRequest = new(HttpMethod.Get, videoUrl);
                videoRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                // ارسال درخواست و دریافت ویدیو
                Console.WriteLine("[INFO] Sending HTTP request to download the video.");
                var videoResponse = await HttpClient.SendAsync(videoRequest);
                videoResponse.EnsureSuccessStatusCode();
                Console.WriteLine("[SUCCESS] Video file downloaded successfully.");

                // تعیین نوع فایل و نام فایل
                var contentType = videoResponse.Content.Headers.ContentType?.MediaType;
                string fileExtension = contentType?.Split('/')[1] ?? "mp4";
                string fileName = Path.Combine(Path.GetTempPath(), $"pinterest_video.{fileExtension}");
                Console.WriteLine($"[INFO] Saving video file as: {fileName}");

                // ذخیره فایل ویدیو در سیستم
                await using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                await videoResponse.Content.CopyToAsync(fileStream);
                Console.WriteLine($"[SUCCESS] Video successfully downloaded and saved to: {fileName}");

                return fileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error during video download: {ex.Message}");
                throw;
            }
        }

        private static string ExtractHighQualityVideoUrl(string html)
        {
            try
            {
                Console.WriteLine("[INFO] Starting video URL extraction process.");

                // تعریف الگوی Regex
                string pattern = @"<video.*?src=[""'](https://v\.pinimg\.com/[^""']+)[""']";
                Console.WriteLine($"[INFO] Using pattern: {pattern} to extract video URL.");

                // اجرای Regex برای پیدا کردن URL
                var match = Regex.Match(html, pattern);
                Console.WriteLine($"[DEBUG] Regex match for video URL: {match.Success}");
                if (!match.Success)
                {
                    Console.WriteLine($"[ERROR] No video URL matched the pattern: {pattern}");
                }
                if (!match.Success)
                {
                    Console.WriteLine("[ERROR] No video URL found in the provided HTML content.");
                    Console.WriteLine($"[DEBUG] HTML content: {html}");
                    return null;
                }

                // اگر URL پیدا شد
                string videoUrl = match.Groups[1].Value;
                Console.WriteLine($"[SUCCESS] Extracted video URL: {videoUrl}");

                return videoUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception occurred while extracting video URL: {ex.Message}");
                throw;
            }
        }

    }
}