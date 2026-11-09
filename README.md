# show_case

3D model showcase and management — a personal portfolio where every model can be rotated 360° in the browser and inspected map by map (wireframe, base color, metallic, roughness, normal, AO).

The project has two web apps sharing one .NET backend:

- **Studio** — private admin (single owner) to upload `.glb` / `.fbx`, fill in EN/VI details and publish.
- **Showcase** — public, statically built site for recruiters, deployed to Cloudflare Pages so it stays up even when the server is off.

> Status: GĐ0–GĐ5 built (backend, pipeline, viewer, Studio, Showcase); GĐ6 deploy next — see [docs/plan.md](docs/plan.md). Full spec: [docs/spec.md](docs/spec.md).

## Run it locally

Needs **Docker Desktop** (running), **Git**, and for the web apps **Node 22** + **pnpm** (`corepack enable`).

```powershell
git clone https://github.com/proonhit/show_case.git
cd show_case
./scripts/dev-up.ps1 -Frontend     # first run asks for the Studio password; first build takes ~5–10 min (Blender)
```

| | |
| --- | --- |
| Studio (admin) | http://localhost:5174 — log in with the password you chose |
| Showcase (public) | http://localhost:5175 — rebuilt from published models: `$env:API_URL='http://localhost:5080'; pnpm --filter @showcase/showcase build; pnpm --filter @showcase/showcase preview` (in `frontend/`) |
| API | http://localhost:5080/health, OpenAPI at `/openapi/v1.json` |

`./scripts/dev-up.ps1 -Down` stops everything (data kept); `-Reset` deletes the local data. Without `-Frontend` only the backend starts; then run `pnpm install` and `pnpm --filter @showcase/studio dev` in `frontend/` yourself. On macOS/Linux use `pwsh ./scripts/dev-up.ps1`.

## Architecture

```mermaid
flowchart TB
    subgraph Server["Self-hosted server · Docker Compose"]
        Tunnel["cloudflared"] --> Nginx["nginx"] --> API["ASP.NET Core API (.NET 10)"]
        API --> DB[("PostgreSQL")]
        API --> Disk["/data/uploads"]
        API --> Worker["Worker (Hangfire)<br/>Blender · gltf-transform"]
        Worker --> Disk
    end
    Studio["Studio (Vue 3)"] -- "HTTPS · cookie" --> Tunnel
    Worker -- "deploy static build" --> Pages["Showcase on Cloudflare Pages<br/>Nuxt SSG · EN/VI"]
    Visitor["Recruiter"] --> Pages
```

## Tech stack

| Area | Choice |
| --- | --- |
| Backend | ASP.NET Core (.NET 10), EF Core + Npgsql, FluentValidation, Serilog |
| Upload | tus (tusdotnet + tus-js-client), chunked, up to 100 MB |
| 3D pipeline | Blender headless (.fbx → .glb), `gltf-transform` (Meshopt, WebP), SharpGLTF (stats), ImageSharp |
| Jobs | Hangfire on PostgreSQL, separate worker container |
| Studio | Vue 3 + Vite + TypeScript, TanStack Vue Query, typed client from OpenAPI |
| Showcase | Nuxt (static generate), @nuxtjs/i18n |
| Viewer | Three.js in a framework-free TypeScript package `viewer-core` |
| Infra | Docker Compose, Cloudflare Tunnel, Cloudflare Pages, GitHub Actions |

## Repository layout

```
backend/     .NET solution: Api, Application, Domain, Infrastructure, Worker   (GĐ1)
frontend/    pnpm workspace: studio, showcase, viewer-core, api-client         (GĐ3–GĐ5)
deploy/      docker-compose, nginx, cloudflared config, .env.example           (GĐ1, GĐ6)
spikes/      throwaway experiments for GĐ0 (FBX convert, viewer map modes)
docs/        spec and plan
```

## GĐ0 spikes

- [`spikes/convert`](spikes/convert) — convert `.fbx` to `.glb` with Blender headless, then optimize with `gltf-transform`.
- [`spikes/viewer`](spikes/viewer) — single HTML page: drop a `.glb`, orbit 360°, switch the 7 map modes with keys 1–7.
