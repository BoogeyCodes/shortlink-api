using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Common;
using ShortLink.Api.Infrastructure;

namespace ShortLink.Api.Features.Links;

public static class GetLinkStats
{
    public sealed record Query(string Code) : IRequest<Response>;

    public sealed record Response(
        string Code,
        string TargetUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        bool IsExpired,
        long HitCount,
        DateTimeOffset? LastAccessedAt);

    public sealed class Handler(AppDbContext db, IClock clock) : IRequestHandler<Query, Response>
    {
        public async Task<Response> Handle(Query request, CancellationToken ct)
        {
            var link = await db.Links.AsNoTracking().SingleOrDefaultAsync(l => l.Code == request.Code, ct)
                       ?? throw new NotFoundException($"No link with code '{request.Code}'.");

            return new Response(
                link.Code,
                link.TargetUrl,
                link.CreatedAt,
                link.ExpiresAt,
                link.IsExpired(clock.UtcNow),
                link.HitCount,
                link.LastAccessedAt);
        }
    }
}
