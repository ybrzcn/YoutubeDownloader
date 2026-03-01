using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.Services;

namespace YoutubeDownloader.Controllers;

public record VideoInfoRequest(string Url);

[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private readonly IVideoService _videoService;

    public VideoController(IVideoService videoService)
        => _videoService = videoService;

    [HttpPost("info")]
    public async Task<IActionResult> GetInfo([FromBody] VideoInfoRequest request)
    {
        try
        {
            var info = await _videoService.GetVideoInfoAsync(request.Url);
            return Ok(info);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download( [FromQuery] string url, [FromQuery] string type = "muxed", [FromQuery] string quality = "360p", [FromQuery] int maxHeight = 720, [FromQuery] bool audioOnly = false)
    {
        try
        {
            var title = await _videoService.GetVideoTitleAsync(url);
            var safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));

            if (audioOnly)
            {
                var ms = new MemoryStream();
                await _videoService.DownloadAudioAsync(url, ms);
                ms.Position = 0;
                return File(ms, "audio/mp4", $"{safeTitle}.m4a");
            }

            if (type == "adaptive")
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
                try
                {
                    await _videoService.DownloadAdaptiveAsync(url, maxHeight, tempPath);
                    var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
                    return File(bytes, "video/mp4", $"{safeTitle}.mp4");
                }
                finally
                {
                    if (System.IO.File.Exists(tempPath))
                        System.IO.File.Delete(tempPath);
                }
            }

            // Muxed
            var memStream = new MemoryStream();
            await _videoService.DownloadMuxedAsync(url, quality, memStream);
            memStream.Position = 0;
            return File(memStream, "video/mp4", $"{safeTitle}.mp4");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
