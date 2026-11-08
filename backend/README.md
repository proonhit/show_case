# backend

ASP.NET Core (.NET 10) solution — created in GĐ1.

```
src/Showcase.Api             HTTP endpoints, auth, tus upload
src/Showcase.Application     use cases, validation
src/Showcase.Domain          entities
src/Showcase.Infrastructure  EF Core (PostgreSQL), file storage, Hangfire
src/Showcase.Worker          background jobs: convert, optimize, build Showcase
tests/                       xUnit + Testcontainers
```
