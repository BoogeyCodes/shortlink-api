using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Common;
using ShortLink.Api.Infrastructure;

namespace ShortLink.Api.Features.Links;

/// <summary>Looks up a code, records the hit, and returns the target to redirect to.</summary>
public static class ResolveLink
{
    public sealed record Query(string Code) : IRequest<string>;

    public sealed class Handler(AppDbContext db, IClock clock) : IRequestHandler<Query, string>
    {
        public async Task<string> Handle(Query request, CancellationToken ct)
        {
            var link = await db.Links.SingleOrDefaultAsync(l => l.Code == request.Code, ct)
                       ?? throw new NotFoundException($"No link with code '{request.Code}'.");

            var now = clock.UtcNow;
            if (link.IsExpired(now))
            {
                throw new GoneException($"Link '{request.Code}' expired at {link.ExpiresAt:O}.");
            }

            link.HitCount++;
            link.LastAccessedAt = now;
            await db.SaveChangesAsync(ct);

            return link.TargetUrl;
        }
    }
}
