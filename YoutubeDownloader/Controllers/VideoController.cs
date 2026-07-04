using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.Services;

namespace YoutubeDownloader.Controllers;

public record VideoInfoRequest(string Url);

[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private readonly IVideoService _videoService;
    private readonly LogService _logService;

    public VideoController(IVideoService videoService, LogService logService)
    {
        _videoService = videoService;
        _logService = logService;
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

    [HttpGet("direct-url")]
    public async Task<IActionResult> GetDirectUrl(
        [FromQuery] string url,
        [FromQuery] string type = "adaptive",
        [FromQuery] int maxHeight = 720,
        [FromQuery] bool audioOnly = false)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var directUrl = await ((VideoService)_videoService).GetDirectUrlAsync(url, type, maxHeight, audioOnly);
            var title = await _videoService.GetVideoTitleAsync(url);
            sw.Stop();
            await _logService.LogRequestAsync(HttpContext, "download",
                videoUrl: url, videoTitle: title,
                quality: audioOnly ? "audio" : $"{maxHeight}p",
                success: true, durationMs: sw.ElapsedMilliseconds);
            return Ok(new { url = directUrl, title });
        }
        catch (Exception ex)
        {
            sw.Stop();
            await _logService.LogRequestAsync(HttpContext, "download",
                videoUrl: url, success: false, error: ex.Message,
                durationMs: sw.ElapsedMilliseconds);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download(
        [FromQuery] string url,
        [FromQuery] string type = "muxed",
        [FromQuery] string quality = "360p",
        [FromQuery] int maxHeight = 720,
        [FromQuery] bool audioOnly = false)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var title = await _videoService.GetVideoTitleAsync(url);
            var safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
            var selectedQuality = audioOnly ? "audio" : type == "adaptive" ? $"{maxHeight}p" : quality;

            if (audioOnly)
            {
                var ms = new MemoryStream();
                await _videoService.DownloadAudioAsync(url, ms);
                ms.Position = 0;
                sw.Stop();
                await _logService.LogRequestAsync(HttpContext, "download",
                    videoUrl: url, videoTitle: title, quality: "audio",
                    success: true, durationMs: sw.ElapsedMilliseconds);
                return File(ms, "audio/mp4", $"{safeTitle}.m4a");
            }

            if (type == "adaptive")
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
                try
                {
                    await _videoService.DownloadAdaptiveAsync(url, maxHeight, tempPath);
                    var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                    sw.Stop();
                    await _logService.LogRequestAsync(HttpContext, "download",
                        videoUrl: url, videoTitle: title, quality: selectedQuality,
                        success: true, durationMs: sw.ElapsedMilliseconds);
                    return File(bytes, "video/mp4", $"{safeTitle}.mp4");
                }
                finally
                {
                    if (System.IO.File.Exists(tempPath))
                        System.IO.File.Delete(tempPath);
                }
            }

            var memStream = new MemoryStream();
            await _videoService.DownloadMuxedAsync(url, quality, memStream);
            memStream.Position = 0;
            sw.Stop();
            await _logService.LogRequestAsync(HttpContext, "download",
                videoUrl: url, videoTitle: title, quality: selectedQuality,
                success: true, durationMs: sw.ElapsedMilliseconds);
            return File(memStream, "video/mp4", $"{safeTitle}.mp4");
        }
        catch (Exception ex)
        {
            sw.Stop();
            await _logService.LogRequestAsync(HttpContext, "download",
                videoUrl: url, success: false, error: ex.Message,
                durationMs: sw.ElapsedMilliseconds);
            return BadRequest(new { error = ex.Message });
        }
    }
}