using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YoutubeDownloader.Data;

namespace YoutubeDownloader.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogController : ControllerBase
{
    private readonly AppDbContext _db;

    public LogController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        var total = await _db.RequestLogs.CountAsync();
        var logs = await _db.RequestLogs
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new { total, page, size, logs });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalRequests = await _db.RequestLogs.CountAsync();
        var totalDownloads = await _db.RequestLogs.CountAsync(l => l.Action == "download");
        var uniqueIps = await _db.RequestLogs.Select(l => l.IpAddress).Distinct().CountAsync();
        var todayRequests = await _db.RequestLogs.CountAsync(l => l.Timestamp.Date == DateTime.UtcNow.Date);

        var topVideos = await _db.RequestLogs
            .Where(l => l.VideoTitle != null)
            .GroupBy(l => l.VideoTitle)
            .Select(g => new { Title = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        return Ok(new { totalRequests, totalDownloads, uniqueIps, todayRequests, topVideos });
    }
}