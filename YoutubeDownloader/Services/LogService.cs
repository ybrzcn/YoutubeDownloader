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

    public async Task LogRequestAsync(HttpContext context, string action, string? videoUrl = null,
        string? videoTitle = null, string? quality = null, bool success = true,
        string? error = null, double? durationMs = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ip = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
                 ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? context.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        var country = context.Request.Headers["CF-IPCountry"].FirstOrDefault();

        var log = new RequestLog
        {
            Timestamp = DateTime.UtcNow,
            IpAddress = ip,
            Endpoint = context.Request.Path,
            VideoUrl = videoUrl,
            VideoTitle = videoTitle,
            Quality = quality,
            Action = action,
            Success = success,
            Error = error,
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault(),
            Country = country,
            DurationMs = durationMs
        };

        db.RequestLogs.Add(log);
        await db.SaveChangesAsync();
    }
}