using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.Models;
using YoutubeDownloader.Services;

namespace YoutubeDownloader.Controllers;

public record VideoInfoRequest(string Url);

public record DownloadRequest(
    string Url,
    string Type = "muxed",
    string Quality = "360p",
    int MaxHeight = 720,
    bool AudioOnly = false);

[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private readonly IVideoService _videoService;
    private readonly LogService _logService;
    private readonly DownloadJobService _jobService;

    public VideoController(IVideoService videoService, LogService logService, DownloadJobService jobService)
    {
        _videoService = videoService;
        _logService = logService;
        _jobService = jobService;
    }

    [HttpPost("info")]
    public async Task<IActionResult> GetInfo([FromBody] VideoInfoRequest request)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var info = await _videoService.GetVideoInfoAsync(request.Url);
            sw.Stop();
            await _logService.LogRequestAsync(HttpContext, "info",
                videoUrl: request.Url,
                videoTitle: info.Title,
                success: true,
                durationMs: sw.ElapsedMilliseconds);
            return Ok(info);
        }
        catch (Exception ex)
        {
            sw.Stop();
            await _logService.LogRequestAsync(HttpContext, "info",
                videoUrl: request.Url,
                success: false,
                error: ex.Message,
                durationMs: sw.ElapsedMilliseconds);
            return BadRequest(new { error = ex.Message });
        }
    }

    // Kicks off an async download job so the request returns immediately and never
    // trips Cloudflare's 100s origin timeout. The heavy yt-dlp/ffmpeg work happens
    // in the background; the client polls status and then fetches the ready file.
    [HttpPost("download")]
    public IActionResult StartDownload([FromBody] DownloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "url is required" });

        var ip = HttpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
                 ?? HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        var job = _jobService.Enqueue(new DownloadJob
        {
            Url = request.Url.Trim(),
            Type = request.Type,
            Quality = request.Quality,
            MaxHeight = request.MaxHeight,
            AudioOnly = request.AudioOnly,
            Ip = ip,
            Country = HttpContext.Request.Headers["CF-IPCountry"].FirstOrDefault(),
            UserAgent = HttpContext.Request.Headers.UserAgent.FirstOrDefault(),
            Endpoint = HttpContext.Request.Path
        });

        return Accepted(new { jobId = job.Id });
    }

    [HttpGet("download/{jobId}")]
    public IActionResult GetDownloadStatus(string jobId)
    {
        var job = _jobService.Get(jobId);
        if (job is null)
            return NotFound(new { error = "job not found" });

        return Ok(new
        {
            status = job.Status.ToString().ToLowerInvariant(),
            progress = job.Progress,
            fileName = job.FileName,
            error = job.Error
        });
    }

    [HttpGet("download/{jobId}/file")]
    public IActionResult GetDownloadFile(string jobId)
    {
        var job = _jobService.Get(jobId);
        if (job is null)
            return NotFound(new { error = "job not found" });
        if (job.Status != DownloadJobStatus.Ready || job.FilePath is null || !System.IO.File.Exists(job.FilePath))
            return Conflict(new { error = "file not ready", status = job.Status.ToString().ToLowerInvariant() });

        // File already sits on disk, so time-to-first-byte is immediate and the
        // (potentially long) transfer streams without hitting the origin timeout.
        return PhysicalFile(job.FilePath, job.ContentType ?? "application/octet-stream",
            job.FileName ?? "download", enableRangeProcessing: true);
    }
}