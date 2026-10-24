using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortLink.Api.Domain;
using ShortLink.Api.Infrastructure;

namespace ShortLink.Api.Features.Links;

public static class CreateLink
{
    public sealed record Command(string TargetUrl, string? CustomCode, int? TtlMinutes) : IRequest<Response>;

    public sealed record Response(string Code, string TargetUrl, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.TargetUrl)
                .NotEmpty()
                .MaximumLength(2048)
                .Must(BeAbsoluteHttpUrl).WithMessage("TargetUrl must be an absolute http or https URL.");

            RuleFor(c => c.CustomCode)
                .Length(4, 16)
                .Matches("^[A-Za-z0-9_-]+$").WithMessage("CustomCode may only contain letters, digits, '-' and '_'.")
                .When(c => c.CustomCode is not null);

            RuleFor(c => c.TtlMinutes)
                .InclusiveBetween(1, 60 * 24 * 365)
                .When(c => c.TtlMinutes is not null);
        }

        private static bool BeAbsoluteHttpUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public sealed class Handler(AppDbContext db, ICodeGenerator codes, IClock clock, ILogger<Handler> logger)
        : IRequestHandler<Command, Response>
    {
        private const int DefaultCodeLength = 7;
        private const int MaxAttempts = 5;

        public async Task<Response> Handle(Command request, CancellationToken ct)
        {
            var now = clock.UtcNow;
            var expiresAt = request.TtlMinutes is { } ttl ? now.AddMinutes(ttl) : (DateTimeOffset?)null;

            if (request.CustomCode is { } custom)
            {
                if (await db.Links.AnyAsync(l => l.Code == custom, ct))
                {
                    throw new CodeTakenException(custom);
                }

                return await Persist(custom, request.TargetUrl, now, expiresAt, ct);
            }

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var code = codes.Generate(DefaultCodeLength);
                if (await db.Links.AnyAsync(l => l.Code == code, ct))
                {
                    logger.LogWarning("Short code collision on attempt {Attempt}; regenerating", attempt);
                    continue;
                }

                return await Persist(code, request.TargetUrl, now, expiresAt, ct);
            }

            throw new InvalidOperationException($"Could not allocate a unique code after {MaxAttempts} attempts.");
        }

        private async Task<Response> Persist(string code, string target, DateTimeOffset now, DateTimeOffset? expiresAt, CancellationToken ct)
        {
            var link = new Link
            {
                Code = code,
                TargetUrl = target,
                CreatedAt = now,
                ExpiresAt = expiresAt,
            };

            db.Links.Add(link);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Created short link {Code} -> {TargetUrl}", link.Code, link.TargetUrl);
            return new Response(link.Code, link.TargetUrl, link.CreatedAt, link.ExpiresAt);
        }
    }

    public sealed class CodeTakenException(string code) : Exception($"The code '{code}' is already in use.");
}
