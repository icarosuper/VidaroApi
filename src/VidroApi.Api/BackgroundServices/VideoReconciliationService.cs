using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Enums;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Api.BackgroundServices;

public class VideoReconciliationService(
    IServiceScopeFactory scopeFactory,
    IOptions<VideoSettings> videoOptions,
    IOptions<ApiSettings> apiOptions,
    ILogger<VideoReconciliationService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(videoOptions.Value.ReconciliationIntervalMinutes);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ReconcileStaleUploadsAsync(stoppingToken);
        }
    }

    private async Task ReconcileStaleUploadsAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting video upload reconciliation");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var minio = scope.ServiceProvider.GetRequiredService<IMinioService>();
            var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueueService>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            var now = clock.UtcNow;
            var staleVideos = await db.Videos
                .Where(v => v.Status == VideoStatus.PendingUpload && v.UploadExpiresAt < now)
                .ToListAsync(ct);

            logger.LogInformation("Found {Count} stale pending-upload videos", staleVideos.Count);

            foreach (var video in staleVideos)
            {
                await ReconcileVideoAsync(video, minio, jobQueue, clock, ct);
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error during video upload reconciliation");
        }
    }

    private async Task ReconcileVideoAsync(
        Domain.Entities.Video video,
        IMinioService minio,
        IJobQueueService jobQueue,
        IDateTimeProvider clock,
        CancellationToken ct)
    {
        try
        {
            var rawObjectKey = $"raw/{video.Id}";
            var fileExistsInMinio = await minio.ObjectExistsAsync(rawObjectKey, ct);

            if (fileExistsInMinio)
            {
                logger.LogWarning(
                    "Video {VideoId} upload completed but webhook was missed — triggering processing",
                    video.Id);

                video.MarkAsProcessing(clock.UtcNow);

                var callbackUrl = $"{apiOptions.Value.BaseUrl}/webhooks/video-processed";
                await jobQueue.PublishJobAsync(video.Id.ToString(), callbackUrl, ct);
            }
            else
            {
                logger.LogWarning(
                    "Video {VideoId} upload expired without file — marking as failed",
                    video.Id);

                video.MarkAsFailed(clock.UtcNow);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reconcile video {VideoId}", video.Id);
        }
    }
}
