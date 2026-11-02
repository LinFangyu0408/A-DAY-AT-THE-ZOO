"""Build large recursive tree variants from samor_4077_133.

Each child is the source tree repeated at a smaller scale and attached to a
higher node of its parent. The operation recurses, producing self-similar
trees while preserving the original low-poly mesh, UVs and material.
"""

from pathlib import Path
import math
import random
import shutil

import bpy
from mathutils import Matrix, Quaternion, Vector


HERE = Path(__file__).resolve().parent
TREE_ROOT = HERE.parent
SOURCE_FBX = (
    TREE_ROOT.parent
    / "4077_卡通低面森林场景"
    / "Mountain Environment.fbx"
)
FBX_DIR = TREE_ROOT / "FBX"
PREVIEW_DIR = TREE_ROOT / "Previews"


VARIANTS = (
    {
        "name": "FractalTowering",
        "height": 1.70,
        "width": 0.92,
        "crown": 1.04,
        "depth": 2,
        "branches": 3,
        "child": 0.48,
        "decay": 0.62,
        "seed": 4101,
    },
    {
        "name": "FractalBroad",
        "height": 1.52,
        "width": 1.30,
        "crown": 1.30,
        "depth": 2,
        "branches": 4,
        "child": 0.43,
        "decay": 0.60,
        "seed": 4207,
    },
    {
        "name": "FractalAncient",
        "height": 1.92,
        "width": 1.10,
        "crown": 1.16,
        "depth": 3,
        "branches": 2,
        "child": 0.42,
        "decay": 0.65,
        "seed": 4319,
    },
    {
        "name": "FractalSpire",
        "height": 2.18,
        "width": 0.78,
        "crown": 0.90,
        "depth": 3,
        "branches": 2,
        "child": 0.37,
        "decay": 0.67,
        "seed": 4423,
    },
)


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
    mesh.transform(source.matrix_world)
    mins = Vector((
        min(vertex.co.x for vertex in mesh.vertices),
        min(vertex.co.y for vertex in mesh.vertices),
        min(vertex.co.z for vertex in mesh.vertices),
    ))
    maxs = Vector((
        max(vertex.co.x for vertex in mesh.vertices),
        max(vertex.co.y for vertex in mesh.vertices),
        max(vertex.co.z for vertex in mesh.vertices),
    ))
    center = (mins + maxs) * 0.5
    height = maxs.z - mins.z
    for vertex in mesh.vertices:
        vertex.co.x -= center.x
        vertex.co.y -= center.y
        vertex.co.z -= mins.z

    material = source.data.materials[0] if source.data.materials else None
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    return mesh, material, height


def deform_crown(mesh, height, params):
    for vertex in mesh.vertices:
        t = max(0.0, min(1.0, vertex.co.z / height))
        crown = t * t * (3.0 - 2.0 * t)
        radial = 1.0 + (params["crown"] - 1.0) * crown
        vertex.co.x *= radial
        vertex.co.y *= radial
        vertex.co.z *= 1.0 + math.sin(t * math.pi) * 0.035
    mesh.update()


def append_copy(source_mesh, material, transform, objects, name):
    mesh = source_mesh.copy()
    mesh.transform(transform)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if material is not None:
        obj.data.materials.clear()
        obj.data.materials.append(material)
    objects.append(obj)
    return [[obj, transform]]


def branch_matrix(anchor, angle, tilt, scale):
    direction = Vector((math.cos(angle) * tilt, math.sin(angle) * tilt, 1.0)).normalized()
    rotation = Vector((0.0, 0.0, 1.0)).rotation_difference(direction)
    return (
        Matrix.Translation(anchor)
        @ rotation.to_matrix().to_4x4()
        @ Matrix.Scale(scale, 4)
    )


def build_variant(source_mesh, material, source_height, params):
    rng = random.Random(params["seed"])
    crown_mesh = source_mesh.copy()
    deform_crown(crown_mesh, source_height, params)
    root_scale = Matrix.Diagonal((
        params["width"],
        params["width"],
        params["height"],
        1.0,
    ))

    objects = []
    root = bpy.data.objects.new(
        "Fractal " + params["name"],
        crown_mesh.copy(),
    )
    crown_mesh.transform(root_scale)
    root.data = crown_mesh
    bpy.context.scene.collection.objects.link(root)
    if material is not None:
        root.data.materials.append(material)
    objects.append(root)

    def recurse(parent_matrix, level, child_scale):
        if level >= params["depth"]:
            return
        count = params["branches"] if level == 0 else 2
        for index in range(count):
            angle = (
                index * math.tau / count
                + rng.uniform(-0.28, 0.28)
                + level * 0.61
            )
            height_fraction = rng.uniform(0.54, 0.82) - level * 0.015
            radius = max(0.45, min(2.4, source_height * 0.105))
            anchor = Vector((
                math.cos(angle) * radius,
                math.sin(angle) * radius,
                source_height * params["height"] * height_fraction,
            ))
            transform = (
                parent_matrix
                @ branch_matrix(anchor, angle, rng.uniform(0.35, 0.72), child_scale)
            )
            append_copy(
                crown_mesh,
                material,
                transform,
                objects,
                f"{params['name']} branch {level}-{index}",
            )
            recurse(transform, level + 1, child_scale * params["decay"])

    main_matrix = Matrix.Identity(4)
    recurse(main_matrix, 0, params["child"])

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = bpy.context.view_layer.objects.active
    result.name = "samor_4077_133_" + params["name"]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    result.data.materials.clear()
    if material is not None:
        result.data.materials.append(material)
    return result


def export_variants(source_mesh, material, source_height):
    FBX_DIR.mkdir(parents=True, exist_ok=True)
    variants = []
    for params in VARIANTS:
        obj = build_variant(source_mesh, material, source_height, params)
        variants.append(obj)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        path = FBX_DIR / f"{obj.name}.fbx"
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
            len(obj.data.vertices),
            "dims=",
            tuple(round(value, 3) for value in obj.dimensions),
        )
    return variants


def render_preview(variants):
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    for index, obj in enumerate(variants):
        obj.location.x = (index - 1.5) * 23.0

    bpy.ops.mesh.primitive_plane_add(size=130, location=(0, 0, -0.15))
    ground = bpy.context.object
    ground.name = "Fractal Preview Ground"
    ground_material = bpy.data.materials.new("Fractal Preview Ground")
    ground_material.diffuse_color = (0.07, 0.09, 0.075, 1.0)
    ground.data.materials.append(ground_material)

    bpy.ops.object.camera_add(location=(0, -92, 23))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 118.0
    target = Vector((0, 0, 18))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera

    for location, energy, size in (
        ((-30, -38, 54), 1800, 32),
        ((34, 5, 40), 1200, 36),
        ((0, 34, 30), 1400, 28),
    ):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (
            Vector((0, 0, 15)) - light.location
        ).to_track_quat("-Z", "Y").to_euler()

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 2000
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world.color = (0.035, 0.055, 0.065)
    scene.render.filepath = str(PREVIEW_DIR / "samor_fractal_giants.png")
    bpy.ops.render.render(write_still=True)


def main():
    reset_scene()
    source_mesh, material, source_height = import_source()
    variants = export_variants(source_mesh, material, source_height)
    render_preview(variants)
    print("FRACTAL_TREES_COMPLETE")


if __name__ == "__main__":
    main()
