#!/usr/bin/env python3
"""Colonist player pieces + top-down preview (v118).

  blender -b --python tools/blender/pieces.py
"""
from __future__ import annotations

import math
import os

import bpy

ROOT = os.environ.get("BLENDER_TILE_REPO_ROOT", os.getcwd())
OUT_DIR = os.path.join(ROOT, "Assets/_Project/Resources/Art/Models/ColonistPieces")
PREVIEW_PATH = os.path.join(ROOT, "docs/art/pieces_topdown.png")

# Matches TableCamera colonist pose: Quaternion.Euler(90, CameraYawDegrees, 0), yaw=90°
CAMERA_YAW_DEG = 90.0


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


def add_pitched_roof(
    name_l: str,
    name_r: str,
    name_ridge: str,
    center_y: float,
    width: float,
    depth: float,
    pitch_deg: float = 42.0,
) -> None:
    pitch = math.radians(pitch_deg)
    roof_t = 0.06
    panel_depth = depth * 0.78
    lift = panel_depth * math.sin(pitch) * 0.5
    z = center_y + lift
    overhang = 1.28
    panel_w = width * overhang
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -depth * 0.28, z))
    rl = bpy.context.object
    rl.name = name_l
    rl.scale = (panel_w * 0.5, panel_depth * 0.5, roof_t * 0.5)
    rl.rotation_euler = (-pitch, 0, 0)
    bpy.ops.object.transform_apply(scale=True, rotation=True)

    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, depth * 0.28, z))
    rr = bpy.context.object
    rr.name = name_r
    rr.scale = (panel_w * 0.5, panel_depth * 0.5, roof_t * 0.5)
    rr.rotation_euler = (pitch, 0, 0)
    bpy.ops.object.transform_apply(scale=True, rotation=True)

    ridge_z = center_y + 0.06
    box(name_ridge, panel_w * 0.92, depth * 1.22, 0.10, (0, 0, ridge_z + 0.05))


def build_settlement() -> bpy.types.Object:
    clear_scene()
    root = bpy.data.objects.new("settlement", None)
    bpy.context.collection.objects.link(root)
    body_h = 0.26
    body_w = 0.36
    body_d = 0.32
    box("Body", body_w, body_d, body_h, (0, 0, body_h * 0.5))
    add_pitched_roof("RoofLeft", "RoofRight", "RoofRidge", body_h, body_w, body_d)
    for obj in bpy.data.objects:
        if obj.name != "settlement":
            obj.parent = root
    return root


def build_city() -> bpy.types.Object:
    clear_scene()
    root = bpy.data.objects.new("city", None)
    bpy.context.collection.objects.link(root)
    # L-shaped base: main wing + short wing
    wing_h = 0.22
    body = box("Body", 0.40, 0.26, wing_h, (-0.10, -0.08, wing_h * 0.5))
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.06, 0.10, wing_h * 0.45))
    wing = bpy.context.object
    wing.name = "BodyWing"
    wing.scale = (0.18 * 0.5, 0.20 * 0.5, wing_h * 0.85 * 0.5)
    bpy.ops.object.transform_apply(scale=True)
    wing.parent = body

    tower_h = 0.34
    tower_w = 0.20
    tower_d = 0.18
    z_tower = wing_h + tower_h * 0.5
    box("UpperStorey", tower_w, tower_d, tower_h, (0.18, 0.16, z_tower))
    add_pitched_roof("RoofLeft", "RoofRight", "RoofRidge", wing_h, 0.38, 0.30, pitch_deg=40.0)
    add_pitched_roof("TowerRoofLeft", "TowerRoofRight", "TowerRoofRidge", wing_h + tower_h, tower_w, tower_d, pitch_deg=44.0)
    for obj in bpy.data.objects:
        if obj.name != "city":
            obj.parent = root
    return root


def build_road() -> bpy.types.Object:
    clear_scene()
    root = bpy.data.objects.new("road", None)
    bpy.context.collection.objects.link(root)
    box("Plank", 0.14, 0.92, 0.05, (0, 0, 0.025))
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
        for sub in child.children:
            sub.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"EMPTY", "MESH"},
        bake_space_transform=True,
    )
    print(f"Wrote {path}")


def _mat_color(name: str, rgb: tuple[float, float, float]) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.85
    return m


def render_topdown_preview() -> None:
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 768
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.72, 0.55, 1.0)

    mat_blue = _mat_color("Blue", (0.2, 0.4, 0.9))
    mat_red = _mat_color("Red", (0.9, 0.25, 0.2))
    mat_roof_d = _mat_color("RoofD", (0.12, 0.22, 0.5))
    mat_roof_l = _mat_color("RoofL", (0.28, 0.45, 0.85))

    def walk(obj: bpy.types.Object):
        yield obj
        for ch in obj.children:
            yield from walk(ch)

    def tint_piece(root: bpy.types.Object, body_mat, roof_lit, roof_shade) -> None:
        for obj in walk(root):
            if obj.type != "MESH":
                continue
            n = obj.name
            if "Ridge" in n:
                obj.data.materials.append(mat_roof_d)
            elif "Left" in n and "Roof" in n:
                obj.data.materials.append(roof_lit)
            elif "Right" in n and "Roof" in n:
                obj.data.materials.append(roof_shade)
            elif "Roof" in n or "Tower" in n:
                obj.data.materials.append(roof_shade)
            else:
                obj.data.materials.append(body_mat)

    s = build_settlement()
    s.location = (-0.55, 0, 0)
    tint_piece(s, mat_blue, mat_blue, mat_roof_d)

    c = build_city()
    c.location = (0.55, 0, 0)
    tint_piece(c, mat_red, mat_red, mat_roof_d)

    # Unity TableCamera: X=90°, Y=90° → Blender scene rotated to match board XZ plane
    for obj in bpy.data.objects:
        if obj.type == "MESH" or obj.name in ("settlement", "city"):
            obj.rotation_euler = (math.radians(90), 0, math.radians(CAMERA_YAW_DEG))

    bpy.ops.object.camera_add(location=(0, 0, 6))
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.4
    cam.rotation_euler = (0, 0, 0)
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(2, -2, 8))
    sun = bpy.context.object
    sun.data.energy = 2.5
    sun.rotation_euler = (math.radians(50), math.radians(15), math.radians(25))

    os.makedirs(os.path.dirname(PREVIEW_PATH), exist_ok=True)
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    print(f"Wrote {PREVIEW_PATH}")


def main() -> None:
    export_fbx("settlement", build_settlement())
    export_fbx("city", build_city())
    export_fbx("road", build_road())
    render_topdown_preview()


if __name__ == "__main__":
    main()
