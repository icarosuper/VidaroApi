using CSharpFunctionalExtensions;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Api.Extensions;
using VidroApi.Domain.Errors;
using VidroApi.Domain.Errors.EntityErrors;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Api.Features.Comments;

public static class ListReplies
{
    public record Command : IRequest<Result<Response, Error>>
    {
        public Guid CommentId { get; init; }
        public int Limit { get; init; }
        public DateTimeOffset? Cursor { get; init; }
    }

    public record Response
    {
        public List<ReplySummary> Replies { get; init; } = [];
        public DateTimeOffset? NextCursor { get; init; }

        public record ReplySummary
        {
            public Guid CommentId { get; init; }
            public Guid UserId { get; init; }
            public string Username { get; init; } = null!;
            public string? Content { get; init; }
            public bool IsDeleted { get; init; }
            public int LikeCount { get; init; }
            public int DislikeCount { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? UpdatedAt { get; init; }
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOptions<ListRepliesSettings> options)
        {
            RuleFor(x => x.Limit)
                .InclusiveBetween(1, options.Value.MaxLimit)
                .WithMessage(x => $"Limit must be between 1 and {options.Value.MaxLimit}.");
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/comments/{commentId:guid}/replies", async (
            Guid commentId,
            IMediator mediator,
            int limit,
            DateTimeOffset? cursor,
            CancellationToken ct) =>
        {
            var cmd = new Command { CommentId = commentId, Limit = limit, Cursor = cursor };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status200OK);
        });

    public class Handler(AppDbContext db)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var commentExists = await db.Comments.AnyAsync(c => c.Id == cmd.CommentId, ct);
            if (!commentExists)
                return Errors.Comment.NotFound(cmd.CommentId);

            var replies = await FetchReplies(cmd.CommentId, cmd.Cursor, cmd.Limit, ct);

            var nextCursor = replies.Count == cmd.Limit
                ? replies[^1].CreatedAt
                : (DateTimeOffset?)null;

            return new Response
            {
                Replies = replies,
                NextCursor = nextCursor
            };
        }

        private Task<List<Response.ReplySummary>> FetchReplies(
            Guid commentId, DateTimeOffset? cursor, int limit, CancellationToken ct)
        {
            var query = db.Comments.Where(c => c.ParentCommentId == commentId);

            if (cursor.HasValue)
                query = query.Where(c => c.CreatedAt > cursor.Value);

            return query
                .OrderBy(c => c.CreatedAt)
                .Take(limit)
                .Select(c => new Response.ReplySummary
                {
                    CommentId = c.Id,
                    UserId = c.UserId,
                    Username = c.User.Username,
                    Content = c.IsDeleted ? null : c.Content,
                    IsDeleted = c.IsDeleted,
                    LikeCount = c.LikeCount,
                    DislikeCount = c.DislikeCount,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync(ct);
        }
    }
}
