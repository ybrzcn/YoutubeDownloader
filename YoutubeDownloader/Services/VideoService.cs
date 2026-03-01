using Microsoft.Extensions.Caching.Memory;
using YoutubeDownloader.Models;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Services;

public class VideoService : IVideoService
{
    private readonly YoutubeClient _youtube;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public VideoService(YoutubeClient youtube, IMemoryCache cache)
    {
        _youtube = youtube;
        _cache = cache;
    }

    public async Task<VideoInfoResponse> GetVideoInfoAsync(string url)
    {
        var key = $"info:{url.Trim()}";
        if (_cache.TryGetValue(key, out VideoInfoResponse? cached))
            return cached!;

        // İki bağımsız istek paralel çalışır (ValueTask → Task dönüşümü gerekli)
        var videoTask = _youtube.Videos.GetAsync(url).AsTask();
        var manifestTask = _youtube.Videos.Streams.GetManifestAsync(url).AsTask();
        await Task.WhenAll(videoTask, manifestTask);

        var video = videoTask.Result;
        var manifest = manifestTask.Result;

        // Download sırasında tekrar istek atmamak için başlığı da önbellekle
        _cache.Set($"title:{url.Trim()}", video.Title, CacheDuration);

        var streams = new List<StreamOption>();

        var muxedStreams = manifest.GetMuxedStreams()
            .OrderByDescending(s => s.VideoQuality.MaxHeight)
            .ToList();
        var muxedHeights = new HashSet<int>(muxedStreams.Select(s => s.VideoQuality.MaxHeight));

        var adaptiveStreams = manifest.GetVideoOnlyStreams()
            .Where(s => s.Container.Name == "mp4" && !muxedHeights.Contains(s.VideoQuality.MaxHeight))
            .GroupBy(s => s.VideoQuality.Label)
            .Select(g => g.OrderByDescending(s => s.Bitrate).First())
            .OrderByDescending(s => s.VideoQuality.MaxHeight)
            .ToList();

        streams.AddRange(adaptiveStreams.Select(s => new StreamOption
        {
            Quality = s.VideoQuality.Label,
            Container = "mp4",
            Size = $"{s.Size.MegaBytes:F1} MB",
            Type = "adaptive",
            MaxHeight = s.VideoQuality.MaxHeight
        }));

        streams.AddRange(muxedStreams.Select(s => new StreamOption
        {
            Quality = s.VideoQuality.Label,
            Container = s.Container.Name,
            Size = $"{s.Size.MegaBytes:F1} MB",
            Type = "muxed",
            MaxHeight = s.VideoQuality.MaxHeight
        }));

        var audioStream = manifest.GetAudioOnlyStreams()
            .Where(s => s.Container.Name == "mp4")
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault()
            ?? manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        if (audioStream != null)
        {
            streams.Add(new StreamOption
            {
                Quality = "Sadece Ses",
                Container = audioStream.Container.Name,
                Size = $"{audioStream.Size.MegaBytes:F1} MB",
                Type = "audio-only"
            });
        }

        var result = new VideoInfoResponse
        {
            Title = video.Title,
            Author = video.Author.ChannelTitle,
            ThumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url,
            DurationSeconds = (int?)video.Duration?.TotalSeconds,
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

        var video = await _youtube.Videos.GetAsync(url);
        _cache.Set(key, video.Title, CacheDuration);
        return video.Title;
    }

    public async Task DownloadMuxedAsync(string url, string quality, Stream outputStream)
    {
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(url);
        var streamInfo = manifest.GetMuxedStreams()
            .Where(s => s.VideoQuality.Label == quality)
            .GetWithHighestBitrate();

        await _youtube.Videos.Streams.CopyToAsync(streamInfo, outputStream);
    }

    public async Task DownloadAdaptiveAsync(string url, int maxHeight, string outputPath)
    {
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(url);

        IStreamInfo videoStream = manifest.GetVideoOnlyStreams()
            .Where(s => s.Container.Name == "mp4" && s.VideoQuality.MaxHeight == maxHeight)
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault()
            ?? manifest.GetVideoOnlyStreams()
                .Where(s => s.Container.Name == "mp4")
                .OrderByDescending(s => s.VideoQuality.MaxHeight)
                .First();

        IStreamInfo audioStream = manifest.GetAudioOnlyStreams()
            .Where(s => s.Container.Name == "mp4")
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault()
            ?? manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        var ffmpegPath = File.Exists("/opt/homebrew/bin/ffmpeg")
            ? "/opt/homebrew/bin/ffmpeg"
            : "ffmpeg";

        var request = new ConversionRequestBuilder(outputPath)
            .SetContainer("mp4")
            .SetFFmpegPath(ffmpegPath)
            .Build();

        await _youtube.Videos.DownloadAsync(
            new List<IStreamInfo> { videoStream, audioStream },
            request);
    }

    public async Task DownloadAudioAsync(string url, Stream outputStream)
    {
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(url);
        var streamInfo = manifest.GetAudioOnlyStreams()
            .Where(s => s.Container.Name == "mp4")
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault()
            ?? manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        await _youtube.Videos.Streams.CopyToAsync(streamInfo, outputStream);
    }
}
