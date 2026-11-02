"""Build a small tree family from the samor_4077_133 source mesh.

The repository's tree generator recurses from a main stem into smaller branch
segments. This adaptation keeps the source tree's authored low-poly silhouette,
UVs, and foliage islands, then applies a recursive branch field to five style
presets. Run from Blender:

    blender --background --factory-startup --python build_samor_tree_family.py
"""

from pathlib import Path
import math
import shutil

import bpy
from mathutils import Vector


HERE = Path(__file__).resolve().parent
TREE_ROOT = HERE.parent
SOURCE_FBX = (
    TREE_ROOT.parent
    / "4077_卡通低面森林场景"
    / "Mountain Environment.fbx"
)
FBX_DIR = TREE_ROOT / "FBX"
PREVIEW_DIR = TREE_ROOT / "Previews"


STYLES = (
    {
        "name": "Spreading",
        "height": 0.78,
        "crown_width": 1.34,
        "lean": 0.10,
        "twist": 0.10,
        "wave": 0.24,
        "fork": 0.0,
    },
    {
        "name": "Slender",
        "height": 1.42,
        "crown_width": 0.76,
        "lean": -0.08,
        "twist": -0.08,
        "wave": 0.12,
        "fork": 0.0,
    },
    {
        "name": "WindSwept",
        "height": 1.04,
        "crown_width": 1.18,
        "lean": 2.25,
        "twist": 0.20,
        "wave": 0.42,
        "fork": 0.0,
    },
    {
        "name": "TwinFork",
        "height": 1.22,
        "crown_width": 1.02,
        "lean": 0.0,
        "twist": 0.0,
        "wave": 0.18,
        "fork": 1.18,
    },
    {
        "name": "LowCanopy",
        "height": 0.66,
        "crown_width": 1.22,
        "lean": -0.18,
        "twist": 0.14,
        "wave": 0.20,
        "fork": 0.0,
    },
)


def smoothstep(edge0, edge1, value):
    if edge0 == edge1:
        return 0.0
    t = max(0.0, min(1.0, (value - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def import_source():
    bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))
    source = bpy.data.objects.get("samor_4077_133")
    if source is None or source.type != "MESH":
        raise RuntimeError("samor_4077_133 was not found in Mountain Environment.fbx")

    mesh = source.data.copy()
    mesh.name = "samor_4077_133_source"
    mesh.transform(source.matrix_world)

    corners = [Vector(corner) for corner in source.bound_box]
    world_corners = [source.matrix_world @ corner for corner in corners]
    min_x = min(c.x for c in world_corners)
    max_x = max(c.x for c in world_corners)
    min_y = min(c.y for c in world_corners)
    max_y = max(c.y for c in world_corners)
    min_z = min(c.z for c in world_corners)
    max_z = max(c.z for c in world_corners)
    center_x = (min_x + max_x) * 0.5
    center_y = (min_y + max_y) * 0.5
    height = max_z - min_z

    for vertex in mesh.vertices:
        vertex.co.x -= center_x
        vertex.co.y -= center_y
        vertex.co.z -= min_z

    material = source.data.materials[0] if source.data.materials else None
    for obj in list(bpy.data.objects):
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.objects.remove(source, do_unlink=True)

    return mesh, material, height


def deform_style(mesh, height, params):
    crown_start = 0.22
    fork_start = 0.34

    for vertex in mesh.vertices:
        co = vertex.co.copy()
        t = max(0.0, min(1.0, co.z / height))
        crown = smoothstep(crown_start, 1.0, t)
        radius = math.hypot(co.x, co.y)
        angle = math.atan2(co.y, co.x)

        # Recursive branch field: every higher tier bends, twists, and widens
        # around the previous branch direction.
        angle += params["twist"] * crown * (0.35 + 0.65 * t)
        radial = 1.0 + (params["crown_width"] - 1.0) * crown
        radial *= 1.0 + params["wave"] * 0.08 * math.sin(t * math.pi * 3.0)
        x = math.cos(angle) * radius * radial
        y = math.sin(angle) * radius * radial

        sway = params["lean"] * (t ** 1.65)
        sway += params["wave"] * 0.18 * math.sin(t * math.pi * 2.2)
        x += sway
        y += params["wave"] * 0.08 * math.cos(t * math.pi * 2.7)

        if params["fork"] > 0.0 and t > fork_start:
            fork = smoothstep(fork_start, 1.0, t) * params["fork"]
            side = 1.0 if co.x >= 0.0 else -1.0
            x += side * fork * (0.55 + math.sin(t * math.pi) * 0.45)
            y += side * fork * 0.18 * math.sin(t * math.pi * 1.5)
            z = co.z * params["height"]
            z += side * fork * 0.14 * smoothstep(fork_start, 0.9, t)
        else:
            z = co.z * params["height"]

        vertex.co = (x, y, max(0.0, z))

    mesh.update()


def make_object(mesh, material, name):
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if material is not None:
        obj.data.materials.append(material)
    return obj


def export_variants(source_mesh, material, source_height):
    shutil.rmtree(FBX_DIR, ignore_errors=True)
    FBX_DIR.mkdir(parents=True, exist_ok=True)
    variants = []

    for index, params in enumerate(STYLES):
        mesh = source_mesh.copy()
        mesh.name = f"samor_4077_133_{params['name']}"
        deform_style(mesh, source_height, params)
        obj = make_object(mesh, material, mesh.name)
        variants.append(obj)

        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        path = FBX_DIR / f"{mesh.name}.fbx"
        bpy.ops.export_scene.fbx(
            filepath=str(path),
            use_selection=True,
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
            path_mode="COPY",
            embed_textures=False,
            add_leaf_bones=False,
        )
        print(
            "EXPORTED",
            path,
            "verts=",
            len(mesh.vertices),
            "materials=",
            len(mesh.materials),
        )

    return variants


def add_preview_setup(variants):
    for index, obj in enumerate(variants):
        obj.location.x = (index - 2) * 11.5

    bpy.ops.mesh.primitive_plane_add(size=70, location=(0, 0, -0.12))
    ground = bpy.context.object
    ground.name = "Preview_Ground"
    ground_material = bpy.data.materials.new("Preview Ground")
    ground_material.diffuse_color = (0.075, 0.10, 0.085, 1.0)
    ground.data.materials.append(ground_material)

    bpy.ops.object.camera_add(location=(0, -55, 14.0))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 62.0
    target = Vector((0, 0, 8.3))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera

    for location, energy, size in (
        ((-16, -20, 26), 1500, 20),
        ((20, 5, 18), 950, 24),
        ((0, 18, 20), 1100, 18),
    ):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (
            Vector((0, 0, 6)) - light.location
        ).to_track_quat("-Z", "Y").to_euler()

def render_preview(variants):
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    add_preview_setup(variants)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 2200
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world.color = (0.035, 0.055, 0.065)
    scene.render.filepath = str(PREVIEW_DIR / "samor_tree_family.png")
    bpy.ops.render.render(write_still=True)
    print("PREVIEW", scene.render.filepath)


def main():
    if not SOURCE_FBX.is_file():
        raise FileNotFoundError(SOURCE_FBX)
    reset_scene()
    source_mesh, material, source_height = import_source()
    variants = export_variants(source_mesh, material, source_height)
    render_preview(variants)
    print("SOURCE_HEIGHT", round(source_height, 4))
    print("TREE_FAMILY_COMPLETE")


if __name__ == "__main__":
    main()
