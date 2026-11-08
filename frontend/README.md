# frontend

pnpm workspace (Node 22, pnpm 10).

```
packages/viewer-core   Three.js viewer, framework-free TypeScript (GĐ3)
packages/api-client    typed client (openapi-fetch) generated from the API's OpenAPI document (GĐ4)
apps/studio            Vue 3 admin (GĐ4)
apps/showcase          Nuxt static site, EN/VI (GĐ5)
```

```bash
cd frontend
pnpm install
pnpm viewer:dev      # demo page at http://localhost:5173 — drop a .glb, keys 1–7 switch map mode
pnpm --filter @showcase/studio dev   # Studio at http://localhost:5174, /api proxied to the API on :5080
pnpm test            # Vitest
pnpm build
```

`packages/api-client/openapi/Showcase.Api.json` and `src/schema.d.ts` are regenerated from the backend by the
*API contract* workflow whenever `backend/src` changes on a branch — never edit them by hand.

`pnpm-lock.yaml` is kept up to date by the *Frontend lockfile* workflow when a `package.json` changes on a branch.

## viewer-core

```ts
import { ModelViewer } from '@showcase/viewer-core';

const viewer = new ModelViewer(element, { autoRotate: true });
viewer.on('progress', (p) => console.log(p.ratio));
const info = await viewer.load('/models/helmet.glb');   // triangles, meshes, available modes…
viewer.setMode('roughness');                             // false when the model has no roughness map
viewer.resetCamera();
viewer.dispose();
```

Vue: `import { ModelViewerView } from '@showcase/viewer-core/vue'`, then
`<ModelViewerView :src="url" v-model:mode="mode" v-model:auto-rotate="spin" />`.

| Mode | Shows | glTF source |
| --- | --- | --- |
| Final | lit, as authored | — |
| Wireframe | triangle edges | geometry |
| Base color | albedo, unlit | `baseColorTexture` × factor |
| Metallic | grey value | B channel of `metallicRoughnessTexture` × factor |
| Roughness | grey value | G channel of `metallicRoughnessTexture` × factor |
| Normal | tangent-space normal map | `normalTexture` |
| AO | grey value | R channel of `occlusionTexture` |

A mode is disabled when no material in the model has its map. Materials are swapped in place, so switching never reloads the file.
