using Microsoft.EntityFrameworkCore;
using YoutubeDownloader.Data;
using YoutubeDownloader.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<DownloadJobService>();
builder.Services.AddHostedService<DownloadJobWorker>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql("Host=localhost;Port=5432;Database=ytdownloader;Username=aybar;Password=aybar1234"));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();

app.Run();