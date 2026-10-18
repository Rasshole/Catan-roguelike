#!/usr/bin/env python3
"""Colonist player pieces — Unity Y-up (board XZ), TableCamera ortho pitch 90° yaw 90°."""
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
# ColonistBoardViewConstants.CameraYawDegrees
CAMERA_YAW_DEG = 90.0
HUMAN_RGB = (0.2, 0.4, 0.9)
AI_RGB = (0.9, 0.25, 0.2)
ROOF_SHADE = 0.55


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def link_root(name: str) -> bpy.types.Object:
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    return root


def box_mesh(
    name: str,
    width: float,
    height: float,
    depth: float,
    loc: tuple[float, float, float],
) -> bpy.types.Object:
    """Unity-style box: width=X, height=Y, depth=Z."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (width * 0.5, height * 0.5, depth * 0.5)
    bpy.ops.object.transform_apply(scale=True)
    return obj


def mat_rgb(name: str, rgb: tuple[float, float, float], rough: float = 0.75) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    return m


def assign_mat(obj: bpy.types.Object, mat: bpy.types.Material) -> None:
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)


def add_colonist_roof(
    parent: bpy.types.Object,
    body_w: float,
    body_d: float,
    roof_y: float,
    player_rgb: tuple[float, float, float],
    offset_x: float = 0.0,
    offset_z: float = 0.0,
    left_name: str = "RoofLeft",
    right_name: str = "RoofRight",
    ridge_name: str = "RoofRidge",
) -> None:
    lit = mat_rgb(left_name, player_rgb, 0.82)
    shade = mat_rgb(right_name, tuple(c * ROOF_SHADE for c in player_rgb), 0.85)
    ridge = mat_rgb(ridge_name, tuple(c * 0.38 for c in player_rgb), 0.9)
    slab = 0.048
    half_span = body_w * 0.54
    depth = body_d * 1.22
    left = box_mesh(
        left_name,
        half_span,
        slab,
        depth,
        (offset_x - half_span * 0.5, roof_y, offset_z),
    )
    right = box_mesh(
        right_name,
        half_span,
        slab,
        depth,
        (offset_x + half_span * 0.5, roof_y + slab * 0.2, offset_z),
    )
    ridge_o = box_mesh(
        ridge_name,
        body_w * 0.12,
        slab * 1.4,
        depth * 1.04,
        (offset_x, roof_y + slab * 0.65, offset_z),
    )
    assign_mat(left, lit)
    assign_mat(right, shade)
    assign_mat(ridge_o, ridge)
    for o in (left, right, ridge_o):
        o.parent = parent


def build_settlement() -> bpy.types.Object:
    clear_scene()
    root = link_root("settlement")
    body_w, body_d = 0.32, 0.30
    body_nominal = 0.22
    gap = 0.02
    body_h = body_nominal - 0.06
    hex_top = 0.0
    base_y = hex_top + gap
    body = box_mesh("Body", body_w, body_h, body_d, (0, base_y + body_h * 0.5, 0))
    assign_mat(body, mat_rgb("Body", (0.55, 0.55, 0.55)))
    roof_y = hex_top + gap + body_nominal
    add_colonist_roof(root, body_w, body_d, roof_y, HUMAN_RGB)
    body.parent = root
    for obj in bpy.data.objects:
        if obj != root and obj.parent is None:
            obj.parent = root
    return root


def build_city() -> bpy.types.Object:
    clear_scene()
    root = link_root("city")
    gap = 0.02
    hex_top = 0.0
    base_y = hex_top + gap
    wing_w, wing_d = 0.36, 0.30
    wing_nominal = 0.24
    wing_h = wing_nominal - 0.06
    tower_w, tower_d = 0.22, 0.20
    tower_nominal = 0.44
    tower_h = tower_nominal - 0.06

    wing = box_mesh(
        "Body",
        wing_w,
        wing_h,
        wing_d,
        (-0.06, base_y + wing_h * 0.5, -0.04),
    )
    assign_mat(wing, mat_rgb("Body", (0.55, 0.55, 0.55)))
    tower = box_mesh(
        "UpperStorey",
        tower_w,
        tower_h,
        tower_d,
        (0.14, base_y + wing_h + tower_h * 0.5, 0.12),
    )
    assign_mat(tower, mat_rgb("UpperStorey", (0.5, 0.5, 0.5)))
    wing_roof_y = base_y + wing_nominal
    add_colonist_roof(root, wing_w, wing_d, wing_roof_y, AI_RGB, -0.06, -0.04)
    tower_roof_y = base_y + wing_nominal + tower_nominal
    add_colonist_roof(
        root,
        tower_w,
        tower_d,
        tower_roof_y,
        AI_RGB,
        0.14,
        0.12,
        left_name="TowerRoofLeft",
        right_name="TowerRoofRight",
        ridge_name="TowerRoofRidge",
    )
    wing.parent = root
    tower.parent = root
    for obj in bpy.data.objects:
        if obj != root and obj.parent is None:
            obj.parent = root
    return root


def build_road() -> bpy.types.Object:
    clear_scene()
    root = link_root("road")
    w, h, d = 0.14, 0.048, 0.92
    center_y = h * 0.5 + 0.028
    plank = box_mesh("Plank", w, h, d, (0, center_y, 0))
    assign_mat(plank, mat_rgb("Plank", HUMAN_RGB))
    plank.parent = root
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


def apply_table_camera(cam: bpy.types.Object) -> None:
    """Match TableCamera.ApplyColonistTopDownPose: ortho, euler (90, 90, 0)."""
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.05
    cam.rotation_euler = (math.radians(90), math.radians(CAMERA_YAW_DEG), 0)
    cam.location = (0, 5.5, 0)


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

    bpy.ops.object.camera_add(location=(0, 5.5, 0))
    cam = bpy.context.object
    apply_table_camera(cam)
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(2, 4, 3))
    sun = bpy.context.object
    sun.data.energy = 1.8
    sun.rotation_euler = (math.radians(50), math.radians(25), math.radians(CAMERA_YAW_DEG))

    bpy.ops.object.light_add(type="AREA", location=(-2, 3.5, -1))
    fill = bpy.context.object
    fill.data.energy = 85
    fill.data.size = 4
    fill.rotation_euler = (math.radians(70), 0, math.radians(-20))

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
