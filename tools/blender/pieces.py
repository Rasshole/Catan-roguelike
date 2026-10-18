#!/usr/bin/env python3
"""Export classic Catan-style player piece FBX for Unity (v115/v117).

  blender -b --python tools/blender/pieces.py
"""
from __future__ import annotations

import math
import os

import bpy

ROOT = os.environ.get("BLENDER_TILE_REPO_ROOT", os.getcwd())
OUT_DIR = os.path.join(ROOT, "Assets/_Project/Resources/Art/Models/ColonistPieces")


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def box(name: str, sx: float, sy: float, sz: float, loc: tuple[float, float, float]) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (sx * 0.5, sy * 0.5, sz * 0.5)
    bpy.ops.object.transform_apply(scale=True)
    return obj


def build_settlement() -> bpy.types.Object:
    clear_scene()
    root = bpy.data.objects.new("settlement", None)
    bpy.context.collection.objects.link(root)
    body_h = 0.22
    body_w = 0.34
    body_d = 0.30
    box("Body", body_w, body_d, body_h, (0, 0, body_h * 0.5))
    pitch = math.radians(38)
    roof_t = 0.05
    roof_d = body_d * 0.62
    lift = roof_d * math.sin(pitch) * 0.5
    z = body_h + lift
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -body_d * 0.24, z))
    rl = bpy.context.object
    rl.name = "RoofLeft"
    rl.scale = (body_w * 0.59, roof_d * 0.5, roof_t * 0.5)
    rl.rotation_euler = (-pitch, 0, 0)
    bpy.ops.object.transform_apply(scale=True, rotation=True)

    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, body_d * 0.24, z))
    rr = bpy.context.object
    rr.name = "RoofRight"
    rr.scale = (body_w * 0.59, roof_d * 0.5, roof_t * 0.5)
    rr.rotation_euler = (pitch, 0, 0)
    bpy.ops.object.transform_apply(scale=True, rotation=True)

    ridge_y = body_h + 0.04
    box("RoofRidge", body_w * 0.62, body_d * 1.05, 0.08, (0, 0, ridge_y + 0.04))
    for obj in bpy.data.objects:
        if obj.name != "settlement":
            obj.parent = root
    return root


def build_city() -> bpy.types.Object:
    clear_scene()
    root = bpy.data.objects.new("city", None)
    bpy.context.collection.objects.link(root)
    body_h = 0.24
    body_w = 0.38
    body_d = 0.34
    box("Body", body_w, body_d, body_h, (0, 0, body_h * 0.5))
    upper_h = 0.20
    upper_w = 0.28
    upper_d = 0.26
    z_upper = body_h + upper_h * 0.5
    box("UpperStorey", upper_w, upper_d, upper_h, (0, 0, z_upper))
    pitch = math.radians(38)
    roof_t = 0.05
    roof_d = upper_d * 0.62
    lift = roof_d * math.sin(pitch) * 0.5
    z = body_h + upper_h + lift
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -upper_d * 0.24, z))
    rl = bpy.context.object
    rl.name = "RoofLeft"
    rl.scale = (upper_w * 0.59, roof_d * 0.5, roof_t * 0.5)
    rl.rotation_euler = (-pitch, 0, 0)
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, upper_d * 0.24, z))
    rr = bpy.context.object
    rr.name = "RoofRight"
    rr.scale = (upper_w * 0.59, roof_d * 0.5, roof_t * 0.5)
    rr.rotation_euler = (pitch, 0, 0)
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    ridge_y = body_h + upper_h + 0.05
    box("RoofRidge", upper_w * 0.65, upper_d * 1.08, 0.09, (0, 0, ridge_y + 0.045))
    for obj in bpy.data.objects:
        if obj.name != "city":
            obj.parent = root
    return root


def build_road() -> bpy.types.Object:
    clear_scene()
    root = bpy.data.objects.new("road", None)
    bpy.context.collection.objects.link(root)
    # 1 unit length along Z, 0.10 width (Unity scales length per edge)
    box("Plank", 0.10, 0.92, 0.045, (0, 0, 0.0225))
    for obj in bpy.data.objects:
        if obj.name != "road":
            obj.parent = root
    return root


def export_fbx(name: str, root: bpy.types.Object) -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, f"{name}.fbx")
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children:
        child.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"EMPTY", "MESH"},
        bake_space_transform=True,
    )
    print(f"Wrote {path}")


def main() -> None:
    export_fbx("settlement", build_settlement())
    export_fbx("city", build_city())
    export_fbx("road", build_road())


if __name__ == "__main__":
    main()
