using System.Diagnostics.CodeAnalysis;

namespace VidaroApi.Domain.Entities;

public class Channel : BaseAuditableEntity
{
    // ReSharper disable once UnusedMember.Local
    [ExcludeFromCodeCoverage]
    private Channel() { }

    public Channel(Guid userId, string name, string? description, DateTimeOffset now)
        : base(now)
    {
        UserId = userId;
        Name = name;
        Description = description;
        FollowerCount = 0;
    }

    public Guid UserId { get; init; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int FollowerCount { get; private set; }

    public void UpdateDetails(string name, string? description, DateTimeOffset now)
    {
        Name = name;
        Description = description;
        SetUpdatedAt(now);
    }

    public void IncrementFollowerCount() => FollowerCount++;
    public void DecrementFollowerCount()
    {
        if (FollowerCount == 0)
            throw new InvalidOperationException("Cannot decrement follower count below zero.");
        FollowerCount--;
    }

    // Navigation properties
    public User User { get; init; } = null!;

    // private List<Video> _videos = [];
    // public IReadOnlyList<Video> Videos => _videos.AsReadOnly();
}
