"""CC0 photo textures + scatter helpers for hex terrain renders."""
from __future__ import annotations

import glob
import math
import os
import random

import bpy

ROOT = os.environ.get("BLENDER_TILE_REPO_ROOT", os.getcwd())
TEX_ROOT = os.path.join(ROOT, "tools/blender/textures")


def _find_map(slot: str, suffix: str) -> str | None:
    folder = os.path.join(TEX_ROOT, slot)
    if not os.path.isdir(folder):
        return None
    hits = glob.glob(os.path.join(folder, "**", f"*{suffix}*.jpg"), recursive=True)
    hits += glob.glob(os.path.join(folder, "**", f"*{suffix}*.JPG"), recursive=True)
    return hits[0] if hits else None


def image_texture_node(nodes, path: str) -> bpy.types.ShaderNodeTexImage:
    n = nodes.new("ShaderNodeTexImage")
    n.image = bpy.data.images.load(path, check_existing=True)
    n.interpolation = "Smart"
    return n


def mat_from_cc0(name: str, slot: str, tint: tuple[float, float, float] = (1, 1, 1), rough: float = 0.82) -> bpy.types.Material:
    color_path = _find_map(slot, "Color") or _find_map(slot, "Diffuse")
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    if color_path:
        tex = image_texture_node(nodes, color_path)
        mul = nodes.new("ShaderNodeMixRGB")
        mul.blend_type = "MULTIPLY"
        mul.inputs[0].default_value = 1.0
        mul.inputs[1].default_value = (1, 1, 1, 1)
        mul.inputs[2].default_value = (*tint, 1.0)
        links.new(tex.outputs["Color"], mul.inputs[1])
        links.new(mul.outputs["Color"], bsdf.inputs["Base Color"])
        disp_path = _find_map(slot, "Displacement")
        if disp_path:
            dtex = image_texture_node(nodes, disp_path)
            disp = nodes.new("ShaderNodeDisplacement")
            disp.inputs["Scale"].default_value = 0.12
            links.new(dtex.outputs["Color"], disp.inputs["Height"])
            links.new(disp.outputs["Displacement"], out.inputs["Displacement"])
    else:
        bsdf.inputs["Base Color"].default_value = (*tint, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def painted_hex_top(
    slot: str,
    tint: tuple[float, float, float],
    rough: float = 0.86,
    inner_r: float = 0.92,
    displace_strength: float = 0.16,
    mat_name: str | None = None,
) -> bpy.types.Object:
    """Single CC0-textured hex cap (no primitive scatter)."""
    label = mat_name or f"Painted_{slot}"
    mat = mat_from_cc0(label, slot, tint=tint, rough=rough)
    add_hex_floor(mat, z=-0.02, inner_r=inner_r)
    return displace_plane(mat, strength=displace_strength, inner_r=inner_r * 0.98)


def add_hex_floor(mat: bpy.types.Material, z: float = 0.0, inner_r: float = 0.92) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=inner_r, depth=0.05, location=(0, 0, z))
    obj = bpy.context.object
    obj.rotation_euler[2] = math.radians(30)
    obj.data.materials.append(mat)
    return obj


def displace_plane(mat: bpy.types.Material, strength: float = 0.35, inner_r: float = 0.92) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=inner_r, depth=0.02, location=(0, 0, 0.03))
    obj = bpy.context.object
    obj.rotation_euler[2] = math.radians(30)
    disp = obj.modifiers.new("Displace", "DISPLACE")
    tex = bpy.data.textures.new("Disp", "CLOUDS")
    tex.noise_scale = 0.45
    disp.texture = tex
    disp.strength = strength
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=disp.name)
    subs = obj.modifiers.new("Subsurf", "SUBSURF")
    subs.levels = 1
    bpy.ops.object.modifier_apply(modifier=subs.name)
    obj.data.materials.append(mat)
    return obj


def scatter_cones(
    rng: random.Random,
    count: int,
    radius_range: tuple[float, float],
    height_range: tuple[float, float],
    mat: bpy.types.Material,
    inner_r: float = 0.86,
) -> None:
    for i in range(count):
        ang = rng.random() * math.tau
        rad = rng.uniform(0.05, inner_r)
        x = math.cos(ang) * rad
        y = math.sin(ang) * rad
        if max(abs(y), abs(x) * 0.866025 + abs(y) * 0.5) > inner_r:
            continue
        h = rng.uniform(*height_range)
        bpy.ops.mesh.primitive_cone_add(
            vertices=5,
            radius1=radius_range[1],
            depth=h,
            location=(x, y, h * 0.5 + 0.04),
        )
        cone = bpy.context.object
        s = rng.uniform(radius_range[0], radius_range[1]) / radius_range[1]
        cone.scale = (s, s, 1.0)
        cone.data.materials.append(mat)
        cone.name = f"Tree_{i}"


def scatter_rock_cubes(
    rng: random.Random,
    count: int,
    size_range: tuple[float, float],
    mat: bpy.types.Material,
    highlight: bpy.types.Material | None = None,
    inner_r: float = 0.84,
) -> None:
    for i in range(count):
        ang = rng.random() * math.tau
        rad = rng.uniform(0.06, inner_r)
        x = math.cos(ang) * rad
        y = math.sin(ang) * rad
        if max(abs(y), abs(x) * 0.866025 + abs(y) * 0.5) > inner_r:
            continue
        sx = rng.uniform(*size_range)
        sy = sx * rng.uniform(0.7, 1.1)
        sz = sx * rng.uniform(0.45, 0.85)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, sz * 0.5 + 0.04))
        rock = bpy.context.object
        rock.scale = (sx, sy, sz)
        rock.rotation_euler = (
            rng.uniform(0.1, 0.5),
            rng.uniform(-0.4, 0.4),
            ang + rng.uniform(-0.5, 0.5),
        )
        rock.data.materials.append(mat)
        if highlight is not None and rng.random() < 0.35:
            bpy.ops.mesh.primitive_cube_add(
                size=1,
                location=(x + sx * 0.2, y + sy * 0.15, sz + 0.06),
            )
            cap = bpy.context.object
            cap.scale = (sx * 0.35, sy * 0.25, sz * 0.18)
            cap.data.materials.append(highlight)


def scatter_clay_terraces(
    rng: random.Random,
    mat: bpy.types.Material,
    inner_r: float = 0.82,
) -> None:
    for ring in range(3):
        rad = 0.18 + ring * 0.22
        steps = 5 + ring * 2
        for s in range(steps):
            ang = (s / steps) * math.tau + ring * 0.2
            x = math.cos(ang) * rad
            y = math.sin(ang) * rad
            if max(abs(y), abs(x) * 0.866025 + abs(y) * 0.5) > inner_r:
                continue
            h = 0.04 + ring * 0.035
            bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, h * 0.5))
            step = bpy.context.object
            step.scale = (0.14 - ring * 0.02, 0.10, h)
            step.rotation_euler = (0, 0, ang)
            step.data.materials.append(mat)


def scatter_icos(
    rng: random.Random,
    count: int,
    r_range: tuple[float, float],
    mat: bpy.types.Material,
    inner_r: float = 0.84,
) -> None:
    for i in range(count):
        ang = rng.random() * math.tau
        rad = rng.uniform(0.08, inner_r)
        x = math.cos(ang) * rad
        y = math.sin(ang) * rad
        if max(abs(y), abs(x) * 0.866025 + abs(y) * 0.5) > inner_r:
            continue
        r = rng.uniform(*r_range)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x, y, r * 0.55 + 0.05))
        rock = bpy.context.object
        rock.data.materials.append(mat)
        rock.name = f"Rock_{i}"


def scatter_sheep(rng: random.Random, count: int, mat: bpy.types.Material, inner_r: float = 0.82) -> None:
    for i in range(count):
        x = rng.uniform(-inner_r, inner_r)
        y = rng.uniform(-inner_r, inner_r)
        if max(abs(y), abs(x) * 0.866025 + abs(y) * 0.5) > inner_r:
            continue
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.028, location=(x, y, 0.05))
        s = bpy.context.object
        s.scale = (1.1, 1.35, 0.7)
        s.data.materials.append(mat)
        s.name = f"Sheep_{i}"


def setup_painterly_compositor(scene: bpy.types.Scene) -> None:
    scene.use_nodes = True
    tree = scene.node_tree
    tree.nodes.clear()
    rl = tree.nodes.new("CompositorNodeRLayers")
    scale = tree.nodes.new("CompositorNodeScale")
    scale.space = "RENDER_SIZE"
    scale.frame_method = "STRETCH"
    scale.inputs[1].default_value = 0.5
    scale.inputs[2].default_value = 0.5
    scale2 = tree.nodes.new("CompositorNodeScale")
    scale2.space = "RENDER_SIZE"
    scale2.frame_method = "STRETCH"
    scale2.inputs[1].default_value = 2.0
    scale2.inputs[2].default_value = 2.0
    poster = tree.nodes.new("CompositorNodePosterize")
    poster.inputs[1].default_value = 10
    comp = tree.nodes.new("CompositorNodeComposite")
    tree.links.new(rl.outputs["Image"], scale.inputs[0])
    tree.links.new(scale.outputs[0], scale2.inputs[0])
    tree.links.new(scale2.outputs[0], poster.inputs[0])
    tree.links.new(poster.outputs[0], comp.inputs[0])
