using Microsoft.EntityFrameworkCore;
using YoutubeDownloader.Models;

namespace YoutubeDownloader.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();
}