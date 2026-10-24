#!/usr/bin/env python3
"""Classic Catan wooden player pieces for the colonist board.

Builds three single, closed meshes and exports them as FBX for Unity:
  settlement.fbx  pentagon prism house (walls + pitched roof)
  city.fbx        long two-storey house joined with a taller tower house (~1.5x settlement footprint)
  road.fbx        wooden beam: ridged two-plane top, hipped bevelled ends
Material slots are named Wall / Roof / Edge; Unity tints them per player at runtime
(Roof darker, Edge = bevel faces darkest so every silhouette edge reads).
Also renders a 3/4 preview (docs/art/pieces_three_quarter.png).

Run: tools/blender/run-pieces.sh   (Blender 5.x, headless)
Model units: settlement wall width = 1.0 (Blender X), depth Y, height Z.
"""
from __future__ import annotations

import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = os.environ.get("BLENDER_TILE_REPO_ROOT", os.getcwd())
OUT_DIR = os.path.join(ROOT, "Assets/_Project/Resources/Art/Models/ColonistPieces")
PREVIEW_34_PATH = os.environ.get(
    "PIECES_PREVIEW_PATH", os.path.join(ROOT, "docs/art/pieces_three_quarter.png"))

HUMAN_RGB = (0.2, 0.4, 0.9)
AI_RGB = (0.9, 0.25, 0.2)
ROOF_SHADE = 0.62
EDGE_SHADE = 0.38
BEVEL = 0.07


def clear_scene() -> None:
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)


def material(name: str, rgb) -> bpy.types.Material:
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.8
    m.diffuse_color = (*rgb, 1.0)
    return m


def shade(rgb, k):
    return tuple(c * k for c in rgb)


def pentagon_prism(bm: bmesh.types.BMesh, x0, x1, depth, wall_h, peak_h, y0=0.0, along_y=False):
    """Pentagon profile (depth x wall_h, peak at peak_h) extruded from x0..x1.

    along_y=False: ridge along X (gable ends face +-X). along_y=True: ridge along Y.
    """
    hd = depth * 0.5
    profile = [(-hd, 0.0), (hd, 0.0), (hd, wall_h), (0.0, peak_h), (-hd, wall_h)]

    def v(x, p):
        if along_y:
            return Vector((p[0] + y0, x, p[1]))  # profile in X/Z, extruded along Y
        return Vector((x, p[0] + y0, p[1]))

    a = [bm.verts.new(v(x0, p)) for p in profile]
    b = [bm.verts.new(v(x1, p)) for p in profile]
    bm.faces.new(a[::-1])
    bm.faces.new(b)
    for i in range(5):
        j = (i + 1) % 5
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])


def mesh_object(name: str, bm: bmesh.types.BMesh) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def finish(obj: bpy.types.Object, mats, bevel=BEVEL) -> None:
    """Wall/Roof by face slope, bevel every hard edge into the Edge slot, keep it one closed mesh."""
    mesh = obj.data
    mesh.materials.clear()
    for m in mats:
        mesh.materials.append(m)
    for poly in mesh.polygons:
        nz = poly.normal.z
        poly.material_index = 1 if 0.2 < nz < 0.97 else 0
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = bevel
    mod.segments = 1
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(25)
    mod.material = 2
    mod.harden_normals = False
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # Triangulate here (beauty, concave-safe): Unity's FBX ngon triangulation drops triangles on
    # the concave walls where the city tower meets the long house, which opens holes.
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    non_manifold = [e for e in bm.edges if not e.is_manifold]
    bm.to_mesh(obj.data)
    bm.free()
    if non_manifold:
        raise RuntimeError(f"{obj.name}: {len(non_manifold)} non-manifold edges (mesh must be closed)")
    for poly in obj.data.polygons:
        poly.use_smooth = False


def build_settlement(mats) -> bpy.types.Object:
    bm = bmesh.new()
    pentagon_prism(bm, -0.5, 0.5, depth=0.86, wall_h=0.56, peak_h=1.0)
    obj = mesh_object("settlement", bm)
    finish(obj, mats)
    return obj


def build_city(mats) -> bpy.types.Object:
    # Long two-storey house (1.5 wide) with a tall tower house at the +X end, unioned into one mesh.
    bm = bmesh.new()
    pentagon_prism(bm, -0.75, 0.75, depth=0.86, wall_h=0.56, peak_h=0.96)
    wing = mesh_object("city", bm)
    bm = bmesh.new()
    pentagon_prism(bm, -0.38, 0.38, depth=0.6, wall_h=1.38, peak_h=1.84, y0=0.42, along_y=True)
    tower = mesh_object("tower", bm)
    mod = wing.modifiers.new("Union", "BOOLEAN")
    mod.operation = "UNION"
    mod.solver = "EXACT"
    mod.object = tower
    bpy.context.view_layer.objects.active = wing
    wing.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    wing.select_set(False)
    bpy.data.objects.remove(tower, do_unlink=True)
    finish(wing, mats)
    return wing


def build_road(mats) -> bpy.types.Object:
    """Wooden road beam: low ridged top (two roof planes so it shades two-tone like the houses)
    and hipped, bevelled ends that read clearly from straight above."""
    bm = bmesh.new()
    x0, x1, hd, wall, peak, hip = -0.5, 0.5, 0.1, 0.085, 0.125, 0.07
    lb0, rb0, rt0, lt0 = (bm.verts.new(Vector(c)) for c in ((x0, -hd, 0), (x0, hd, 0), (x0, hd, wall), (x0, -hd, wall)))
    lb1, rb1, rt1, lt1 = (bm.verts.new(Vector(c)) for c in ((x1, -hd, 0), (x1, hd, 0), (x1, hd, wall), (x1, -hd, wall)))
    r0 = bm.verts.new(Vector((x0 + hip, 0.0, peak)))
    r1 = bm.verts.new(Vector((x1 - hip, 0.0, peak)))
    for f in (
        (lb0, rb0, rb1, lb1),          # bottom
        (lb0, lt0, rt0, rb0),          # end wall 0
        (lb1, rb1, rt1, lt1),          # end wall 1
        (lb0, lb1, lt1, lt0),          # side -y
        (rb0, rt0, rt1, rb1),          # side +y
        (lt0, lt1, r1, r0),            # roof plane -y
        (rt0, r0, r1, rt1),            # roof plane +y
        (lt0, r0, rt0),                # hip 0
        (lt1, rt1, r1),                # hip 1
    ):
        bm.faces.new(f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    obj = mesh_object("road", bm)
    finish(obj, mats, bevel=0.018)
    return obj


def export_fbx(obj: bpy.types.Object, path: str) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    obj.select_set(False)


def player_mats(prefix: str, rgb):
    return [
        material(f"{prefix}Wall", rgb),
        material(f"{prefix}Roof", shade(rgb, ROOF_SHADE)),
        material(f"{prefix}Edge", shade(rgb, EDGE_SHADE)),
    ]


def render_preview(path: str) -> None:
    """3/4 view: blue settlement, city, road and a red set behind."""
    scene = bpy.context.scene
    objs = []
    for row, (prefix, rgb) in enumerate((("Blue", HUMAN_RGB), ("Red", AI_RGB))):
        mats = player_mats(prefix, rgb)
        s = build_settlement(mats)
        s.location = (-1.6, row * 1.6, 0)
        c = build_city(mats)
        c.location = (0.5, row * 1.6, 0)
        r = build_road(mats)
        r.location = (2.6, row * 1.6 - 0.2, 0)
        r.rotation_euler = (0, 0, math.radians(30))
        objs += [s, c, r]
    bpy.ops.mesh.primitive_plane_add(size=12, location=(0.5, 0.8, 0))
    ground = bpy.context.object
    ground.data.materials.append(material("Ground", (0.82, 0.74, 0.52)))

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 6.4
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)
    target = Vector((0.5, 0.8, 0.5))
    elev, yaw = math.radians(38), math.radians(-62)
    cam.location = target + Vector((math.cos(elev) * math.cos(yaw), math.cos(elev) * math.sin(yaw), math.sin(elev))) * 12
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.2
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(40), math.radians(10), math.radians(-35))
    bpy.context.collection.objects.link(sun)
    world = bpy.data.worlds.new("World") if scene.world is None else scene.world
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.6, 0.7, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7

    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items} else "CYCLES"
    if scene.render.engine == "CYCLES":
        scene.cycles.samples = 32
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 900
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    clear_scene()
    mats = player_mats("", (0.8, 0.8, 0.8))
    for name, builder in (("settlement", build_settlement), ("city", build_city), ("road", build_road)):
        obj = builder(mats)
        dims = obj.dimensions
        print(f"PIECE {name}: verts={len(obj.data.vertices)} faces={len(obj.data.polygons)} "
              f"dims=({dims.x:.3f},{dims.y:.3f},{dims.z:.3f}) closed=yes")
        export_fbx(obj, os.path.join(OUT_DIR, f"{name}.fbx"))
        bpy.data.objects.remove(obj, do_unlink=True)
    clear_scene()
    os.makedirs(os.path.dirname(PREVIEW_34_PATH), exist_ok=True)
    render_preview(PREVIEW_34_PATH)
    print(f"PREVIEW {PREVIEW_34_PATH}")


if __name__ == "__main__":
    main()
