using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Infrastructure;

namespace ShortLink.Api.Features.Links;

/// <summary>Lists the most recently created links, newest first.</summary>
public static class ListLinks
{
    public sealed record Query(int? Limit, bool IncludeExpired) : IRequest<IReadOnlyList<Item>>;

    public sealed record Item(
        string Code,
        string TargetUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        bool IsExpired,
        long HitCount);

    public sealed class Handler(AppDbContext db, IClock clock) : IRequestHandler<Query, IReadOnlyList<Item>>
    {
        public async Task<IReadOnlyList<Item>> Handle(Query request, CancellationToken ct)
        {
            var now = clock.UtcNow;
            var limit = Math.Clamp(request.Limit ?? 50, 1, 200);

            // Id is auto-incremented in creation order, so ordering by it is "newest first"
            // without relying on SQLite ordering a DateTimeOffset column. Expiry is a
            // DateTimeOffset comparison SQLite can't translate either, so it's applied in memory.
            var links = await db.Links.AsNoTracking()
                .OrderByDescending(l => l.Id)
                .ToListAsync(ct);

            return links
                .Where(l => request.IncludeExpired || !l.IsExpired(now))
                .Take(limit)
                .Select(l => new Item(l.Code, l.TargetUrl, l.CreatedAt, l.ExpiresAt, l.IsExpired(now), l.HitCount))
                .ToList();
        }
    }
}
