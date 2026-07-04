using System.Collections.Concurrent;
using System.Threading.Channels;
using YoutubeDownloader.Models;

namespace YoutubeDownloader.Services;

public class DownloadJobService
{
    private readonly ConcurrentDictionary<string, DownloadJob> _jobs = new();
    private readonly Channel<DownloadJob> _queue = Channel.CreateUnbounded<DownloadJob>();

    public ChannelReader<DownloadJob> Reader => _queue.Reader;

    public DownloadJob Enqueue(DownloadJob job)
    {
        _jobs[job.Id] = job;
        _queue.Writer.TryWrite(job);
        return job;
    }

    public DownloadJob? Get(string id) =>
        _jobs.TryGetValue(id, out var job) ? job : null;

    public IEnumerable<DownloadJob> All => _jobs.Values;

    public void Remove(string id)
    {
        if (_jobs.TryRemove(id, out var job))
            TryDeleteFile(job);
    }

    public static void TryDeleteFile(DownloadJob job)
    {
        if (job.FilePath is not null && File.Exists(job.FilePath))
        {
            try { File.Delete(job.FilePath); } catch { /* best effort */ }
        }
    }
}
