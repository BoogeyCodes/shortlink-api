# ShortLink API

[![CI](https://github.com/BoogeyCodes/shortlink-api/actions/workflows/ci.yml/badge.svg)](https://github.com/BoogeyCodes/shortlink-api/actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![License: MIT](https://img.shields.io/badge/license-MIT-green)

A small, production-shaped URL shortener built with **ASP.NET Core 8 minimal APIs**.
It is intentionally compact, but it is wired the way a real service would be: CQRS handlers
with MediatR, validation in the pipeline, EF Core persistence, RFC 7807 error responses,
structured logging, integration tests against the real HTTP stack, and a multi-stage Docker build.

```
POST /api/links            create a short link (optional custom code and TTL)
GET  /{code}               302 redirect to the target, records the hit
GET  /api/links/{code}     metadata, hit count, last access, expiry state
DELETE /api/links/{code}   remove a link
GET  /health               liveness probe
GET  /swagger              interactive API docs
```

## Why this exists

I wanted a reference project that answers "how do you structure a .NET API?" in a few hundred
lines instead of a few thousand. Every piece is something I reach for on real work:

| Concern            | Choice                                              |
| ------------------ | --------------------------------------------------- |
| Request handling   | Vertical slices, one file per feature, MediatR      |
| Validation         | FluentValidation run as a MediatR pipeline behavior |
| Persistence        | EF Core 8 + SQLite (swap the provider in one line)  |
| Errors             | `IExceptionHandler` mapping to `ProblemDetails`     |
| Logging            | Serilog with request logging                        |
| Testing            | xUnit + `WebApplicationFactory`, fake clock, temp DB |
| Packaging          | Multi-stage Dockerfile, non-root runtime user       |
| CI                 | GitHub Actions: restore, build, test, Docker build  |

## Running it

### With the .NET SDK

```bash
dotnet run --project src/ShortLink.Api
```

Then open <http://localhost:5000/swagger> (the port is printed on startup).
A `shortlink.db` SQLite file is created next to the binary on first run.

### With Docker

```bash
docker build -t shortlink-api .
docker run --rm -p 8080:8080 -v shortlink-data:/data shortlink-api
```

### Try it

```bash
# create
curl -s -X POST http://localhost:5000/api/links \
  -H "Content-Type: application/json" \
  -d '{"targetUrl":"https://example.com/some/very/long/path","customCode":"demo","ttlMinutes":60}'

# {"code":"demo","targetUrl":"https://example.com/some/very/long/path","createdAt":"...","expiresAt":"..."}

# follow (302)
curl -i http://localhost:5000/demo

# stats
curl -s http://localhost:5000/api/links/demo
# {"code":"demo","targetUrl":"...","hitCount":1,"lastAccessedAt":"...","isExpired":false,...}
```

A ready-made [`requests.http`](requests.http) file is included for the VS Code REST Client extension.

## Running the tests

```bash
dotnet test
```

The integration tests boot the full application in-process against a throwaway SQLite file
and replace `IClock` with a controllable fake, so expiry behaviour is tested by advancing time
rather than sleeping.

## Project layout

```
src/ShortLink.Api
├── Program.cs                 composition root: Serilog, EF Core, MediatR, Swagger
├── Domain/Link.cs             the entity and its expiry rule
├── Features/Links/            one file per use case (command/query + validator + handler)
│   ├── CreateLink.cs
│   ├── ResolveLink.cs
│   ├── GetLinkStats.cs
│   └── DeleteLink.cs
├── Endpoints/LinkEndpoints.cs HTTP surface only; delegates to MediatR
├── Common/                    validation pipeline, exception -> ProblemDetails mapping
└── Infrastructure/            DbContext, code generator, clock abstraction
tests/ShortLink.Api.Tests      xUnit integration + unit tests
```

## Design notes

- **Short codes** are 7 characters from a 55-character alphabet that omits look-alikes
  (`0/O`, `1/l/I`). That is roughly 1.5 × 10¹² combinations; collisions are still checked and
  retried.
- **Expiry** is evaluated on read, not by a background sweeper. Expired links return `410 Gone`
  so clients can distinguish "never existed" from "used to exist".
- **Hit counting** happens in the redirect handler inside the same `SaveChanges`. For very high
  traffic you would move this to an async channel or a counter in Redis; it is kept synchronous
  here for clarity.
- **Validation** rejects anything that is not an absolute `http`/`https` URL, which also blocks
  `javascript:` and `data:` schemes from being stored.

## Possible next steps

- API key authentication for the management endpoints
- Rate limiting on link creation using the built-in `AddRateLimiter`
- PostgreSQL provider and EF migrations for a multi-instance deployment
- QR code generation for each short link

## License

MIT
