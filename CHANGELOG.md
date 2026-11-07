# Changelog

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- `GET /api/links` — list recent links, newest first, with a `limit` (1–200) and an
  `includeExpired` flag. Ordered by id so it doesn't depend on SQLite sorting a
  `DateTimeOffset` column.

## [0.1.0]

### Added
- Create, resolve (redirect), stat and delete short links.
- Custom codes and optional TTL expiry; RFC 7807 problem responses.
- Minimal API + MediatR CQRS + EF Core (SQLite) + FluentValidation + Serilog.
