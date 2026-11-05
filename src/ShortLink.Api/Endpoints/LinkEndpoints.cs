using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using ShortLink.Api.Features.Links;

namespace ShortLink.Api.Endpoints;

public static class LinkEndpoints
{
    public sealed record CreateLinkRequest(string TargetUrl, string? CustomCode = null, int? TtlMinutes = null);

    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/links").WithTags("Links");

        group.MapPost("/", async Task<Created<CreateLink.Response>> (CreateLinkRequest body, ISender sender, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateLink.Command(body.TargetUrl, body.CustomCode, body.TtlMinutes), ct);
            var shortUrl = $"{http.Request.Scheme}://{http.Request.Host}/{result.Code}";
            return TypedResults.Created(shortUrl, result);
        })
        .WithName("CreateLink")
        .WithSummary("Create a short link")
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async Task<Ok<IReadOnlyList<ListLinks.Item>>> (int? limit, bool? includeExpired, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ListLinks.Query(limit, includeExpired ?? false), ct)))
        .WithName("ListLinks")
        .WithSummary("List recent short links, newest first");

        group.MapGet("/{code}", async Task<Ok<GetLinkStats.Response>> (string code, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetLinkStats.Query(code), ct)))
        .WithName("GetLinkStats")
        .WithSummary("Get metadata and hit count for a short link")
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{code}", async Task<NoContent> (string code, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteLink.Command(code), ct);
            return TypedResults.NoContent();
        })
        .WithName("DeleteLink")
        .WithSummary("Delete a short link")
        .ProducesProblem(StatusCodes.Status404NotFound);

        // The public redirect lives at the root so short URLs stay short: https://host/{code}
        app.MapGet("/{code:regex(^[A-Za-z0-9_-]{{4,16}}$)}", async Task<RedirectHttpResult> (string code, ISender sender, CancellationToken ct) =>
        {
            var target = await sender.Send(new ResolveLink.Query(code), ct);
            return TypedResults.Redirect(target, permanent: false);
        })
        .WithName("Redirect")
        .WithSummary("Redirect to the original URL")
        .WithTags("Redirect")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status410Gone);

        return app;
    }
}
