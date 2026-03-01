namespace YoutubeDownloader.Models;

public class VideoInfoResponse
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public List<StreamOption> AvailableStreams { get; set; } = new();
}

public class StreamOption
{
    public string Quality { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;  // "muxed", "adaptive", "audio-only"
    public int MaxHeight { get; set; }
}