using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public class VideoMetadata : BaseEntity
{
    // ReSharper disable once UnusedMember.Local
    [ExcludeFromCodeCoverage]
    private VideoMetadata() { }

    public VideoMetadata(Guid videoId, long fileSizeBytes, double durationSeconds,
        int width, int height, string codec, DateTimeOffset now)
        : base(now)
    {
        VideoId = videoId;
        FileSizeBytes = fileSizeBytes;
        DurationSeconds = durationSeconds;
        Width = width;
        Height = height;
        Codec = codec;
    }

    public Guid VideoId { get; init; }
    public long FileSizeBytes { get; init; }
    public double DurationSeconds { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string Codec { get; init; } = null!;

    // Navigation property
    public Video Video { get; init; } = null!;
}
