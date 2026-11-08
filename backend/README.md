# backend

ASP.NET Core (.NET 10) API for Studio and the Showcase build.

```
Showcase.slnx
src/Showcase.Domain          entities (Model, translations, files, stats, images, categories, tags, profile)
src/Showcase.Application     request/response contracts, FluentValidation rules, IFileStorage
src/Showcase.Infrastructure  EF Core + PostgreSQL (AppDbContext), LocalFileStorage
src/Showcase.Api             minimal-API endpoints, single-password cookie auth, rate limiting
tests/Showcase.Api.Tests     xUnit + WebApplicationFactory + Testcontainers (real PostgreSQL)
```

## Run locally

Requires the .NET 10 SDK and Docker.

```bash
# 1. Database
cp deploy/.env.example deploy/.env
docker compose -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d

# 2. Admin password hash (store it with user-secrets, never in git)
cd backend
dotnet run --project src/Showcase.Api -- hash-password "your-password"
dotnet user-secrets init --project src/Showcase.Api
dotnet user-secrets set "Auth:AdminPasswordHash" "<hash printed above>" --project src/Showcase.Api

# 3. API (Development creates the schema automatically; OpenAPI at /openapi/v1.json)
dotnet run --project src/Showcase.Api

# Tests (Docker must be running)
dotnet test Showcase.slnx
```

## Endpoints so far

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/health` | — |
| POST | `/api/auth/login` · GET `/api/auth/me` · POST `/api/auth/logout` | login is rate-limited |
| GET/POST/PUT/DELETE | `/api/admin/categories` | owner |
| GET/POST/DELETE | `/api/admin/models`, PATCH `/api/admin/models/{id}/status` | owner |
| GET | `/api/public/categories`, `/api/public/models`, `/api/public/models/{slug}` | — (published only) |

To log out every session, raise `Auth:SessionVersion`.
