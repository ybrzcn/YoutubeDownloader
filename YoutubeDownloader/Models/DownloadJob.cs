namespace YoutubeDownloader.Models;

public enum DownloadJobStatus
{
    Pending,
    Processing,
    Ready,
    Error
}

public class DownloadJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required string Url { get; init; }
    public required string Type { get; init; }   // muxed | adaptive | audio
    public string Quality { get; init; } = "360p";
    public int MaxHeight { get; init; } = 720;
    public bool AudioOnly { get; init; }

    // Mutable runtime state (touched from the background worker + request threads)
    public volatile DownloadJobStatus Status = DownloadJobStatus.Pending;
    public int Progress;            // 0-100, best effort
    public string? FilePath;
    public string? FileName;
    public string? ContentType;
    public string? Error;

    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime? CompletedAt;

    // Captured at request time so the worker can log without an HttpContext
    public string Ip { get; init; } = "unknown";
    public string? Country { get; init; }
    public string? UserAgent { get; init; }
    public string Endpoint { get; init; } = "/api/video/download";
}
