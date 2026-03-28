using System.Security.Claims;
using CSharpFunctionalExtensions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using VidroApi.Api.Extensions;
using VidroApi.Domain.Entities;
using VidroApi.Domain.Errors;
using VidroApi.Domain.Errors.EntityErrors;
using VidroApi.Infrastructure.Persistence;

namespace VidroApi.Api.Features.Channels;

public static class DeleteChannel
{
    public record Command : IRequest<UnitResult<Error>>
    {
        public Guid ChannelId { get; init; }
        public Guid UserId { get; init; }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/v1/channels/{channelId:guid}", async (
            Guid channelId,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new Command
            {
                ChannelId = channelId,
                UserId = user.GetUserId()
            };
            var result = await mediator.Send(cmd, ct);
            return result.ToApiResult();
        })
        .RequireAuthorization();

    public class Handler(AppDbContext db) : IRequestHandler<Command, UnitResult<Error>>
    {
        public async ValueTask<UnitResult<Error>> Handle(Command cmd, CancellationToken ct)
        {
            var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == cmd.ChannelId, ct);

            if (channel is null)
                return CommonErrors.NotFound(nameof(Channel), cmd.ChannelId);

            var userIsNotOwner = channel.UserId != cmd.UserId;
            if (userIsNotOwner)
                return Errors.Channel.NotOwner();

            var followers = await db.ChannelFollowers
                .Where(cf => cf.ChannelId == cmd.ChannelId)
                .ToListAsync(ct);
            db.ChannelFollowers.RemoveRange(followers);

            db.Channels.Remove(channel);
            await db.SaveChangesAsync(ct);

            return UnitResult.Success<Error>();
        }
    }
}
