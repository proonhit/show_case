# Spike: viewer with 7 map modes

Goal (GĐ0): prove that Three.js can show each texture channel of a glTF model by swapping materials, before building `viewer-core` in GĐ3.

## Run

ES modules need a web server (opening the file directly will not work):

```bash
cd spikes/viewer
npx serve .        # or: python -m http.server 8080
```

Open the printed URL, then drop a `.glb` (raw or optimized with Meshopt/KTX2) onto the page.

- Drag to orbit, scroll to zoom; **Turntable** rotates 360° until you touch the model.
- Keys **1–7**: Final, Wireframe, Base color, Metallic, Roughness, Normal, AO.
- Modes the file has no map for are disabled; the info line lists which maps were found.

## How the channels are read

glTF packs **metallic in B** and **roughness in G** of `metallicRoughnessTexture`, and **occlusion in R** of `occlusionTexture`. A small `ShaderMaterial` outputs one raw channel as grey, without color-space conversion, so the values match what the artist painted. Materials are cached per mode, so switching never reloads the file.

## Known limits (fine for a spike)

- Skinned/animated meshes and multi-material meshes are not handled.
- `KHR_texture_transform` and a second UV set for AO are ignored.
