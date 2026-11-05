using System.Net;
using System.Net.Http.Json;
using ShortLink.Api.Features.Links;

namespace ShortLink.Api.Tests;

public sealed class LinkEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public LinkEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Create_returns_201_with_generated_code_and_location()
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = "https://example.com/docs" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateLink.Response>();
        Assert.NotNull(body);
        Assert.Equal(7, body.Code.Length);
        Assert.Equal("https://example.com/docs", body.TargetUrl);
        Assert.Null(body.ExpiresAt);
        Assert.EndsWith($"/{body.Code}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_honours_custom_code_and_rejects_duplicates()
    {
        var first = await _client.PostAsJsonAsync("/api/links", new { targetUrl = "https://example.com", customCode = "my-docs" });
        var second = await _client.PostAsJsonAsync("/api/links", new { targetUrl = "https://example.org", customCode = "my-docs" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    public async Task Create_rejects_invalid_target_urls(string target)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = target });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(problem);
        Assert.True(problem.ContainsKey("errors"));
    }

    [Fact]
    public async Task Redirect_returns_302_to_target_and_increments_hit_count()
    {
        var created = await CreateAsync("https://example.com/hits");

        var redirect1 = await _client.GetAsync($"/{created.Code}");
        var redirect2 = await _client.GetAsync($"/{created.Code}");
        var stats = await _client.GetFromJsonAsync<GetLinkStats.Response>($"/api/links/{created.Code}");

        Assert.Equal(HttpStatusCode.Found, redirect1.StatusCode);
        Assert.Equal("https://example.com/hits", redirect1.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Found, redirect2.StatusCode);
        Assert.NotNull(stats);
        Assert.Equal(2, stats.HitCount);
        Assert.Equal(_factory.Clock.UtcNow, stats.LastAccessedAt);
    }

    [Fact]
    public async Task Redirect_returns_404_for_unknown_code()
    {
        var response = await _client.GetAsync("/nope404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Expired_link_returns_410_and_stats_reflect_expiry()
    {
        var created = await CreateAsync("https://example.com/temp", ttlMinutes: 10);
        Assert.NotNull(created.ExpiresAt);

        var before = await _client.GetAsync($"/{created.Code}");
        _factory.Clock.Advance(TimeSpan.FromMinutes(11));
        var after = await _client.GetAsync($"/{created.Code}");
        var stats = await _client.GetFromJsonAsync<GetLinkStats.Response>($"/api/links/{created.Code}");

        Assert.Equal(HttpStatusCode.Found, before.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, after.StatusCode);
        Assert.True(stats!.IsExpired);
        Assert.Equal(1, stats.HitCount);
    }

    [Fact]
    public async Task Delete_removes_link_and_subsequent_lookups_404()
    {
        var created = await CreateAsync("https://example.com/delete-me");

        var delete = await _client.DeleteAsync($"/api/links/{created.Code}");
        var stats = await _client.GetAsync($"/api/links/{created.Code}");
        var deleteAgain = await _client.DeleteAsync($"/api/links/{created.Code}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stats.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_generates_without_schema_conflicts()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("CreateLinkResponse", json);
        Assert.Contains("GetLinkStatsResponse", json);
    }

    [Fact]
    public async Task Health_endpoint_is_available()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_recent_links_newest_first()
    {
        var older = await CreateAsync("https://example.com/first");
        var newer = await CreateAsync("https://example.com/second");

        var response = await _client.GetAsync("/api/links?limit=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<ListLinks.Item>>();
        Assert.NotNull(items);
        var codes = items!.Select(i => i.Code).ToList();
        Assert.Contains(newer.Code, codes);
        Assert.Contains(older.Code, codes);
        Assert.True(codes.IndexOf(newer.Code) < codes.IndexOf(older.Code), "newest link should come first");
    }

    private async Task<CreateLink.Response> CreateAsync(string target, int? ttlMinutes = null)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new { targetUrl = target, ttlMinutes });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateLink.Response>())!;
    }
}
