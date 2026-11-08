# Spike: FBX → GLB with Blender headless

Goal (GĐ0): confirm that an `.fbx` exported from Blender keeps its textures and PBR inputs after converting to `.glb`, and that the optimized file fits the ≤ 10 MB target.

## Run

```bash
# 1. Convert (Blender must be on PATH; on Windows use the full path to blender.exe)
blender -b --factory-startup --python convert_fbx_to_glb.py -- ../../samples/model.fbx out/model.glb

# 2. Optimize (Node LTS required; flags may differ between gltf-transform versions — check `--help`)
npx @gltf-transform/cli optimize out/model.glb out/model.opt.glb --compress meshopt --texture-compress webp

# 3. Inspect
npx @gltf-transform/cli inspect out/model.opt.glb
```

Put sample models in `samples/` at the repo root (ignored by git).

## What to record

| Model | Triangles | .fbx size | .glb size | Optimized size | Maps that survived | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| | | | | | | |

The material report printed by the script lists, per material, whether base color, metallic, roughness and normal are still linked, and flags images that are **MISSING** (usually: textures not embedded when exporting the `.fbx` — use Path Mode = Copy + Embed Textures).

## Decide after the spike

- Blender convert keeps the maps → use it in the worker (GĐ2).
- Maps are lost → document the required export settings, or prefer uploading `.glb` for models that show off their maps.
- Compare with FBX2glTF only if Blender's result is poor.
