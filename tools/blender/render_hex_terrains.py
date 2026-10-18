#!/usr/bin/env python3
"""Headless Blender: painterly Catan-style hex terrain albedos (v117+)."""
from __future__ import annotations

import math
import os
import random
import shutil

import bpy

ROOT = os.environ.get("BLENDER_TILE_REPO_ROOT", os.getcwd())
RES = int(os.environ.get("BLENDER_TILE_RES", "1024"))
PASS = int(os.environ.get("BLENDER_TILE_PASS", "3"))
OUT_RESOURCES = os.path.join(ROOT, "Assets/_Project/Resources/Art/Board/Blender")
OUT_DOCS = os.path.join(ROOT, "docs/art/blender-tiles")
REF_PHOTO = os.environ.get(
    "BLENDER_REF_PHOTO",
    os.path.join(ROOT, "docs/art/blender-tiles/reference_board.jpg"),
)

INNER_R = 0.92
DENSITY = 1.0 + (PASS - 1) * 0.15


def import_hex_post():
    post_path = os.path.join(ROOT, "tools/blender/hex_post.py")
    import importlib.util

    spec = importlib.util.spec_from_file_location("hex_post", post_path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def setup_render() -> None:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 40 + PASS * 20
    scene.cycles.use_denoising = False
    scene.cycles.max_bounces = 3
    scene.render.resolution_x = RES
    scene.render.resolution_y = RES
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.exposure = -0.45

    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.85, 0.90, 0.98, 1.0)
    bg.inputs[1].default_value = 0.85

    bpy.ops.object.camera_add(location=(0, 0, 4.2))
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.05
    cam.rotation_euler = (0, 0, 0)
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(2.5, -3.5, 9))
    sun = bpy.context.object
    sun.data.energy = 2.4 + PASS * 0.15
    sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(42), math.radians(10), math.radians(22))

    bpy.ops.object.light_add(type="AREA", location=(-2.5, 2.5, 5))
    fill = bpy.context.object
    fill.data.energy = 180
    fill.data.size = 7
    fill.rotation_euler = (math.radians(62), 0, math.radians(-28))


def inside_hex(x: float, y: float, r: float = INNER_R) -> bool:
    px = abs(x) / r
    py = abs(y) / r
    return max(py, px * 0.866025 + py * 0.5) <= 1.0


def mat_solid(name: str, rgb: tuple[float, float, float], rough: float = 0.75) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    return m


def mat_striped_field(name: str, gold: tuple[float, float, float], ochre: tuple[float, float, float], angle: float) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexWave")
    tex.wave_type = "BANDS"
    tex.bands_direction = "X"
    tex.inputs["Scale"].default_value = 28.0
    tex.inputs["Distortion"].default_value = 1.2
    tex.inputs["Detail"].default_value = 2.0
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.45
    ramp.color_ramp.elements[0].color = (*ochre, 1.0)
    ramp.color_ramp.elements[1].position = 0.55
    ramp.color_ramp.elements[1].color = (*gold, 1.0)
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Rotation"].default_value[2] = math.radians(angle)
    coord = nodes.new("ShaderNodeTexCoord")
    links.new(coord.outputs["Generated"], mapping.inputs["Vector"])
    links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.82
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def mat_mountain(name: str) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 4.5
    noise.inputs["Detail"].default_value = 6.0
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.32, 0.36, 0.48, 1.0)
    ramp.color_ramp.elements[1].color = (0.58, 0.62, 0.68, 1.0)
    geom = nodes.new("ShaderNodeNewGeometry")
    snow_ramp = nodes.new("ShaderNodeValToRGB")
    snow_ramp.color_ramp.elements[0].position = 0.55
    snow_ramp.color_ramp.elements[0].color = (0.35, 0.40, 0.52, 1.0)
    snow_ramp.color_ramp.elements[1].position = 0.78
    snow_ramp.color_ramp.elements[1].color = (0.95, 0.97, 1.0, 1.0)
    mix = nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MIX"
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], mix.inputs[1])
    links.new(geom.outputs["Position"], snow_ramp.inputs["Fac"])
    links.new(snow_ramp.outputs["Color"], mix.inputs[2])
    links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.68
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def mat_clay_pit(name: str) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    vor = nodes.new("ShaderNodeTexVoronoi")
    vor.feature = "DISTANCE_TO_EDGE"
    vor.inputs["Scale"].default_value = 2.8
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.28, 0.14, 0.08, 1.0)
    ramp.color_ramp.elements[1].color = (0.72, 0.38, 0.18, 1.0)
    links.new(vor.outputs["Distance"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def mat_grass_meadow(name: str) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    n1 = nodes.new("ShaderNodeTexNoise")
    n1.inputs["Scale"].default_value = 12.0
    n2 = nodes.new("ShaderNodeTexNoise")
    n2.inputs["Scale"].default_value = 35.0
    mix = nodes.new("ShaderNodeMixRGB")
    mix.inputs[0].default_value = 0.55
    mix.inputs[1].default_value = (0.14, 0.42, 0.12, 1.0)
    mix.inputs[2].default_value = (0.38, 0.68, 0.24, 1.0)
    links.new(n1.outputs["Fac"], mix.inputs[0])
    links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(n2.outputs["Fac"], bsdf.inputs["Roughness"])
    bsdf.inputs["Roughness"].default_value = 0.9
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def add_hex_floor(mat: bpy.types.Material, z: float = 0.0) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=INNER_R, depth=0.06, location=(0, 0, z))
    obj = bpy.context.object
    obj.rotation_euler[0] = 0
    obj.rotation_euler[2] = math.radians(30)
    obj.data.materials.append(mat)
    return obj


def displace_grid(strength: float, tex_type: str = "CLOUDS", scale: float = 0.5) -> bpy.types.Object:
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=128, y_subdivisions=128, size=1.95)
    grid = bpy.context.object
    grid.rotation_euler = (0, 0, math.radians(30))
    disp = grid.modifiers.new("Displace", "DISPLACE")
    tex = bpy.data.textures.new("Disp", tex_type)
    tex.noise_scale = scale
    if hasattr(tex, "noise_depth"):
        tex.noise_depth = 4
    disp.texture = tex
    disp.strength = strength
    bpy.context.view_layer.objects.active = grid
    bpy.ops.object.modifier_apply(modifier=disp.name)
    subs = grid.modifiers.new("Subsurf", "SUBSURF")
    subs.levels = 1
    bpy.ops.object.modifier_apply(modifier=subs.name)
    return grid


def build_forest() -> None:
    add_hex_floor(mat_solid("Soil", (0.10, 0.28, 0.08)))
    rng = random.Random(101 + PASS)
    spacing = 0.11 / DENSITY
    for row in range(-8, 9):
        for col in range(-8, 9):
            x = col * spacing * 1.05 + (row % 2) * spacing * 0.52
            y = row * spacing * 0.92
            x += rng.uniform(-0.02, 0.02)
            y += rng.uniform(-0.02, 0.02)
            if not inside_hex(x, y, 0.86):
                continue
            rad = rng.uniform(0.09, 0.14)
            roll = rng.random()
            if roll < 0.14:
                col = (0.92, 0.62, 0.12)
            elif roll < 0.32:
                col = (0.22, 0.52, 0.14)
            else:
                col = (0.08 + rng.uniform(0, 0.05), 0.32 + rng.uniform(0, 0.16), 0.07)
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=rad, location=(x, y, rad * 0.55))
            t = bpy.context.object
            t.scale = (1.05, 1.05, 0.65)
            t.data.materials.append(mat_solid(f"T{row}{col}", col, 0.78))


def build_wheat() -> None:
    add_hex_floor(mat_solid("Dirt", (0.52, 0.38, 0.14)))
    parcels = [
        (-0.35, 0.15, 25, (0.90, 0.72, 0.12), (0.50, 0.34, 0.10)),
        (0.25, 0.28, 110, (0.86, 0.66, 0.11), (0.48, 0.32, 0.09)),
        (0.05, -0.32, 70, (0.92, 0.76, 0.14), (0.54, 0.38, 0.10)),
        (-0.22, -0.18, 155, (0.82, 0.64, 0.10), (0.44, 0.30, 0.08)),
        (0.38, -0.08, 0, (0.88, 0.70, 0.12), (0.50, 0.34, 0.10)),
        (-0.05, 0.38, 45, (0.90, 0.74, 0.13), (0.52, 0.36, 0.10)),
    ]
    for cx, cy, ang, gold, ochre in parcels:
        if not inside_hex(cx, cy, 0.78):
            continue
        rot = math.radians(ang)
        for stripe in range(-7, 8):
            off = stripe * 0.042
            lx = cx + math.cos(rot) * off
            ly = cy + math.sin(rot) * off
            bpy.ops.mesh.primitive_cube_add(size=1, location=(lx, ly, 0.04))
            s = bpy.context.object
            s.scale = (0.038, 0.26, 0.012)
            s.rotation_euler = (0, 0, rot)
            col = gold if stripe % 2 == 0 else ochre
            s.data.materials.append(mat_solid(f"St{stripe}", col, 0.82))
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.32, -0.28, 0.06))
    barn = bpy.context.object
    barn.scale = (0.07, 0.05, 0.045)
    barn.data.materials.append(mat_solid("Barn", (0.40, 0.26, 0.16)))


def build_pasture() -> None:
    grid = displace_grid(0.07 + PASS * 0.015, "CLOUDS", 0.9)
    grid.data.materials.append(mat_grass_meadow("Meadow"))
    add_hex_floor(mat_solid("MeadowBase", (0.22, 0.52, 0.18)))
    rng = random.Random(303 + PASS)
    for flock in range(int(16 * DENSITY)):
        fx = rng.uniform(-0.7, 0.7)
        fy = rng.uniform(-0.7, 0.7)
        if not inside_hex(fx, fy, 0.82):
            continue
        for _ in range(rng.randint(3, 7)):
            ox = fx + rng.uniform(-0.06, 0.06)
            oy = fy + rng.uniform(-0.06, 0.06)
            if not inside_hex(ox, oy, 0.84):
                continue
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.022, location=(ox, oy, 0.05))
            s = bpy.context.object
            s.scale = (1.0, 1.2, 0.65)
            s.data.materials.append(mat_solid("Sh", (0.98, 0.98, 0.95), 0.7))
    bpy.ops.mesh.primitive_plane_add(size=0.35, location=(0.12, 0.08, 0.025))
    path = bpy.context.object
    path.rotation_euler = (0, 0, math.radians(35))
    path.scale = (0.35, 1.6, 1.0)
    path.data.materials.append(mat_solid("Path", (0.80, 0.84, 0.66), 0.95))


def build_ore() -> None:
    grid = displace_grid(0.72 + PASS * 0.1, "MARBLE", 0.38)
    grid.data.materials.append(mat_mountain("Ore"))
    disp2 = grid.modifiers.new("Displace", "DISPLACE")
    tex2 = bpy.data.textures.new("Ridge", "STUCCI")
    tex2.noise_scale = 0.25
    disp2.texture = tex2
    disp2.strength = 0.18
    bpy.context.view_layer.objects.active = grid
    bpy.ops.object.modifier_apply(modifier=disp2.name)


def build_clay() -> None:
    grid = displace_grid(0.38 + PASS * 0.06, "VORONOI", 0.65)
    grid.data.materials.append(mat_clay_pit("Clay"))


def mat_sand_dunes(name: str) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 6.0
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.78, 0.62, 0.38, 1.0)
    ramp.color_ramp.elements[1].color = (0.94, 0.82, 0.58, 1.0)
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def build_desert() -> None:
    add_hex_floor(mat_solid("SandBase", (0.88, 0.72, 0.46)))
    grid = displace_grid(0.12 + PASS * 0.02, "CLOUDS", 1.1)
    grid.data.materials.append(mat_sand_dunes("Sand"))
    rng = random.Random(505 + PASS)
    for i in range(22 + PASS * 3):
        ang = rng.random() * math.tau
        rad = rng.random() * 0.78
        x = math.cos(ang) * rad
        y = math.sin(ang) * rad
        if not inside_hex(x, y, 0.85):
            continue
        bpy.ops.mesh.primitive_cylinder_add(radius=0.005, depth=0.10, location=(x, y, 0.035))
        tw = bpy.context.object
        tw.rotation_euler = (rng.uniform(0.4, 1.1), rng.uniform(-0.3, 0.3), ang)
        tw.data.materials.append(mat_solid(f"Tw{i}", (0.42, 0.30, 0.20)))


BUILDERS = {
    "Wood": build_forest,
    "Wheat": build_wheat,
    "Sheep": build_pasture,
    "Ore": build_ore,
    "Brick": build_clay,
    "Desert": build_desert,
}


def render_slot(name: str) -> str:
    clear_scene()
    setup_render()
    BUILDERS[name]()
    os.makedirs(OUT_RESOURCES, exist_ok=True)
    os.makedirs(OUT_DOCS, exist_ok=True)
    out_res = os.path.join(OUT_RESOURCES, f"{name}.png")
    out_doc = os.path.join(OUT_DOCS, f"{name}_preview.png")
    bpy.context.scene.render.filepath = out_res
    bpy.ops.render.render(write_still=True)
    hex_post = import_hex_post()
    pre_var = hex_post.interior_luma_variance(out_res)
    sat = 0.92 + PASS * 0.02
    hex_post.process_png(out_res, saturation=sat)
    post_var = hex_post.interior_luma_variance(out_res)
    default_min = float(os.environ.get("BLENDER_TILE_MIN_LUMA_STD", "10"))
    min_var = {"Desert": 2.0, "Brick": 7.0}.get(name, default_min)
    if post_var < min_var:
        raise RuntimeError(
            f"{name}: interior luma std {post_var:.1f} < {min_var} (pre={pre_var:.1f}); render is washed out"
        )
    hex_post.assert_terrain_color(name, out_res)
    shutil.copy2(out_res, out_doc)
    print(f"Wrote {out_res}")
    return out_res


def make_contact_sheet(paths: list[str]) -> None:
    hex_post = import_hex_post()
    if hex_post.Image is None:
        return
    tiles = [hex_post.Image.open(p).convert("RGBA") for p in paths]
    w, h = tiles[0].size
    sheet = hex_post.Image.new("RGBA", (w * 3, h * 2), (32, 32, 32, 255))
    for i, im in enumerate(tiles):
        sheet.paste(im, ((i % 3) * w, (i // 3) * h), im)
    sheet.save(os.path.join(OUT_DOCS, "contact_sheet.png"))


def write_pass_notes() -> None:
    notes = {
        1: """## Pass 1 (baseline Cycles)
- Honest critique: sand bevel was broken (opaque black rim); wheat cubes cast a black shadow blob;
  ore/clay read as floating bubbles; saturation too low vs photo. Side-by-side crops were misaligned.
""",
        2: """## Pass 2
- Fixed hex mask/bevel (true sand-yellow rim), striped shader wheat parcels, grid forest canopy,
  displaced ore/clay/desert with procedural materials, better reference crops.
- Still short of photo: mountains need sharper ridges; forest needs more yellow-orange pops;
  clay pits still smooth; overall more painterly hand-tuned contrast needed.
""",
        3: """## Pass 3
- Increased displacement, saturation, sun energy, and forest/wheat density.
- Closer on layout and color family but still stylized 3D vs printed board illustration.
  Keeping `ColonistBlenderHexTerrainArt.Enabled = false` until another art pass.
""",
        6: """## Pass 6 (v120)
- Re-aligned reference_board.jpg crops via HSV match; lowered exposure/sun/fill and saturation.
- Toned forest/sheep greens; still not photo-close — `Enabled = false`.
""",
    }
    path = os.path.join(OUT_DOCS, "pass_notes.md")
    with open(path, "w", encoding="utf-8") as f:
        f.write("# Blender hex tile iteration (v117)\n")
        for p in sorted(notes):
            if p <= PASS:
                f.write(notes[p])


def main() -> None:
    print(f"BLENDER_TILE_PASS={PASS}")
    paths = []
    render_paths = {}
    for name in BUILDERS:
        p = render_slot(name)
        paths.append(p)
        render_paths[name] = p
    make_contact_sheet(paths)
    hex_post = import_hex_post()
    if os.path.isfile(REF_PHOTO):
        hex_post.make_side_by_side(
            REF_PHOTO,
            render_paths,
            os.path.join(OUT_DOCS, "side_by_side.png"),
            list(BUILDERS.keys()),
        )
    write_pass_notes()


if __name__ == "__main__":
    main()
