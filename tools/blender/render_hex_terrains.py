#!/usr/bin/env python3
"""Headless Blender: orthographic painted hex terrain albedos (v116).

Usage:
  xvfb-run -a blender -b --python tools/blender/render_hex_terrains.py
"""
from __future__ import annotations

import math
import os
import random
import shutil
import sys

import bpy
from mathutils import Vector

ROOT = os.environ.get("BLENDER_TILE_REPO_ROOT", os.getcwd())
RES = int(os.environ.get("BLENDER_TILE_RES", "1024"))
OUT_RESOURCES = os.path.join(ROOT, "Assets/_Project/Resources/Art/Board/Blender")
OUT_DOCS = os.path.join(ROOT, "docs/art/blender-tiles")

HEX_R = 1.0
BEVEL = 0.055


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in bpy.data.materials:
        if block.users == 0:
            bpy.data.materials.remove(block)


def setup_render() -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RES
    scene.render.resolution_y = RES
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.eevee.taa_render_samples = 32

    bpy.ops.object.camera_add(location=(0, 0, 8))
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = HEX_R * 2.35
    cam.rotation_euler = (0, 0, 0)
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(2, -3, 6))
    sun = bpy.context.object
    sun.data.energy = 2.2
    sun.rotation_euler = (math.radians(55), math.radians(8), math.radians(20))

    bpy.ops.object.light_add(type="AREA", location=(-2, 2, 4))
    fill = bpy.context.object
    fill.data.energy = 180
    fill.data.size = 4
    fill.rotation_euler = (math.radians(70), 0, math.radians(-35))

    return cam


def mat_rgb(name: str, rgb: tuple[float, float, float], rough: float = 0.85) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    return m


def add_hex_base(z: float = 0.0, radius: float = HEX_R * 0.94) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=radius, depth=0.12, location=(0, 0, z))
    obj = bpy.context.object
    obj.rotation_euler[2] = math.radians(30)
    return obj


def add_bevel_ring() -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6, radius=HEX_R * 1.02, depth=0.14, location=(0, 0, -0.02)
    )
    outer = bpy.context.object
    outer.rotation_euler[2] = math.radians(30)
    outer.data.materials.append(mat_rgb("Bevel", (0.93, 0.86, 0.65), 0.9))

    inner = add_hex_base(z=-0.01, radius=HEX_R * 0.90)
    bpy.context.view_layer.objects.active = outer
    mod = outer.modifiers.new("Bool", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = inner
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(inner, do_unlink=True)
    return outer


def scatter_on_hex(
    count: int,
    factory,
    seed: int,
    r_max: float = 0.82,
) -> None:
    rng = random.Random(seed)
    for i in range(count):
        ang = rng.random() * math.tau
        rad = math.sqrt(rng.random()) * r_max
        x = math.cos(ang) * rad
        y = math.sin(ang) * rad
        factory(x, y, i, rng)


def build_forest() -> None:
    base = add_hex_base()
    base.data.materials.append(mat_rgb("ForestFloor", (0.12, 0.38, 0.14)))

    def tree(x, y, i, rng):
        h = rng.uniform(0.18, 0.32)
        bpy.ops.mesh.primitive_cone_add(
            vertices=8, radius1=0.11, depth=h, location=(x, y, h * 0.5 + 0.02)
        )
        t = bpy.context.object
        shade = rng.uniform(0.08, 0.22)
        t.data.materials.append(mat_rgb(f"Tree{i}", (0.05, shade, 0.06)))

    scatter_on_hex(140, tree, seed=11, r_max=0.88)


def build_wheat() -> None:
    base = add_hex_base()
    base.data.materials.append(mat_rgb("WheatSoil", (0.55, 0.42, 0.18)))

    for patch in range(5):
        ang = patch * 72 + 10
        cx = math.cos(math.radians(ang)) * 0.35
        cy = math.sin(math.radians(ang)) * 0.35
        rot = math.radians(patch * 37 - 20)
        bpy.ops.mesh.primitive_plane_add(size=0.55, location=(cx, cy, 0.07))
        plane = bpy.context.object
        plane.rotation_euler = (math.radians(90), 0, rot)
        plane.data.materials.append(mat_rgb(f"Wheat{patch}", (0.86, 0.72, 0.18)))

        for stripe in range(-3, 4):
            bpy.ops.mesh.primitive_cube_add(
                size=1,
                location=(cx + stripe * 0.06, cy, 0.09),
            )
            s = bpy.context.object
            s.scale = (0.04, 0.22, 0.02)
            s.rotation_euler = (0, 0, rot)
            s.data.materials.append(mat_rgb(f"Furrow{patch}{stripe}", (0.72, 0.58, 0.12)))

    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.15, -0.2, 0.1))
    barn = bpy.context.object
    barn.scale = (0.12, 0.08, 0.06)
    barn.data.materials.append(mat_rgb("Barn", (0.45, 0.32, 0.22)))


def build_pasture() -> None:
    base = add_hex_base()
    base.data.materials.append(mat_rgb("Meadow", (0.38, 0.78, 0.28)))

    def sheep(x, y, i, rng):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.06, location=(x, y, 0.08))
        body = bpy.context.object
        body.scale = (1.1, 1.4, 0.75)
        body.data.materials.append(mat_rgb(f"Wool{i}", (0.96, 0.96, 0.94)))
        bpy.ops.mesh.primitive_uv_sphere_add(
            radius=0.035, location=(x + 0.05, y + 0.04, 0.1)
        )
        head = bpy.context.object
        head.data.materials.append(mat_rgb(f"Head{i}", (0.18, 0.16, 0.16)))

    for flock in range(8):
        ang = flock * 45
        fx = math.cos(math.radians(ang)) * 0.4
        fy = math.sin(math.radians(ang)) * 0.4
        scatter_on_hex(4, lambda x, y, i, r: sheep(fx + x * 0.15, fy + y * 0.15, flock * 10 + i, r), seed=20 + flock, r_max=0.12)


def build_ore() -> None:
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=64, y_subdivisions=64, size=HEX_R * 1.8)
    grid = bpy.context.object
    grid.rotation_euler = (math.radians(90), 0, math.radians(30))
    disp = grid.modifiers.new("Displace", "DISPLACE")
    tex = bpy.data.textures.new("OreNoise", "CLOUDS")
    tex.noise_scale = 0.45
    disp.texture = tex
    disp.strength = 0.35
    bpy.context.view_layer.objects.active = grid
    bpy.ops.object.modifier_apply(modifier=disp.name)
    grid.data.materials.append(mat_rgb("OreRock", (0.55, 0.57, 0.6), 0.75))

    for peak in range(6):
        ang = peak * 60
        px = math.cos(math.radians(ang)) * 0.35
        py = math.sin(math.radians(ang)) * 0.35
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.22, location=(px, py, 0.15))
        p = bpy.context.object
        p.scale = (1.0, 1.0, 1.6)
        p.data.materials.append(mat_rgb(f"Peak{peak}", (0.92, 0.94, 0.96)))


def build_clay() -> None:
    base = add_hex_base()
    base.data.materials.append(mat_rgb("ClayBase", (0.62, 0.36, 0.22)))

    for pit in range(7):
        ang = pit * 51
        px = math.cos(math.radians(ang)) * 0.42
        py = math.sin(math.radians(ang)) * 0.42
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.18, location=(px, py, 0.02))
        s = bpy.context.object
        s.scale = (1.2, 1.2, 0.35)
        s.data.materials.append(mat_rgb(f"Pit{pit}", (0.38, 0.22, 0.12)))

    for ridge in range(5):
        ang = ridge * 70 + 15
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.12))
        r = bpy.context.object
        r.scale = (0.08, 0.55, 0.08)
        r.rotation_euler = (0, 0, math.radians(ang))
        r.data.materials.append(mat_rgb(f"Ridge{ridge}", (0.72, 0.48, 0.28)))


def build_desert() -> None:
    base = add_hex_base()
    base.data.materials.append(mat_rgb("Sand", (0.9, 0.82, 0.62)))

    for dune in range(4):
        ang = dune * 90
        bpy.ops.mesh.primitive_plane_add(size=0.9, location=(0, 0, 0.05 + dune * 0.01))
        d = bpy.context.object
        d.rotation_euler = (math.radians(90), math.radians(ang), 0)
        d.data.materials.append(mat_rgb(f"Dune{d}", (0.94, 0.86, 0.68)))

    rng = random.Random(99)
    for i in range(12):
        ang = rng.random() * math.tau
        rad = rng.random() * 0.75
        x = math.cos(ang) * rad
        y = math.sin(ang) * rad
        bpy.ops.mesh.primitive_cylinder_add(
            radius=0.008, depth=0.14, location=(x, y, 0.07)
        )
        twig = bpy.context.object
        twig.rotation_euler = (rng.uniform(0.2, 0.8), 0, ang)
        twig.data.materials.append(mat_rgb(f"Twig{i}", (0.42, 0.32, 0.22)))


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
    add_bevel_ring()
    BUILDERS[name]()
    os.makedirs(OUT_RESOURCES, exist_ok=True)
    os.makedirs(OUT_DOCS, exist_ok=True)
    out_res = os.path.join(OUT_RESOURCES, f"{name}.png")
    out_doc = os.path.join(OUT_DOCS, f"{name}_preview.png")
    bpy.context.scene.render.filepath = out_res
    bpy.ops.render.render(write_still=True)
    shutil.copy2(out_res, out_doc)
    print(f"Wrote {out_res} and {out_doc}")
    return out_res


def make_contact_sheet(paths: list[str]) -> None:
    try:
        from PIL import Image
    except ImportError:
        print("PIL not available; skip contact sheet", file=sys.stderr)
        return

    tiles = [Image.open(p).convert("RGBA") for p in paths]
    w, h = tiles[0].size
    cols = 3
    rows = 2
    sheet = Image.new("RGBA", (w * cols, h * rows), (30, 30, 30, 255))
    labels = ["Wood", "Wheat", "Sheep", "Ore", "Brick", "Desert"]
    for i, im in enumerate(tiles):
        x = (i % cols) * w
        y = (i // cols) * h
        sheet.paste(im, (x, y), im)
    out = os.path.join(OUT_DOCS, "contact_sheet.png")
    sheet.save(out)
    print(f"Wrote {out}")


def main() -> None:
    paths = []
    for name in BUILDERS:
        paths.append(render_slot(name))
    make_contact_sheet(paths)


if __name__ == "__main__":
    main()
