"""Convert an .fbx to .glb with Blender running headless (used by the Worker's processing job).

    blender -b --factory-startup --python-exit-code 3 --python fbx_to_glb.py -- input.fbx output.glb

Prints one line starting with REPORT followed by JSON (meshes, triangles, materials, missing textures)
and exits with code 2 and a readable reason when the file cannot be used.
"""

import json
import os
import sys

import bpy


def fail(reason):
    print("REPORT " + json.dumps({"error": reason}))
    sys.exit(2)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(args) != 2:
        fail("usage: -- input.fbx output.glb")
    src, dst = (os.path.abspath(a) for a in args)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=src, use_image_search=True)
    except Exception as e:  # Blender raises RuntimeError with the importer's message
        fail(f"FBX import failed: {e}")

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        fail("The FBX contains no mesh.")
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)

    missing = sorted(
        img.name for img in bpy.data.images
        if img.type == "IMAGE" and not (img.has_data or img.packed_file or os.path.isfile(bpy.path.abspath(img.filepath)))
    )

    bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB", export_image_format="AUTO", export_apply=True, export_yup=True)
    print("REPORT " + json.dumps({
        "meshes": len(meshes),
        "triangles": triangles,
        "materials": len(bpy.data.materials),
        "missingTextures": missing,
    }))


if __name__ == "__main__":
    main()
