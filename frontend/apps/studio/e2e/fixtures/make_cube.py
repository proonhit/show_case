"""Writes cube.glb: a small PBR cube with base color, metallic-roughness, normal and occlusion textures,
so every map mode in the viewer has something to show. Run: python3 make_cube.py"""
import io, json, struct
from PIL import Image

def png(fn, size=64):
    img = Image.new("RGB", (size, size))
    img.putdata([fn(x, y, size) for y in range(size) for x in range(size)])
    buf = io.BytesIO(); img.save(buf, "PNG"); return buf.getvalue()

checker = lambda x, y, s: (230, 120, 40) if ((x // 8) + (y // 8)) % 2 else (40, 90, 200)
# glTF: occlusion R, roughness G, metallic B
orm = lambda x, y, s: (int(255 * (0.5 + 0.5 * y / s)), int(255 * x / s), 255 if y < s // 2 else 0)
normal = lambda x, y, s: (128 + (40 if (x // 16) % 2 else -40), 128, 255)

positions, normals, uvs, indices = [], [], [], []
faces = [((1,0,0),(0,1,0),(0,0,1)), ((-1,0,0),(0,1,0),(0,0,-1)), ((0,1,0),(0,0,1),(1,0,0)),
         ((0,-1,0),(0,0,-1),(1,0,0)), ((0,0,1),(0,1,0),(-1,0,0)), ((0,0,-1),(0,1,0),(1,0,0))]
for n, up, right in faces:
    base = len(positions)
    for u, v in ((0,0),(1,0),(1,1),(0,1)):
        p = [0.5*n[i] + (u-0.5)*right[i] + (v-0.5)*up[i] for i in range(3)]
        positions.append(p); normals.append(list(n)); uvs.append([u, 1 - v])
    # wind counter-clockwise when seen from outside
    cross = [right[1]*up[2]-right[2]*up[1], right[2]*up[0]-right[0]*up[2], right[0]*up[1]-right[1]*up[0]]
    ccw = sum(cross[i]*n[i] for i in range(3)) > 0
    indices += [base, base+1, base+2, base, base+2, base+3] if ccw else [base, base+2, base+1, base, base+3, base+2]

blob = bytearray(); views = []
def add(data, target=None):
    while len(blob) % 4: blob.append(0)
    view = {"buffer": 0, "byteOffset": len(blob), "byteLength": len(data)}
    if target: view["target"] = target
    blob.extend(data); views.append(view); return len(views) - 1

f32 = lambda rows: b"".join(struct.pack("<3f" if len(r) == 3 else "<2f", *r) for r in rows)
vp = add(f32(positions), 34962); vn = add(f32(normals), 34962); vt = add(f32(uvs), 34962)
vi = add(struct.pack(f"<{len(indices)}H", *indices), 34963)
images = [add(png(checker)), add(png(orm)), add(png(normal))]

gltf = {
  "asset": {"version": "2.0", "generator": "show_case e2e fixture"},
  "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0, "name": "Cube"}],
  "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}, "indices": 3, "material": 0}]}],
  "materials": [{"name": "Checker", "pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicRoughnessTexture": {"index": 1}},
                 "normalTexture": {"index": 2}, "occlusionTexture": {"index": 1}}],
  "textures": [{"source": 0, "sampler": 0}, {"source": 1, "sampler": 0}, {"source": 2, "sampler": 0}],
  "samplers": [{"magFilter": 9729, "minFilter": 9987}],
  "images": [{"bufferView": v, "mimeType": "image/png"} for v in images],
  "accessors": [
    {"bufferView": vp, "componentType": 5126, "count": len(positions), "type": "VEC3", "min": [-0.5]*3, "max": [0.5]*3},
    {"bufferView": vn, "componentType": 5126, "count": len(normals), "type": "VEC3"},
    {"bufferView": vt, "componentType": 5126, "count": len(uvs), "type": "VEC2"},
    {"bufferView": vi, "componentType": 5123, "count": len(indices), "type": "SCALAR"}],
  "bufferViews": views, "buffers": [{"byteLength": 0}],
}
while len(blob) % 4: blob.append(0)
gltf["buffers"][0]["byteLength"] = len(blob)
js = json.dumps(gltf, separators=(",", ":")).encode()
js += b" " * (-len(js) % 4)
out = struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(js) + 8 + len(blob))
out += struct.pack("<I4s", len(js), b"JSON") + js + struct.pack("<I4s", len(blob), b"BIN\0") + bytes(blob)
open("cube.glb", "wb").write(out)
print(f"cube.glb: {len(out)} bytes, {len(indices)//3} triangles")
