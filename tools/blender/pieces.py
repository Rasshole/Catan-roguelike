#!/usr/bin/env python3
"""Colonist player pieces for top-down TableCamera (Y-up in Unity via FBX).

Roof facets lie in horizontal planes so pitch/yaw 90°/90° shows lit vs shaded halves + ridge.
"""
from __future__ import annotations

import json
import math
import os

import bpy
from mathutils import Vector

ROOT = os.environ.get("BLENDER_TILE_REPO_ROOT", os.getcwd())
OUT_DIR = os.path.join(ROOT, "Assets/_Project/Resources/Art/Models/ColonistPieces")
PREVIEW_PATH = os.path.join(ROOT, "docs/art/pieces_topdown.png")
BOUNDS_JSON = os.path.join(ROOT, "docs/art/piece_bounds.json")
CAMERA_YAW_DEG = 90.0


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def link_root(name: str) -> bpy.types.Object:
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    return root


def box_mesh(name: str, sx: float, sy: float, sz: float, loc: tuple[float, float, float]) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (sx * 0.5, sy * 0.5, sz * 0.5)
    bpy.ops.object.transform_apply(scale=True)
    return obj


def set_vertex_colors(obj: bpy.types.Object, rgba: tuple[float, float, float, float]) -> None:
    mesh = obj.data
    if not mesh.vertex_colors:
        mesh.vertex_colors.new(name="Col")
    layer = mesh.vertex_colors.active
    col = rgba
    for poly in mesh.polygons:
        for loop_idx in poly.loop_indices:
            layer.data[loop_idx].color = col


def build_settlement() -> bpy.types.Object:
    clear_scene()
    root = link_root("settlement")
    body_h = 0.20
    body_w = 0.36
    body_d = 0.32
    body = box_mesh("Body", body_w, body_d, body_h, (0, 0, body_h * 0.5))
    z_roof = body_h + 0.025
    # Horizontal roof halves (visible from top-down camera)
    over = 1.22
    box_mesh("RoofLeft", body_w * over * 0.52, body_d * over, 0.05, (-body_w * 0.13, 0, z_roof))
    box_mesh("RoofRight", body_w * over * 0.52, body_d * over, 0.05, (body_w * 0.13, 0, z_roof + 0.012))
    box_mesh("RoofRidge", body_w * 0.14, body_d * over * 1.05, 0.07, (0, 0, z_roof + 0.055))
    for obj in bpy.data.objects:
        if obj != root and obj.parent is None:
            obj.parent = root
    set_vertex_colors(body, (0.55, 0.55, 0.55, 1))
    return root


def build_city() -> bpy.types.Object:
    clear_scene()
    root = link_root("city")
    wing_h = 0.22
    box_mesh("Body", 0.42, 0.28, wing_h, (-0.12, -0.06, wing_h * 0.5))
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.04, 0.12, wing_h * 0.42))
    wing = bpy.context.object
    wing.name = "BodyWing"
    wing.scale = (0.20 * 0.5, 0.22 * 0.5, wing_h * 0.75 * 0.5)
    bpy.ops.object.transform_apply(scale=True)
    wing.parent = bpy.data.objects["Body"]

    tower_h = 0.38
    tower_w = 0.22
    tower_d = 0.20
    z0 = wing_h
    box_mesh("UpperStorey", tower_w, tower_d, tower_h, (0.20, 0.18, z0 + tower_h * 0.5))
    z_roof = z0 + tower_h + 0.03
    box_mesh("RoofLeft", 0.40 * 0.52, 0.30 * 1.15, 0.05, (-0.10, 0, wing_h + 0.03))
    box_mesh("RoofRight", 0.40 * 0.52, 0.30 * 1.15, 0.05, (0.02, 0, wing_h + 0.04))
    box_mesh("RoofRidge", 0.12, 0.34, 0.07, (-0.04, 0, wing_h + 0.075))
    box_mesh("TowerRoofLeft", tower_w * 0.52, tower_d * 1.1, 0.05, (0.17, 0.14, z_roof))
    box_mesh("TowerRoofRight", tower_w * 0.52, tower_d * 1.1, 0.05, (0.23, 0.22, z_roof + 0.012))
    box_mesh("TowerRoofRidge", tower_w * 0.16, tower_d * 1.12, 0.08, (0.20, 0.18, z_roof + 0.06))
    for obj in bpy.data.objects:
        if obj != root and obj.parent is None:
            obj.parent = root
    return root


def build_road() -> bpy.types.Object:
    clear_scene()
    root = link_root("road")
    box_mesh("Plank", 0.14, 0.92, 0.05, (0, 0, 0.025))
    for obj in bpy.data.objects:
        if obj != root and obj.parent is None:
            obj.parent = root
    return root


def export_fbx(name: str, root: bpy.types.Object) -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, f"{name}.fbx")
    bpy.ops.object.select_all(action="DESELECT")

    def select_tree(o: bpy.types.Object) -> None:
        o.select_set(True)
        for ch in o.children:
            select_tree(ch)

    select_tree(root)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"EMPTY", "MESH"},
        bake_space_transform=True,
        mesh_smooth_type="FACE",
    )
    print(f"Wrote {path}")


def measure_bounds(root: bpy.types.Object) -> dict:
    out: dict[str, list[float]] = {}
    def walk(o: bpy.types.Object):
        yield o
        for c in o.children:
            yield from walk(c)

    for obj in walk(root):
        if obj.type != "MESH":
            continue
        vs = [obj.matrix_world @ v.co for v in obj.data.vertices]
        if not vs:
            continue
        xs = [v.x for v in vs]
        ys = [v.y for v in vs]
        zs = [v.z for v in vs]
        out[obj.name] = [min(xs), min(ys), min(zs), max(xs), max(ys), max(zs)]
    # whole piece
    meshes = [o for o in walk(root) if o.type == "MESH"]
    if meshes:
        all_v = []
        for obj in meshes:
            all_v.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
        xs = [v.x for v in all_v]
        ys = [v.y for v in all_v]
        zs = [v.z for v in all_v]
        out["_root"] = [min(xs), min(ys), min(zs), max(xs), max(ys), max(zs)]
    return out


def render_topdown_preview() -> None:
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 900
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.52, 0.68, 0.48, 1.0)

    s = build_settlement()
    s.location = (-0.55, 0, 0)
    c = build_city()
    c.location = (0.55, 0, 0)

    # Unity TableCamera: board XZ, Y up → Blender export uses Y-up; view from +Y (top-down)
    bpy.ops.object.camera_add(location=(0, 5.5, 0))
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.05
    cam.rotation_euler = (math.radians(90), 0, math.radians(CAMERA_YAW_DEG))
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(1.5, 3, 2))
    sun = bpy.context.object
    sun.data.energy = 2.2
    sun.rotation_euler = (math.radians(35), math.radians(20), math.radians(25))

    os.makedirs(os.path.dirname(PREVIEW_PATH), exist_ok=True)
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    print(f"Wrote {PREVIEW_PATH}")


def main() -> None:
    bounds_doc = {}
    for name, builder in (
        ("settlement", build_settlement),
        ("city", build_city),
        ("road", build_road),
    ):
        root = builder()
        export_fbx(name, root)
        bounds_doc[name] = measure_bounds(root)

    with open(BOUNDS_JSON, "w", encoding="utf-8") as f:
        json.dump(bounds_doc, f, indent=2)
    print(f"Wrote {BOUNDS_JSON}")
    render_topdown_preview()


if __name__ == "__main__":
    main()
