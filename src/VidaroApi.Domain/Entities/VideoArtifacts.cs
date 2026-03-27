using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public class VideoArtifacts : BaseEntity
{
    // ReSharper disable once UnusedMember.Local
    [ExcludeFromCodeCoverage]
    private VideoArtifacts() { }

    public VideoArtifacts(Guid videoId, string processedPath, string previewPath,
        string hlsPath, string audioPath, List<string> thumbnailPaths, DateTimeOffset now)
        : base(now)
    {
        VideoId = videoId;
        ProcessedPath = processedPath;
        PreviewPath = previewPath;
        HlsPath = hlsPath;
        AudioPath = audioPath;
        ThumbnailPaths = thumbnailPaths;
    }

    public Guid VideoId { get; init; }
    public string ProcessedPath { get; init; } = null!;
    public string PreviewPath { get; init; } = null!;
    public string HlsPath { get; init; } = null!;
    public string AudioPath { get; init; } = null!;
    public List<string> ThumbnailPaths { get; init; } = null!;

    // Navigation property
    public Video Video { get; init; } = null!;
}
