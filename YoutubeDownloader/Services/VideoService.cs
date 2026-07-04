using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using YoutubeDownloader.Models;

namespace YoutubeDownloader.Services;

public class VideoService : IVideoService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public VideoService(IMemoryCache cache)
    {
        _cache = cache;
    }

    private async Task<string> RunYtDlp(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"yt-dlp error: {error}");
        }

        return output.Trim();
    }

    public async Task<VideoInfoResponse> GetVideoInfoAsync(string url)
    {
        var key = $"info:{url.Trim()}";
        if (_cache.TryGetValue(key, out VideoInfoResponse? cached))
            return cached!;

        var json = await RunYtDlp($"--dump-json --no-download \"{url}\"");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var title = root.GetProperty("title").GetString() ?? "";
        var author = root.TryGetProperty("uploader", out var up) ? up.GetString() ?? "" : "";
        var thumbnail = root.TryGetProperty("thumbnail", out var th) ? th.GetString() ?? "" : "";
        var duration = root.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.Number
            ? (int?)dur.GetDouble() : null;

        var streams = new List<StreamOption>();
        var formats = root.GetProperty("formats");
        var seenQualities = new HashSet<string>();

        foreach (var fmt in formats.EnumerateArray())
        {
            var ext = fmt.TryGetProperty("ext", out var e) ? e.GetString() : "";
            if (ext != "mp4" && ext != "m4a" && ext != "webm") continue;

            var height = fmt.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : 0;
            var acodec = fmt.TryGetProperty("acodec", out var ac) ? ac.GetString() ?? "none" : "none";
            var vcodec = fmt.TryGetProperty("vcodec", out var vc) ? vc.GetString() ?? "none" : "none";
            var filesize = fmt.TryGetProperty("filesize", out var fs) && fs.ValueKind == JsonValueKind.Number ? fs.GetInt64() : 0;
            if (filesize == 0 && fmt.TryGetProperty("filesize_approx", out var fsa) && fsa.ValueKind == JsonValueKind.Number)
                filesize = fsa.GetInt64();

            var sizeMb = filesize > 0 ? $"{filesize / 1024.0 / 1024.0:F1} MB" : "? MB";

            if (vcodec != "none" && acodec != "none" && height > 0)
            {
                var quality = $"{height}p";
                if (!seenQualities.Contains($"muxed-{quality}"))
                {
                    seenQualities.Add($"muxed-{quality}");
                    streams.Add(new StreamOption { Quality = quality, Container = "mp4", Size = sizeMb, Type = "muxed", MaxHeight = height });
                }
            }
            else if (vcodec != "none" && height > 0 && ext == "mp4")
            {
                var quality = $"{height}p";
                if (!seenQualities.Contains($"adaptive-{quality}"))
                {
                    seenQualities.Add($"adaptive-{quality}");
                    streams.Add(new StreamOption { Quality = quality, Container = "mp4", Size = sizeMb, Type = "adaptive", MaxHeight = height });
                }
            }
            else if (acodec != "none" && (vcodec == "none" || vcodec == null) && (ext == "m4a" || ext == "mp4"))
            {
                if (!seenQualities.Contains("audio"))
                {
                    seenQualities.Add("audio");
                    streams.Add(new StreamOption { Quality = "Sadece Ses", Container = "mp4", Size = sizeMb, Type = "audio-only", MaxHeight = 0 });
                }
            }
        }

        streams = streams.OrderByDescending(s => s.MaxHeight).ToList();

        var result = new VideoInfoResponse
        {
            Title = title,
            Author = author,
            ThumbnailUrl = thumbnail,
            DurationSeconds = duration,
            AvailableStreams = streams
        };

        _cache.Set(key, result, CacheDuration);
        return result;
    }

    public async Task<string> GetVideoTitleAsync(string url)
    {
        var key = $"title:{url.Trim()}";
        if (_cache.TryGetValue(key, out string? cached))
            return cached!;

        var title = await RunYtDlp($"--get-title \"{url}\"");
        _cache.Set(key, title, CacheDuration);
        return title;
    }

    public async Task<string> GetDirectUrlAsync(string url, string type, int maxHeight, bool audioOnly)
    {
        string format;
        if (audioOnly)
            format = "bestaudio[ext=m4a]/bestaudio";
        else if (type == "adaptive")
            format = $"bestvideo[height<={maxHeight}][ext=mp4]+bestaudio[ext=m4a]/best[height<={maxHeight}]";
        else
            format = $"best[height<={maxHeight}][ext=mp4]/best[ext=mp4]";

        var result = await RunYtDlp($"-f \"{format}\" --get-url \"{url}\"");
        return result.Split('\n')[0];
    }

    public async Task DownloadMuxedAsync(string url, string quality, Stream outputStream)
    {
        var tempFile = Path.GetTempFileName() + ".mp4";
        try
        {
            var height = quality.Replace("p", "");
            await RunYtDlp($"-f \"best[height<={height}][ext=mp4]/best[ext=mp4]\" -o \"{tempFile}\" \"{url}\"");
            await using var fs = File.OpenRead(tempFile);
            await fs.CopyToAsync(outputStream);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    public async Task DownloadAdaptiveAsync(string url, int maxHeight, string outputPath)
    {
        await RunYtDlp($"-f \"bestvideo[height<={maxHeight}][ext=mp4]+bestaudio[ext=m4a]/best[height<={maxHeight}]\" --merge-output-format mp4 -o \"{outputPath}\" \"{url}\"");
    }

    public async Task DownloadAudioAsync(string url, Stream outputStream)
    {
        var tempFile = Path.GetTempFileName() + ".m4a";
        try
        {
            await RunYtDlp($"-f \"bestaudio[ext=m4a]/bestaudio\" -o \"{tempFile}\" \"{url}\"");
            await using var fs = File.OpenRead(tempFile);
            await fs.CopyToAsync(outputStream);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}