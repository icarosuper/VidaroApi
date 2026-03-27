# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Working style

After each implementation step:
1. **Suggest a commit message in Portuguese** — the user reviews and commits manually. Never commit without being asked.
2. **Show the next possible steps** — brief list so the user can choose what to implement next.

## Commands

```bash
# Build
dotnet build

# Run API (development)
dotnet run --project src/VidaroApi.Api

# All tests
dotnet test

# Single test class
dotnet test tests/VidaroApi.UnitTests --filter "FullyQualifiedName~ClassName"

# Single test method
dotnet test tests/VidaroApi.UnitTests --filter "FullyQualifiedName~ClassName.MethodName"

# EF Core migrations (always specify both projects)
dotnet ef migrations add <Name> --project src/VidaroApi.Infrastructure --startup-project src/VidaroApi.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/VidaroApi.Infrastructure --startup-project src/VidaroApi.Api

# Start dependencies
docker-compose up -d postgres redis minio
```

## Architecture

**Clean Architecture + Vertical Slice Architecture.** Each feature lives in a single self-contained file under `src/VidaroApi.Application/<Domain>/FeatureName.cs`.

### Project dependency flow

```
Domain ← Application ← Infrastructure ← Api
```

- **Domain** — entities, enums, `DomainError`. No external dependencies.
- **Application** — one file per feature (slice). Defines interfaces (`IMinioService`, `IJobQueueService`) that Infrastructure implements. No EF Core here.
- **Infrastructure** — EF Core `AppDbContext`, `MinioService`, `RedisJobQueueService`, `TokenService`, settings classes. All external I/O lives here.
- **Api** — `Program.cs` only. Registers DI, middleware, JWT, and calls `FeatureName.MapEndpoint(app)` for every slice.

### Vertical Slice pattern

Every slice in `Application/` follows this structure:

```csharp
public static class FeatureName
{
    public record Request(...) : IRequest<Response>;
    public record Response(...);
    public class Validator : AbstractValidator<Request> { ... }
    public class Handler(...) : IRequestHandler<Request, Response> { ... }
    public static void MapEndpoint(IEndpointRouteBuilder app) => app.MapPost(...);
}
```

Validation runs automatically via `ValidationBehavior<,>` (MediatR pipeline). FluentValidation exceptions are caught by `ExceptionMiddleware` and returned as 400.

### Integration with VideoProcessor (Go)

The VideoProcessor is a separate service at `../VideoProcessor`. Integration points:

1. **Upload** — API writes raw video to MinIO at `raw/{videoId}` via presigned PUT URL (client uploads directly, never through the API).
2. **Enqueue** — `IJobQueueService.PublishJobAsync(videoId, callbackUrl)` writes a `job:{videoId}` key to Redis and pushes `videoId` to `PROCESSING_REQUEST_QUEUE`.
3. **Webhook** — VideoProcessor calls `POST /webhooks/video-processed` when done. Validated with HMAC-SHA256 (`X-Webhook-Signature: sha256=...`). Secret is shared via `Webhook:Secret` config.

### MinIO object paths (shared contract with VideoProcessor)

| Path | Owner | Content |
|---|---|---|
| `raw/{videoId}` | API | Original upload |
| `processed/{videoId}_processed` | VideoProcessor | Transcoded video |
| `thumbnails/{videoId}/` | VideoProcessor | 5 JPG frames |
| `audio/{videoId}.mp3` | VideoProcessor | Audio track |
| `preview/{videoId}_preview.mp4` | VideoProcessor | Low-quality preview |
| `hls/{videoId}/` | VideoProcessor | HLS segments + playlist |

### Auth

DIY JWT — no ASP.NET Core Identity. `TokenService` (Infrastructure) generates access tokens (15 min) and refresh tokens (7 days). Refresh tokens are stored in `RefreshTokens` table and rotated on each use. Extract `UserId` from claims using `ctx.User.GetUserId()` (extension on `ClaimsPrincipal`).

### Key configuration sections (`appsettings.json`)

- `ConnectionStrings:Postgres`, `ConnectionStrings:Redis`
- `MinIO` — endpoint, credentials, bucket, `UploadUrlTtlHours`
- `Jwt` — secret, token expiry
- `VideoSettings:MaxTagsPerVideo` — validated in slices, not hardcoded
- `TrendingSettings` — score weights and time decay for `GET /videos/trending`
- `Webhook:Secret` — HMAC secret shared with VideoProcessor

## Design decisions

- **Counters are denormalized** — `LikeCount`, `DislikeCount`, `ViewCount` on `Videos` and `FollowerCount` on `Channels` are updated atomically via `ExecuteUpdateAsync`. Never use `COUNT(*)` for these.
- **Cursor-based pagination everywhere** — use `CreatedAt` as cursor, never `OFFSET`.
- **Videos belong to Channels, not Users** — `Video.ChannelId → Channel.UserId`. A user can own multiple channels.
- **`VideoArtifacts` and `VideoMetadata` are separate tables** — 1:1 with `Videos`, nullable until processing completes.
- **Single presigned PUT URL for upload** — multipart is planned but not implemented. See `docs/plans/` for future work.

## Implementation plan

See `docs/plans/2026-03-26-implementation-plan.md` for the full task-by-task plan.
