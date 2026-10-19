#!/usr/bin/env python3
"""Colonist player pieces — watertight meshes + board sprites (Unity Y-up, board XZ)."""
from __future__ import annotations

import json
import math
import os

import bpy
import bmesh
from mathutils import Matrix, Vector

ROOT = os.environ.get("BLENDER_TILE_REPO_ROOT", os.getcwd())
OUT_DIR = os.path.join(ROOT, "Assets/_Project/Resources/Art/Models/ColonistPieces")
SPRITE_DIR = os.path.join(ROOT, "Assets/_Project/Resources/Art/Board/PlayerPieceSprites")
PREVIEW_PATH = os.path.join(ROOT, "docs/art/pieces_topdown.png")
PREVIEW_34_PATH = os.path.join(ROOT, "docs/art/pieces_three_quarter.png")
BOUNDS_JSON = os.path.join(ROOT, "docs/art/piece_bounds.json")
CAMERA_YAW_DEG = 90.0
HUMAN_RGB = (0.2, 0.4, 0.9)
AI_RGB = (0.9, 0.25, 0.2)
ROOF_SHADE = 0.52
EDGE_SHADE = 0.72
HEX_SCALE = 1.2
BASE_GAP = 0.02


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


def mat_rgb(name: str, rgb: tuple[float, float, float], rough: float = 0.75) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    return m


def join_meshes(name: str, parts: list[bpy.types.Object]) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_smooth()
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.remove_doubles(threshold=0.0008)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def assign_material_slots(obj: bpy.types.Object, body_mat: bpy.types.Material, roof_mat: bpy.types.Material) -> None:
    mesh = obj.data
    mesh.materials.clear()
    mesh.materials.append(body_mat)
    mesh.materials.append(roof_mat)
    roof_y_min = None
    for poly in mesh.polygons:
        c = Vector((0.0, 0.0, 0.0))
        for vi in poly.vertices:
            c += mesh.vertices[vi].co
        c /= len(poly.vertices)
        if roof_y_min is None or c.y > roof_y_min:
            roof_y_min = c.y
    if roof_y_min is None:
        return
    eave = roof_y_min - 0.02
    for poly in mesh.polygons:
        c = Vector((0.0, 0.0, 0.0))
        for vi in poly.vertices:
            c += mesh.vertices[vi].co
        c /= len(poly.vertices)
        poly.material_index = 1 if c.y >= eave else 0


def box_part(
    name: str,
    width: float,
    height: float,
    depth: float,
    loc: tuple[float, float, float],
    rot: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (width * 0.5, height * 0.5, depth * 0.5)
    obj.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


def bevel_object(obj: bpy.types.Object, width: float = 0.012, segments: int = 2) -> None:
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def build_settlement_mesh(player_rgb: tuple[float, float, float], hex_scale: float = HEX_SCALE) -> bpy.types.Object:
    w = settlement_width(hex_scale)
    d = w * 0.92
    wall_h = w * 0.36
    roof_rise = w * 0.22
    base_y = BASE_GAP
    wall = box_part("Wall", w, wall_h, d, (0.0, base_y + wall_h * 0.5, 0.0))
    eave_y = base_y + wall_h
    half_w = w * 0.52
    roof_d = d * 1.04
    roof_th = w * 0.06
    pitch = math.radians(32.0)
    left = box_part(
        "RoofL",
        half_w,
        roof_th,
        roof_d,
        (0.0, eave_y + roof_rise * 0.42, 0.0),
        (pitch, 0.0, 0.0),
    )
    left.location.x = -half_w * 0.25
    right = box_part(
        "RoofR",
        half_w,
        roof_th,
        roof_d,
        (0.0, eave_y + roof_rise * 0.42, 0.0),
        (-pitch, 0.0, 0.0),
    )
    right.location.x = half_w * 0.25
    gable = box_part("Gable", w * 0.14, roof_rise * 0.85, roof_d * 0.98, (0.0, eave_y + roof_rise * 0.72, 0.0))
    parts = [wall, left, right, gable]
    obj = join_meshes("SettlementMesh", parts)
    body = mat_rgb("Body", tuple(c * EDGE_SHADE for c in player_rgb))
    roof = mat_rgb("Roof", tuple(c * ROOF_SHADE for c in player_rgb))
    assign_material_slots(obj, body, roof)
    return obj


def build_city_mesh(player_rgb: tuple[float, float, float], hex_scale: float = HEX_SCALE) -> bpy.types.Object:
    span = city_span(hex_scale)
    base_y = BASE_GAP
    wing_w = span * 0.62
    wing_d = span * 0.48
    wing_h = span * 0.26
    tower_w = span * 0.30
    tower_d = span * 0.28
    tower_h = span * 0.68
    wing_ox = -span * 0.18
    wing_oz = -span * 0.08
    tower_ox = span * 0.22
    tower_oz = span * 0.12
    wing = box_part("Wing", wing_w, wing_h, wing_d, (wing_ox, base_y + wing_h * 0.5, wing_oz))
    tower = box_part(
        "Tower",
        tower_w,
        tower_h,
        tower_d,
        (tower_ox, base_y + tower_h * 0.5, tower_oz),
    )
    pitch = math.radians(30.0)
    wing_roof = box_part(
        "WingRoof",
        wing_w * 0.96,
        span * 0.05,
        wing_d * 1.02,
        (wing_ox, base_y + wing_h + span * 0.03, wing_oz),
        (pitch, 0.0, math.radians(8)),
    )
    tower_cap = box_part(
        "TowerCap",
        tower_w * 1.05,
        span * 0.06,
        tower_d * 1.05,
        (tower_ox, base_y + tower_h + span * 0.03, tower_oz),
    )
    obj = join_meshes("CityMesh", [wing, tower, wing_roof, tower_cap])
    body = mat_rgb("CityBody", tuple(c * EDGE_SHADE for c in player_rgb))
    roof = mat_rgb("CityRoof", tuple(c * ROOF_SHADE for c in player_rgb))
    assign_material_slots(obj, body, roof)
    return obj


def build_road_mesh(player_rgb: tuple[float, float, float], hex_scale: float = HEX_SCALE) -> bpy.types.Object:
    flat = flat_hex_width(hex_scale)
    w = flat * 0.068
    h = flat * 0.024
    length = 0.92
    center_y = h * 0.5 + BASE_GAP + 0.008
    plank = box_part("Plank", w, h, length, (0.0, center_y, 0.0))
    bevel_object(plank, width=w * 0.18, segments=3)
    mat = mat_rgb("Road", player_rgb, 0.78)
    plank.data.materials.append(mat)
    return plank


def mesh_to_root(name: str, mesh_obj: bpy.types.Object) -> bpy.types.Object:
    root = link_root(name)
    mesh_obj.parent = root
    return root


def build_settlement(player_rgb: tuple[float, float, float] = HUMAN_RGB) -> bpy.types.Object:
    clear_scene()
    mobj = build_settlement_mesh(player_rgb)
    return mesh_to_root("settlement", mobj)


def build_city(player_rgb: tuple[float, float, float] = AI_RGB) -> bpy.types.Object:
    clear_scene()
    mobj = build_city_mesh(player_rgb)
    return mesh_to_root("city", mobj)


def build_road(player_rgb: tuple[float, float, float] = HUMAN_RGB) -> bpy.types.Object:
    clear_scene()
    mobj = build_road_mesh(player_rgb)
    return mesh_to_root("road", mobj)


def export_fbx(name: str, root: bpy.types.Object) -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, f"{name}.fbx")

    def select_tree(o: bpy.types.Object) -> None:
        o.select_set(True)
        for ch in o.children:
            select_tree(ch)

    bpy.ops.object.select_all(action="DESELECT")
    select_tree(root)
    bpy.context.view_layer.objects.active = root.children[0] if root.children else root
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

    meshes = [o for o in walk(root) if o.type == "MESH"]
    for obj in meshes:
        vs = [obj.matrix_world @ v.co for v in obj.data.vertices]
        if not vs:
            continue
        xs = [v.x for v in vs]
        ys = [v.y for v in vs]
        zs = [v.z for v in vs]
        out[obj.name] = [min(xs), min(ys), min(zs), max(xs), max(ys), max(zs)]
    if meshes:
        all_v = []
        for obj in meshes:
            all_v.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
        xs = [v.x for v in all_v]
        ys = [v.y for v in all_v]
        zs = [v.z for v in all_v]
        out["_root"] = [min(xs), min(ys), min(zs), max(xs), max(ys), max(zs)]
    return out


def setup_sprite_scene() -> bpy.types.Scene:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.world = bpy.data.worlds.new("SpriteWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0, 0, 0, 0)
    return scene


def render_sprite(mesh_builder, out_name: str, player_rgb: tuple[float, float, float]) -> None:
    clear_scene()
    scene = setup_sprite_scene()
    mobj = mesh_builder(player_rgb)
    bpy.ops.object.select_all(action="DESELECT")
    mobj.select_set(True)
    bpy.context.view_layer.objects.active = mobj

    bpy.ops.object.camera_add(location=(0.85, -1.05, 0.72))
    cam = bpy.context.object
    cam.data.type = "PERSP"
    cam.data.lens = 38
    cam.rotation_euler = (math.radians(58), 0.0, math.radians(42))
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(1.5, -1.2, 2.5))
    sun = bpy.context.object
    sun.data.energy = 2.8
    sun.rotation_euler = (math.radians(42), math.radians(12), math.radians(30))

    bpy.ops.object.light_add(type="AREA", location=(-1.2, 0.8, 1.2))
    fill = bpy.context.object
    fill.data.energy = 120
    fill.data.size = 3

    os.makedirs(SPRITE_DIR, exist_ok=True)
    path = os.path.join(SPRITE_DIR, f"{out_name}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print(f"Wrote {path}")


def apply_table_camera(cam: bpy.types.Object, ortho_scale: float = 1.15) -> None:
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho_scale
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

    s_mesh = build_settlement_mesh(HUMAN_RGB)
    s_mesh.location = (-0.38, 0, 0)
    s_mesh.rotation_euler = (math.radians(90), 0, 0)
    c_mesh = build_city_mesh(AI_RGB)
    c_mesh.location = (0.38, 0, 0)
    c_mesh.rotation_euler = (math.radians(90), 0, 0)

    bpy.ops.object.camera_add(location=(0, 0, 4.2))
    cam = bpy.context.object
    apply_table_camera(cam, ortho_scale=1.75)
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(2, 4, 3))
    sun = bpy.context.object
    sun.data.energy = 1.8
    sun.rotation_euler = (math.radians(50), math.radians(25), math.radians(CAMERA_YAW_DEG))

    os.makedirs(os.path.dirname(PREVIEW_PATH), exist_ok=True)
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    assert_preview_has_content(PREVIEW_PATH)
    print(f"Wrote {PREVIEW_PATH}")


def render_three_quarter_preview() -> None:
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 900
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.45, 0.58, 0.44, 1.0)

    s_mesh = build_settlement_mesh(HUMAN_RGB)
    s_mesh.location = (-0.55, 0, 0)
    c_mesh = build_city_mesh(AI_RGB)
    c_mesh.location = (0.05, 0, 0)
    r_mesh = build_road_mesh(HUMAN_RGB)
    r_mesh.location = (0.62, 0, 0)
    r_mesh.rotation_euler = (0, 0, math.radians(18))

    bpy.ops.object.camera_add(location=(1.1, -1.35, 0.95))
    cam = bpy.context.object
    cam.data.type = "PERSP"
    cam.data.lens = 42
    cam.rotation_euler = (math.radians(58), 0, math.radians(38))
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(2, -2, 3))
    sun = bpy.context.object
    sun.data.energy = 2.4
    sun.rotation_euler = (math.radians(48), math.radians(18), math.radians(25))

    os.makedirs(os.path.dirname(PREVIEW_34_PATH), exist_ok=True)
    scene.render.filepath = PREVIEW_34_PATH
    bpy.ops.render.render(write_still=True)
    assert_preview_has_content(PREVIEW_34_PATH)
    print(f"Wrote {PREVIEW_34_PATH}")


def render_all_sprites() -> None:
    render_sprite(build_settlement_mesh, "SettlementHuman", HUMAN_RGB)
    render_sprite(build_settlement_mesh, "SettlementAi", AI_RGB)
    render_sprite(build_city_mesh, "CityHuman", HUMAN_RGB)
    render_sprite(build_city_mesh, "CityAi", AI_RGB)


def main() -> None:
    bounds_doc = {}
    for name, builder in (
        ("settlement", lambda: build_settlement(HUMAN_RGB)),
        ("city", lambda: build_city(AI_RGB)),
        ("road", lambda: build_road(HUMAN_RGB)),
    ):
        root = builder()
        export_fbx(name, root)
        bounds_doc[name] = measure_bounds(root)

    with open(BOUNDS_JSON, "w", encoding="utf-8") as f:
        json.dump(bounds_doc, f, indent=2)
    print(f"Wrote {BOUNDS_JSON}")

    render_all_sprites()
    render_topdown_preview()
    render_three_quarter_preview()


if __name__ == "__main__":
    main()
