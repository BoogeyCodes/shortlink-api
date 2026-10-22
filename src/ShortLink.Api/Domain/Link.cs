namespace ShortLink.Api.Domain;

/// <summary>A shortened link and its usage counters.</summary>
public sealed class Link
{
    public int Id { get; set; }

    /// <summary>The short code that appears in the URL, e.g. <c>k3Xp9a</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Optional expiry. Null means the link never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public long HitCount { get; set; }

    public DateTimeOffset? LastAccessedAt { get; set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;
}
