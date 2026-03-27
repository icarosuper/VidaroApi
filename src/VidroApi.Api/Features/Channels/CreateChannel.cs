using System.Security.Claims;
using CSharpFunctionalExtensions;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VidroApi.Api.Extensions;
using VidroApi.Application.Abstractions;
using VidroApi.Domain.Entities;
using VidroApi.Domain.Errors;
using VidroApi.Domain.Errors.EntityErrors;
using VidroApi.Infrastructure.Persistence;
using VidroApi.Infrastructure.Settings;

namespace VidroApi.Api.Features.Channels;

public static class CreateChannel
{
    public record Request
    {
        public string Name { get; init; } = null!;
        public string? Description { get; init; }
    }

    public record Command : IRequest<Result<Response, Error>>
    {
        public Guid UserId { get; init; }
        public string Name { get; init; } = null!;
        public string? Description { get; init; }
    }

    public record Response
    {
        public Guid ChannelId { get; init; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(Channel.NameMaxLength);

            RuleFor(x => x.Description)
                .MaximumLength(Channel.DescriptionMaxLength)
                .When(x => x.Description is not null);
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/v1/channels", async (
            Request req,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new Command
            {
                UserId = user.GetUserId(),
                Name = req.Name,
                Description = req.Description
            };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization();

    public class Handler(AppDbContext db, IDateTimeProvider clock, IOptions<ChannelSettings> channelOptions)
        : IRequestHandler<Command, Result<Response, Error>>
    {
        public async ValueTask<Result<Response, Error>> Handle(Command cmd, CancellationToken ct)
        {
            var maxChannels = channelOptions.Value.MaxChannelsPerUser;
            var channelCount = await db.Channels.CountAsync(c => c.UserId == cmd.UserId, ct);
            var limitReached = channelCount >= maxChannels;
            if (limitReached)
                return Errors.Channel.LimitReached(maxChannels);

            var channel = new Channel(cmd.UserId, cmd.Name, cmd.Description, clock.UtcNow);

            db.Channels.Add(channel);
            await db.SaveChangesAsync(ct);

            return new Response
            {
                ChannelId = channel.Id
            };
        }
    }
}
