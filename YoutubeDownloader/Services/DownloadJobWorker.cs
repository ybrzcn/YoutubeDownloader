using System.Diagnostics;
using YoutubeDownloader.Models;

namespace YoutubeDownloader.Services;

public class DownloadJobWorker : BackgroundService
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromMinutes(30);

    private readonly DownloadJobService _jobs;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LogService _logService;
    private readonly ILogger<DownloadJobWorker> _logger;

    public DownloadJobWorker(
        DownloadJobService jobs,
        IServiceScopeFactory scopeFactory,
        LogService logService,
        ILogger<DownloadJobWorker> logger)
    {
        _jobs = jobs;
        _scopeFactory = scopeFactory;
        _logService = logService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanup = CleanupLoopAsync(stoppingToken);

        await foreach (var job in _jobs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                job.Status = DownloadJobStatus.Error;
                job.Error = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "Download job {JobId} failed", job.Id);
            }
        }

        await cleanup;
    }

    private async Task ProcessAsync(DownloadJob job, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        job.Status = DownloadJobStatus.Processing;
        _logger.LogInformation(
            "Job {JobId} started: url={Url} type={Type} quality={Quality} maxHeight={MaxHeight} audioOnly={AudioOnly}",
            job.Id, job.Url, job.Type, job.Quality, job.MaxHeight, job.AudioOnly);

        using var scope = _scopeFactory.CreateScope();
        var videoService = scope.ServiceProvider.GetRequiredService<IVideoService>();

        var title = await videoService.GetVideoTitleAsync(job.Url);
        _logger.LogInformation("Job {JobId} title resolved ({Elapsed}ms): {Title}",
            job.Id, sw.ElapsedMilliseconds, title);
        var safeTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        var lastLogged = -10;
        var progress = new Progress<double>(pct =>
        {
            job.Progress = (int)Math.Clamp(pct, 0, 100);
            if (job.Progress >= lastLogged + 10)
            {
                lastLogged = job.Progress;
                _logger.LogInformation("Job {JobId} progress: {Progress}% ({Elapsed}ms)",
                    job.Id, job.Progress, sw.ElapsedMilliseconds);
            }
        });
        _logger.LogInformation("Job {JobId} downloading...", job.Id);

        string tempPath;
        string quality;

        if (job.AudioOnly)
        {
            tempPath = Path.Combine(Path.GetTempPath(), $"{job.Id}.m4a");
            await using (var fs = File.Create(tempPath))
                await videoService.DownloadAudioAsync(job.Url, fs, progress);
            job.FileName = $"{safeTitle}.m4a";
            job.ContentType = "audio/mp4";
            quality = "audio";
        }
        else if (job.Type == "adaptive")
        {
            tempPath = Path.Combine(Path.GetTempPath(), $"{job.Id}.mp4");
            await videoService.DownloadAdaptiveAsync(job.Url, job.MaxHeight, tempPath, progress);
            job.FileName = $"{safeTitle}.mp4";
            job.ContentType = "video/mp4";
            quality = $"{job.MaxHeight}p";
        }
        else
        {
            tempPath = Path.Combine(Path.GetTempPath(), $"{job.Id}.mp4");
            await using (var fs = File.Create(tempPath))
                await videoService.DownloadMuxedAsync(job.Url, job.Quality, fs, progress);
            job.FileName = $"{safeTitle}.mp4";
            job.ContentType = "video/mp4";
            quality = job.Quality;
        }

        job.FilePath = tempPath;
        job.Progress = 100;
        job.Status = DownloadJobStatus.Ready;
        job.CompletedAt = DateTime.UtcNow;
        sw.Stop();

        var sizeMb = new FileInfo(tempPath).Length / 1024.0 / 1024.0;
        _logger.LogInformation("Job {JobId} ready in {Elapsed}ms: {File} ({Size:F1} MB)",
            job.Id, sw.ElapsedMilliseconds, job.FileName, sizeMb);

        await _logService.LogRequestAsync("download", job.Endpoint, job.Ip, job.Country, job.UserAgent,
            videoUrl: job.Url, videoTitle: title, quality: quality,
            success: true, durationMs: sw.ElapsedMilliseconds);
    }

    private async Task CleanupLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var cutoff = DateTime.UtcNow - JobTtl;
                foreach (var job in _jobs.All)
                {
                    var since = job.CompletedAt ?? job.CreatedAt;
                    if (since < cutoff)
                        _jobs.Remove(job.Id);
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}
