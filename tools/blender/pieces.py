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
HEX_SCALE = 1.2


def flat_hex_width(hex_scale: float = HEX_SCALE) -> float:
    return hex_scale * math.sqrt(3.0)


def settlement_width(hex_scale: float = HEX_SCALE) -> float:
    return flat_hex_width(hex_scale) * 0.20


def city_span(hex_scale: float = HEX_SCALE) -> float:
    return flat_hex_width(hex_scale) * 0.30


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


def populate_settlement(root: bpy.types.Object, hex_scale: float = HEX_SCALE) -> None:
    body_w = settlement_width(hex_scale)
    body_d = body_w * 0.94
    body_nominal = body_w * 0.54
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


def build_settlement() -> bpy.types.Object:
    clear_scene()
    root = link_root("settlement")
    populate_settlement(root)
    return root


def populate_city(root: bpy.types.Object, hex_scale: float = HEX_SCALE) -> None:
    gap = 0.02
    hex_top = 0.0
    base_y = hex_top + gap
    span = city_span(hex_scale)
    wing_w = span * 0.58
    wing_d = span * 0.52
    wing_nominal = span * 0.38
    wing_h = wing_nominal - 0.06
    tower_w = span * 0.40
    tower_d = span * 0.44
    tower_nominal = wing_nominal * 1.72
    tower_h = tower_nominal - 0.06
    wing_ox = -span * 0.14
    wing_oz = -span * 0.10
    tower_ox = span * 0.20
    tower_oz = span * 0.16

    wing = box_mesh(
        "Body",
        wing_w,
        wing_h,
        wing_d,
        (wing_ox, base_y + wing_h * 0.5, wing_oz),
    )
    assign_mat(wing, mat_rgb("Body", (0.55, 0.55, 0.55)))
    tower = box_mesh(
        "UpperStorey",
        tower_w,
        tower_h,
        tower_d,
        (tower_ox, base_y + wing_h + tower_h * 0.5, tower_oz),
    )
    assign_mat(tower, mat_rgb("UpperStorey", (0.5, 0.5, 0.5)))
    wing_roof_y = base_y + wing_nominal
    add_colonist_roof(root, wing_w, wing_d, wing_roof_y, AI_RGB, wing_ox, wing_oz)
    tower_roof_y = base_y + wing_nominal + tower_nominal
    add_colonist_roof(
        root,
        tower_w,
        tower_d,
        tower_roof_y,
        AI_RGB,
        tower_ox,
        tower_oz,
        left_name="TowerRoofLeft",
        right_name="TowerRoofRight",
        ridge_name="TowerRoofRidge",
    )
    wing.parent = root
    tower.parent = root
    for obj in bpy.data.objects:
        if obj != root and obj.parent is None:
            obj.parent = root


def build_city() -> bpy.types.Object:
    clear_scene()
    root = link_root("city")
    populate_city(root)
    return root


def build_road() -> bpy.types.Object:
    clear_scene()
    root = link_root("road")
    flat = flat_hex_width()
    w = flat * 0.068
    h = flat * 0.024
    d = 0.92
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
    """Orthographic top-down on board XZ (Unity Y-up pieces rotated for Blender preview)."""
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 1.15
    cam.location = (0, 0, 4.2)
    cam.rotation_euler = (0, 0, 0)


def assert_preview_has_content(path: str) -> None:
    try:
        from PIL import Image
    except ImportError:
        return
    im = Image.open(path).convert("RGB")
    extrema = im.getextrema()
    if extrema[0][0] == extrema[0][1] and extrema[1][0] == extrema[1][1]:
        raise RuntimeError(f"Preview {path} is blank (no channel variance)")


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

    s = link_root("settlement_preview")
    populate_settlement(s)
    s.location = (-0.38, 0, 0)
    s.rotation_euler = (math.radians(90), 0, 0)
    c = link_root("city_preview")
    populate_city(c)
    c.location = (0.38, 0, 0)
    c.rotation_euler = (math.radians(90), 0, 0)

    bpy.ops.object.camera_add(location=(0, 0, 4.2))
    cam = bpy.context.object
    apply_table_camera(cam)
    cam.data.ortho_scale = 1.75
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
    try:
        from PIL import Image

        im = Image.open(PREVIEW_PATH).convert("RGB")
        px = im.getdata()
        rs = gs = bs = 0.0
        n = 0
        for r, g, b in px:
            rs += r
            gs += g
            bs += b
            n += 1
        mean = (rs / n, gs / n, bs / n)
        bg = (0.52 * 255, 0.68 * 255, 0.48 * 255)
        if sum(abs(mean[i] - bg[i]) for i in range(3)) < 12:
            raise RuntimeError(f"Preview {PREVIEW_PATH} matches empty background (mean={mean})")
    except ImportError:
        assert_preview_has_content(PREVIEW_PATH)
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
