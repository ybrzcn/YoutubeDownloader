using YoutubeDownloader.Data;
using YoutubeDownloader.Models;

namespace YoutubeDownloader.Services;

public class LogService
{
    private readonly IServiceProvider _serviceProvider;

    public LogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task LogRequestAsync(HttpContext context, string action, string? videoUrl = null,
        string? videoTitle = null, string? quality = null, bool success = true,
        string? error = null, double? durationMs = null)
    {
        var ip = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
                 ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? context.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        var country = context.Request.Headers["CF-IPCountry"].FirstOrDefault();

        return LogRequestAsync(action, context.Request.Path, ip, country,
            context.Request.Headers.UserAgent.FirstOrDefault(),
            videoUrl, videoTitle, quality, success, error, durationMs);
    }

    // Context-free overload for background work (no HttpContext available).
    public async Task LogRequestAsync(string action, string endpoint, string ip, string? country,
        string? userAgent, string? videoUrl = null, string? videoTitle = null, string? quality = null,
        bool success = true, string? error = null, double? durationMs = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var log = new RequestLog
        {
            Timestamp = DateTime.UtcNow,
            IpAddress = ip,
            Endpoint = endpoint,
            VideoUrl = videoUrl,
            VideoTitle = videoTitle,
            Quality = quality,
            Action = action,
            Success = success,
            Error = error,
            UserAgent = userAgent,
            Country = country,
            DurationMs = durationMs
        };

        db.RequestLogs.Add(log);
        await db.SaveChangesAsync();
    }
}