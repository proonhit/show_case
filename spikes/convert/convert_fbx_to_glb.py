"""Convert an .fbx file to .glb with Blender running headless, and report what survived.

Usage:
    blender -b --factory-startup --python convert_fbx_to_glb.py -- input.fbx output.glb

The report printed at the end is the point of this spike: it shows whether the
textures and PBR inputs (base color, metallic, roughness, normal, AO) came through
the FBX import, so we can decide how .fbx files must be exported from Blender.
"""

import os
import sys

import bpy


def parse_args():
    if "--" not in sys.argv:
        sys.exit("Usage: blender -b --python convert_fbx_to_glb.py -- input.fbx output.glb")
    args = sys.argv[sys.argv.index("--") + 1:]
    if len(args) != 2:
        sys.exit("Expected exactly 2 arguments: input.fbx output.glb")
    src, dst = (os.path.abspath(a) for a in args)
    if not os.path.isfile(src):
        sys.exit(f"Input not found: {src}")
    return src, dst


def linked_input(node, name):
    sock = node.inputs.get(name)
    return sock is not None and sock.is_linked


def report_materials():
    print("\n=== Material report ===")
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            print(f"- {mat.name}: no node tree")
            continue
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None:
            print(f"- {mat.name}: no Principled BSDF")
            continue
        checks = {
            "base color": linked_input(bsdf, "Base Color"),
            "metallic": linked_input(bsdf, "Metallic"),
            "roughness": linked_input(bsdf, "Roughness"),
            "normal": linked_input(bsdf, "Normal"),
        }
        flags = ", ".join(f"{k}={'yes' if v else 'NO'}" for k, v in checks.items())
        print(f"- {mat.name}: {flags}")

    print("\n=== Images ===")
    for img in bpy.data.images:
        if img.type != "IMAGE":
            continue
        has_pixels = img.has_data or img.packed_file is not None or os.path.isfile(bpy.path.abspath(img.filepath))
        state = "ok" if has_pixels else "MISSING (texture not embedded / path not found)"
        print(f"- {img.name} {tuple(img.size)}: {state}")


def main():
    src, dst = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)

    bpy.ops.import_scene.fbx(filepath=src, use_image_search=True)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    triangles = 0
    for obj in meshes:
        mesh = obj.data
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
    print(f"\nImported {len(meshes)} meshes, ~{triangles:,} triangles")

    report_materials()

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=dst,
        export_format="GLB",
        export_image_format="AUTO",
        export_apply=True,
        export_yup=True,
    )
    size_mb = os.path.getsize(dst) / (1024 * 1024)
    print(f"\nWrote {dst} ({size_mb:.1f} MB)")


if __name__ == "__main__":
    main()
