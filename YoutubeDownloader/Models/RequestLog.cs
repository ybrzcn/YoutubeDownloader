namespace YoutubeDownloader.Models;

public class RequestLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string? VideoUrl { get; set; }
    public string? VideoTitle { get; set; }
    public string? Quality { get; set; }
    public string? Action { get; set; } 
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? UserAgent { get; set; }
    public string? Country { get; set; }
    public double? DurationMs { get; set; }
}