using YoutubeDownloader.Models;

namespace YoutubeDownloader.Services;

public interface IVideoService
{
    Task<VideoInfoResponse> GetVideoInfoAsync(string url);
    Task<string> GetVideoTitleAsync(string url);
    Task DownloadMuxedAsync(string url, string quality, Stream outputStream);
    Task DownloadAdaptiveAsync(string url, int maxHeight, string outputPath);
    Task DownloadAudioAsync(string url, Stream outputStream);
}