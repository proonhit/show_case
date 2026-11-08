# Implementation plan (MVP)

MVP target: **17/01/2027**, about 10 weeks at 10–12 hours/week (~115 h). Riskiest parts first: the viewer's per-channel map modes and FBX conversion are tested in GĐ0 before real code is written.

Working rules: every phase ends with a runnable build and a git tag; `main` always builds; work happens on branches merged through pull requests; "done" means tests for the core logic, green CI, and an up-to-date README.

| Phase | Dates | Hours | Outcome | Tag |
| --- | --- | --- | --- | --- |
| GĐ0 Setup & spikes | 09/11 – 15/11 | 10 | Repo skeleton, FBX→GLB spike, viewer map-mode spike | — |
| GĐ1 Backend foundation | 16/11 – 29/11 | 22 | API, DB, single-password login, tus upload, Hangfire, CI | v0.1 |
| GĐ2 3D pipeline | 30/11 – 06/12 | 12 | Upload .fbx → optimized .glb + stats + thumbnails | v0.2 |
| GĐ3 viewer-core | 07/12 – 13/12 | 12 | 360° viewer, 7 map modes, mobile tuning | v0.3 |
| GĐ4 Studio | 14/12 – 27/12 | 22 | Full admin UI (EN/VI form, upload, preview, publish) | v0.4 |
| GĐ5 Showcase + build | 28/12 – 10/01 | 22 | Static EN/VI site on Cloudflare Pages, rebuilt on publish | v0.5 |
| GĐ6 Deploy & launch | 11/01 – 17/01 | 15 | Server, Tunnel, domain, CI/CD, backups, first models | v1.0 |

## GĐ0 checklist

- [x] Repo skeleton: `backend/`, `frontend/`, `deploy/`, `docs/`, `.gitignore`, `.editorconfig`, `.env.example` — done 08/11 (PR #1)
- [x] Write spike scripts: `spikes/convert` (Blender FBX → GLB) and `spikes/viewer` (7 map modes) — done 08/11 (PR #1)
- [ ] Install tools: .NET 10 SDK, Node LTS + pnpm, Docker Desktop, Blender, `gltf-transform`
- [ ] 3 sample models in `samples/`: one `.glb`, one `.fbx` with embedded textures, one ~300K triangles
- [ ] Run `spikes/convert` on the `.fbx`, fill in the results table
- [ ] Run `spikes/viewer` on all samples, check each map mode
- [ ] Optimize the heavy model, confirm ≤ 10 MB

Done when the sample `.fbx` converts with textures intact, the viewer shows the roughness channel correctly, and the heavy model is ≤ 10 MB after optimization.

The detailed task list for every phase lives in the shared plan document.
