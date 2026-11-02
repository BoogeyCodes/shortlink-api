using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Common;
using ShortLink.Api.Infrastructure;

namespace ShortLink.Api.Features.Links;

public static class DeleteLink
{
    public sealed record Command(string Code) : IRequest;

    public sealed class Handler(AppDbContext db, ILogger<Handler> logger) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken ct)
        {
            var deleted = await db.Links.Where(l => l.Code == request.Code).ExecuteDeleteAsync(ct);
            if (deleted == 0)
            {
                throw new NotFoundException($"No link with code '{request.Code}'.");
            }

            logger.LogInformation("Deleted short link {Code}", request.Code);
        }
    }
}
