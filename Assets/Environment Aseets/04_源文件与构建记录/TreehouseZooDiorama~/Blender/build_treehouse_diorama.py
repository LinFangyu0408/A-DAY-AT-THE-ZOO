import math
import os
import random

import bpy
from mathutils import Vector


ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSET_ROOT = os.path.dirname(ROOT)
BLEND_PATH = os.path.join(ROOT, "TreehouseZooDiorama.blend")
FBX_PATH = os.path.join(
    ASSET_ROOT, "TreehouseZooDiorama", "Meshes", "TreehouseZooDiorama.fbx"
)
PREVIEW_PATH = os.path.join(ROOT, "TreehouseZooDiorama_Preview.png")

random.seed(4077)


def srgb_to_linear(channel):
    if channel <= 0.04045:
        return channel / 12.92
    return ((channel + 0.055) / 1.055) ** 2.4


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def make_material(name, color, roughness=0.82):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    linear_color = tuple(srgb_to_linear(channel) for channel in color)
    mat.diffuse_color = (*linear_color, 1.0)
    nodes = mat.node_tree.nodes
    bsdf = next((node for node in nodes if node.bl_idname == "ShaderNodeBsdfPrincipled"), None)
    if bsdf is None:
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    output = next((node for node in nodes if node.bl_idname == "ShaderNodeOutputMaterial"), None)
    if output is None:
        output = nodes.new("ShaderNodeOutputMaterial")
    for link in list(output.inputs[0].links):
        mat.node_tree.links.remove(link)
    mat.node_tree.links.new(bsdf.outputs["BSDF"], output.inputs[0])
    bsdf.inputs["Base Color"].default_value = (*linear_color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def make_empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.35
    return obj


def register_object(obj, mat=None, parent=None, collection=None, smooth=False):
    if collection is not None:
        for current in list(obj.users_collection):
            current.objects.unlink(obj)
        collection.objects.link(obj)
    if mat is not None:
        for material in mat if isinstance(mat, (list, tuple)) else (mat,):
            obj.data.materials.append(material)
    if hasattr(obj.data, "polygons"):
        for poly in obj.data.polygons:
            poly.use_smooth = smooth
    if parent is not None:
        obj.parent = parent
    return obj


def create_mesh_object(name, vertices, faces, mat, parent=None, material_indices=None):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if isinstance(mat, (list, tuple)):
        for item in mat:
            mesh.materials.append(item)
    elif mat is not None:
        mesh.materials.append(mat)
    for i, poly in enumerate(mesh.polygons):
        poly.use_smooth = False
        if material_indices:
            poly.material_index = material_indices[i]
    if parent is not None:
        obj.parent = parent
    return obj


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_bevel(obj, width=0.04, segments=1):
    if width <= 0:
        return
    activate(obj)
    mod = obj.modifiers.new("Soft low-poly edges", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    bpy.ops.object.modifier_apply(modifier=mod.name)


def add_box(name, location, dimensions, mat, parent, rotation=(0, 0, 0), bevel=0.03):
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    register_object(obj, mat, parent)
    apply_bevel(obj, bevel, 1)
    return obj


def add_beam(name, start, end, width, depth, mat, parent, bevel=0.025):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    length = direction.length
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cube_add(size=1, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (width * 0.5, depth * 0.5, length * 0.5)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    register_object(obj, mat, parent)
    apply_bevel(obj, bevel, 1)
    return obj


def add_cylinder_between(
    name, start, end, radius, mat, parent, vertices=7, radius_end=None, bevel=0.0
):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    length = direction.length
    midpoint = (start + end) * 0.5
    if radius_end is None:
        radius_end = radius
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius,
        radius2=radius_end,
        depth=length,
        end_fill_type="NGON",
        location=midpoint,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    register_object(obj, mat, parent)
    apply_bevel(obj, bevel, 1)
    return obj


def add_ico(name, location, scale, mat, parent, subdivisions=1, rotation=(0, 0, 0), jitter=0.0):
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdivisions,
        radius=1.0,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    if jitter > 0:
        for vert in obj.data.vertices:
            vert.co *= random.uniform(1.0 - jitter, 1.0 + jitter)
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    register_object(obj, mat, parent)
    if isinstance(mat, (list, tuple)) and len(mat) > 1:
        weights = [2, 4, 1][: len(mat)]
        for poly in obj.data.polygons:
            poly.material_index = random.choices(range(len(mat)), weights=weights, k=1)[0]
    return obj


clear_scene()

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.resolution_x = 1440
scene.render.resolution_y = 810
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = PREVIEW_PATH
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"

world = bpy.data.worlds.new("Bright zoo sky") if not bpy.data.worlds else bpy.data.worlds[0]
scene.world = world
world.use_nodes = True
world_nodes = world.node_tree.nodes
background = next((node for node in world_nodes if node.bl_idname == "ShaderNodeBackground"), None)
world_output = next((node for node in world_nodes if node.bl_idname == "ShaderNodeOutputWorld"), None)
if background is None:
    background = world_nodes.new("ShaderNodeBackground")
if world_output is None:
    world_output = world_nodes.new("ShaderNodeOutputWorld")
if not world_output.inputs[0].links:
    world.node_tree.links.new(background.outputs["Background"], world_output.inputs["Surface"])
background.inputs["Color"].default_value = (
    0.34,
    0.63,
    0.90,
    1.0,
)
background.inputs["Strength"].default_value = 0.8

grass = make_material("Grass_Green", (0.32, 0.61, 0.12))
grass_light = make_material("Grass_Sunlit", (0.37, 0.67, 0.15))
grass_dark = make_material("Grass_Shadow", (0.27, 0.53, 0.10))
bark = make_material("Tree_Bark", (0.30, 0.13, 0.045))
bark_light = make_material("Tree_Bark_Sunlit", (0.43, 0.21, 0.075))
bark_dark = make_material("Tree_Bark_Shadow", (0.20, 0.075, 0.025))
wood = make_material("Platform_Wood", (0.47, 0.22, 0.065))
wood_light = make_material("Platform_Wood_Sunlit", (0.62, 0.33, 0.12))
wood_dark = make_material("Fence_Wood_Dark", (0.34, 0.14, 0.035))
iron = make_material("Wrought_Iron_Bolts", (0.08, 0.065, 0.045), 0.58)
leaf_dark = make_material("Foliage_Dark", (0.055, 0.30, 0.045))
leaf_mid = make_material("Foliage_Mid", (0.12, 0.43, 0.07))
leaf_light = make_material("Foliage_Sunlit", (0.24, 0.55, 0.10))
rock_gray = make_material("Rock_Gray", (0.35, 0.36, 0.37))
rock_light = make_material("Rock_Sunlit", (0.52, 0.52, 0.52))
rock_dark = make_material("Rock_Shadow", (0.22, 0.23, 0.24))
mountain = make_material("Distant_Mountains", (0.36, 0.50, 0.58), 0.95)
cloud_white = make_material("Cloud_White", (0.94, 0.96, 0.94), 1.0)

root = make_empty("TreehouseZooDiorama")
ground_root = make_empty("GROUND", root)
hero_root = make_empty("HERO_TREEHOUSE", root)
platform_root = make_empty("TREE_PLATFORM", hero_root)
ladder_root = make_empty("LADDER", hero_root)
fence_root = make_empty("FENCES", root)
rocks_root = make_empty("ROCKS", root)
background_root = make_empty("BACKGROUND_TREES", root)
grass_root = make_empty("GRASS_TUFTS", root)
mountain_root = make_empty("DISTANT_GROUND", root)
bush_root = make_empty("SHRUBS", root)
cloud_root = make_empty("CLOUD_FORMS", root)


def build_ground():
    x_min, x_max = -23.0, 23.0
    y_min, y_max = -18.0, 18.0
    x_step, y_step = 2.0, 2.0
    nx = int((x_max - x_min) / x_step)
    ny = int((y_max - y_min) / y_step)
    verts = []
    for j in range(ny + 1):
        y = y_min + j * y_step
        for i in range(nx + 1):
            x = x_min + i * x_step
            dist = math.sqrt(x * x + y * y)
            edge = max(0.0, (dist - 7.0) / 20.0)
            height = (
                math.sin(x * 0.37) * 0.055
                + math.cos(y * 0.31) * 0.045
                + random.uniform(-0.018, 0.018)
            )
            height *= edge
            verts.append((x, y, height))

    faces = []
    materials_idx = []
    for j in range(ny):
        for i in range(nx):
            a = j * (nx + 1) + i
            b = a + 1
            c = a + nx + 2
            d = a + nx + 1
            if (i + j) % 2:
                faces.extend(((a, b, c), (a, c, d)))
            else:
                faces.extend(((a, b, d), (b, c, d)))
            materials_idx.extend(
                (
                    random.choices((0, 1, 2), weights=(6, 2, 2), k=1)[0],
                    random.choices((0, 1, 2), weights=(6, 2, 2), k=1)[0],
                )
            )
    create_mesh_object(
        "Grass_Terrain",
        verts,
        faces,
        [grass, grass_light, grass_dark],
        ground_root,
        materials_idx,
    )


build_ground()


def build_hero_tree():
    # Faceted trunk with an irregular, broad base and narrower crown.
    segments = 12
    rings = [
        (0.00, 2.15, 0.10),
        (0.62, 1.76, 0.04),
        (1.15, 1.48, -0.03),
        (2.05, 1.42, 0.02),
        (3.15, 1.33, 0.00),
        (4.35, 1.25, -0.02),
        (5.55, 1.20, 0.02),
        (6.65, 1.40, 0.00),
        (7.35, 1.05, 0.03),
    ]
    verts = []
    for ring_i, (z, radius, offset) in enumerate(rings):
        for i in range(segments):
            angle = (math.tau * i / segments) + offset
            variation = 1.0 + 0.065 * math.sin(i * 2.7 + ring_i * 0.8)
            if ring_i == 0:
                variation += 0.18 * math.sin(i * 3.0)
            verts.append(
                (
                    math.cos(angle) * radius * variation,
                    math.sin(angle) * radius * variation,
                    z,
                )
            )
    faces = []
    mats = []
    for r in range(len(rings) - 1):
        for i in range(segments):
            ni = (i + 1) % segments
            a = r * segments + i
            b = r * segments + ni
            c = (r + 1) * segments + ni
            d = (r + 1) * segments + i
            faces.append((a, b, c, d))
            mats.append(random.choices((0, 1, 2), weights=(7, 2, 1), k=1)[0])
    faces.append(tuple(reversed(range(segments))))
    mats.append(2)
    top_start = (len(rings) - 1) * segments
    faces.append(tuple(top_start + i for i in range(segments)))
    mats.append(0)
    create_mesh_object(
        "Hero_Tree_Trunk",
        verts,
        faces,
        [bark, bark_light, bark_dark],
        hero_root,
        mats,
    )

    # Raised bark ridges emphasize the stylized vertical facets.
    for i in range(7):
        angle = math.tau * i / 7 + 0.18
        r0 = 1.10 if i % 2 == 0 else 1.02
        r1 = 1.48 if i % 2 == 0 else 1.35
        add_beam(
            f"Bark_Ridge_{i + 1:02d}",
            (math.cos(angle) * r0, math.sin(angle) * r0, 0.32),
            (math.cos(angle) * r1, math.sin(angle) * r1, 6.2),
            0.12,
            0.12,
            bark_light if i % 2 == 0 else bark_dark,
            hero_root,
            0.01,
        )

    # Broad buttress roots are welded visually into the trunk instead of
    # looking like separate spikes.
    for i in range(7):
        angle = math.tau * i / 7 + 0.15
        tangent = Vector((-math.sin(angle), math.cos(angle), 0))
        sections = [
            (0.95, 0.62, 2.05, 0.10, 0.98),
            (1.60, 0.50, 1.15, 0.05, 0.56),
            (2.55, 0.16, 0.20, 0.02, 0.15),
        ]
        verts = []
        for radius, half_width, top_z, bottom_z, _ in sections:
            center = Vector((math.cos(angle) * radius, math.sin(angle) * radius, 0))
            left = center + tangent * half_width
            right = center - tangent * half_width
            verts.extend(
                (
                    (left.x, left.y, bottom_z),
                    (right.x, right.y, bottom_z),
                    (left.x, left.y, top_z),
                    (right.x, right.y, top_z),
                )
            )
        faces = []
        mats = []
        for section in range(len(sections) - 1):
            a = section * 4
            b = a + 4
            faces.extend(
                (
                    (a, b + 1, b, a + 1),
                    (a + 2, a + 3, b + 3, b + 2),
                    (a, a + 2, b + 2, b),
                    (a + 1, b + 1, b + 3, a + 3),
                )
            )
            mats.extend((0, 1, 0, 1))
        faces.extend(((0, 1, 3, 2), (8, 10, 11, 9)))
        mats.extend((0, 1))
        create_mesh_object(
            f"Buttress_Root_{i + 1:02d}",
            verts,
            faces,
            [bark, bark_light],
            hero_root,
            mats,
        )

    # Large branches holding the low-poly crown.
    branches = [
        (Vector((0.35, 0.1, 5.2)), Vector((-3.6, 0.8, 8.55)), 0.64, 0.24),
        (Vector((-0.1, -0.2, 5.5)), Vector((3.65, -0.5, 8.75)), 0.70, 0.27),
        (Vector((0.1, 0.25, 5.7)), Vector((-0.7, 3.2, 8.95)), 0.58, 0.18),
        (Vector((0.2, 0.0, 5.9)), Vector((1.7, 2.0, 9.10)), 0.48, 0.16),
    ]
    for i, (start, end, r0, r1) in enumerate(branches):
        add_cylinder_between(
            f"Main_Branch_{i + 1:02d}",
            start,
            end,
            r0,
            bark,
            hero_root,
            vertices=7,
            radius_end=r1,
        )

    # The crown is deliberately made from intersecting low-poly forms.
    foliage = [
        ((0.0, 0.0, 9.10), (3.45, 3.10, 2.05), leaf_dark),
        ((-2.5, 0.5, 9.45), (2.75, 2.65, 1.85), leaf_mid),
        ((2.45, -0.1, 9.55), (2.95, 2.75, 1.95), leaf_light),
        ((-0.7, 2.25, 10.00), (2.45, 2.15, 1.65), leaf_mid),
        ((1.6, 1.45, 10.30), (2.25, 2.05, 1.55), leaf_light),
        ((-1.9, -1.65, 9.80), (2.15, 1.95, 1.50), leaf_dark),
        ((0.2, 0.0, 11.10), (2.95, 2.45, 1.65), leaf_mid),
    ]
    for i, (loc, scale, mat) in enumerate(foliage):
        add_ico(
            f"Hero_Canopy_{i + 1:02d}",
            loc,
            scale,
            [mat, leaf_mid, leaf_light],
            hero_root,
            subdivisions=2,
            rotation=(random.uniform(-0.2, 0.2), random.uniform(-0.2, 0.2), i * 0.31),
            jitter=0.055,
        )


build_hero_tree()


def annular_deck(name, inner_radius, outer_radius, z, thickness, segments, mat_a, mat_b, parent):
    gap = 0.007
    verts = []
    faces = []
    mats = []
    for i in range(segments):
        a0 = math.tau * i / segments + gap
        a1 = math.tau * (i + 1) / segments - gap
        i0 = (math.cos(a0) * inner_radius, math.sin(a0) * inner_radius)
        i1 = (math.cos(a1) * inner_radius, math.sin(a1) * inner_radius)
        o0 = (math.cos(a0) * outer_radius, math.sin(a0) * outer_radius)
        o1 = (math.cos(a1) * outer_radius, math.sin(a1) * outer_radius)
        base = len(verts)
        verts.extend(
            (
                (i0[0], i0[1], z + thickness * 0.5),
                (o0[0], o0[1], z + thickness * 0.5),
                (o1[0], o1[1], z + thickness * 0.5),
                (i1[0], i1[1], z + thickness * 0.5),
                (i0[0], i0[1], z - thickness * 0.5),
                (o0[0], o0[1], z - thickness * 0.5),
                (o1[0], o1[1], z - thickness * 0.5),
                (i1[0], i1[1], z - thickness * 0.5),
            )
        )
        faces.extend(
            (
                (base, base + 1, base + 2, base + 3),
                (base + 7, base + 6, base + 5, base + 4),
                (base, base + 4, base + 5, base + 1),
                (base + 1, base + 5, base + 6, base + 2),
                (base + 2, base + 6, base + 7, base + 3),
                (base + 3, base + 7, base + 4, base),
            )
        )
        mats.extend((0 if i % 2 == 0 else 1,) * 6)
    return create_mesh_object(name, verts, faces, [mat_a, mat_b], parent, mats)


def build_platform():
    annular_deck(
        "Treehouse_Deck",
        1.25,
        3.55,
        3.78,
        0.36,
        12,
        wood,
        wood_light,
        platform_root,
    )

    # Outer band and inner trunk collar.
    segments = 20
    verts = []
    faces = []
    mats = []
    for radius, base_z, height, mat_index in (
        (3.58, 3.55, 0.46, 1),
        (1.28, 3.58, 0.38, 0),
    ):
        band_start = len(verts)
        for ring_z in (base_z, base_z + height):
            for i in range(segments):
                angle = math.tau * i / segments
                verts.append((math.cos(angle) * radius, math.sin(angle) * radius, ring_z))
        for i in range(segments):
            ni = (i + 1) % segments
            a = band_start + i
            b = band_start + ni
            c = band_start + segments + ni
            d = band_start + segments + i
            faces.append((a, b, c, d))
            mats.append(mat_index)
    create_mesh_object(
        "Deck_Rim_And_Collar",
        verts,
        faces,
        [wood_dark, wood_light],
        platform_root,
        mats,
    )

    # Support wedges under the deck.
    for i in range(7):
        angle = math.tau * i / 7 + 0.12
        inner = (math.cos(angle) * 1.25, math.sin(angle) * 1.25, 2.95)
        outer = (math.cos(angle) * 2.72, math.sin(angle) * 2.72, 3.58)
        add_beam(
            f"Deck_Support_{i + 1:02d}",
            inner,
            outer,
            0.20,
            0.22,
            wood_dark,
            platform_root,
            0.025,
        )

    # Rafters and a segmented under-ring give the deck visible construction.
    for i in range(12):
        angle = math.tau * i / 12 + 0.04
        add_beam(
            f"Underdeck_Rafter_{i + 1:02d}",
            (math.cos(angle) * 1.30, math.sin(angle) * 1.30, 3.52),
            (math.cos(angle) * 3.28, math.sin(angle) * 3.28, 3.52),
            0.10,
            0.11,
            wood_dark,
            platform_root,
            0.012,
        )
    ring_segments = 16
    for i in range(ring_segments):
        a0 = math.tau * i / ring_segments + 0.035
        a1 = math.tau * (i + 1) / ring_segments - 0.035
        add_beam(
            f"Underdeck_Ring_{i + 1:02d}",
            (math.cos(a0) * 2.24, math.sin(a0) * 2.24, 3.36),
            (math.cos(a1) * 2.24, math.sin(a1) * 2.24, 3.36),
            0.12,
            0.14,
            wood_dark,
            platform_root,
            0.012,
        )

    # Upright posts around the platform, with a clear gap at the ladder.
    for i in range(9):
        angle = math.tau * i / 9 - math.pi * 0.5
        if -1.80 < angle < -1.35:
            continue
        x = math.cos(angle) * 3.16
        y = math.sin(angle) * 3.16
        add_box(
            f"Rail_Post_{i + 1:02d}",
            (x, y, 4.52),
            (0.18, 0.18, 1.48),
            wood,
            platform_root,
            bevel=0.035,
        )
        add_box(
            f"Rail_Post_Cap_{i + 1:02d}",
            (x, y, 5.28),
            (0.25, 0.25, 0.09),
            wood_light,
            platform_root,
            bevel=0.02,
        )
        add_cylinder_between(
            f"Rail_Bolt_{i + 1:02d}",
            (x - 0.02, y - 0.11, 3.91),
            (x + 0.02, y - 0.11, 3.91),
            0.035,
            iron,
            platform_root,
            vertices=8,
        )


build_platform()


def build_ladder():
    x_center = 1.15
    bottom_z = 0.12
    top_z = 3.70
    bottom_y = -2.72
    top_y = -2.33
    rail_offsets = (-0.48, 0.48)
    for i, offset in enumerate(rail_offsets):
        add_beam(
            f"Ladder_Rail_{i + 1:02d}",
            (x_center + offset, bottom_y, bottom_z),
            (x_center + offset, top_y, top_z),
            0.15,
            0.18,
            wood,
            ladder_root,
            0.025,
        )
    rung_count = 10
    for i in range(rung_count):
        t = (i + 0.45) / (rung_count + 0.2)
        z = bottom_z + (top_z - bottom_z) * t
        y = bottom_y + (top_y - bottom_y) * t
        add_beam(
            f"Ladder_Rung_{i + 1:02d}",
            (x_center - 0.46, y, z),
            (x_center + 0.46, y, z),
            0.12,
            0.12,
            wood_light,
            ladder_root,
            0.02,
        )
    add_box(
        "Ladder_Top_Lip",
        (x_center, top_y - 0.07, 3.83),
        (1.18, 0.24, 0.12),
        wood_light,
        ladder_root,
        bevel=0.025,
    )


build_ladder()


def fence_line(name, start, end, parent):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    length = direction.length
    count = max(2, int(length / 1.25) + 1)
    points = [start.lerp(end, i / (count - 1)) for i in range(count)]
    for i, point in enumerate(points):
        post_height = random.uniform(1.04, 1.24)
        add_box(
            f"{name}_Post_{i + 1:02d}",
            (point.x, point.y, 0.04 + post_height * 0.5),
            (0.18, 0.18, post_height),
            wood_dark,
            parent,
            rotation=(0, 0, random.uniform(-0.035, 0.035)),
            bevel=0.025,
        )
        if i % 3 == 0 or i == len(points) - 1:
            add_box(
                f"{name}_Post_Cap_{i + 1:02d}",
                (point.x, point.y, 0.04 + post_height + 0.035),
                (0.25, 0.25, 0.08),
                wood,
                parent,
                bevel=0.015,
            )
    for i in range(len(points) - 1):
        a = points[i]
        b = points[i + 1]
        for rail_i, z in enumerate((0.42, 0.84)):
            add_beam(
                f"{name}_Rail_{i + 1:02d}_{rail_i + 1}",
                (a.x, a.y, z),
                (b.x, b.y, z),
                0.12,
                0.16,
                wood,
                parent,
                0.018,
            )


def build_fences():
    fence_line("Fence_Back_Left", (-14.5, 8.4), (-4.1, 8.9), fence_root)
    fence_line("Fence_Back_Right", (4.0, 8.9), (14.5, 8.2), fence_root)
    fence_line("Fence_Right", (14.5, 8.2), (13.1, -2.2), fence_root)
    fence_line("Fence_Left", (-14.5, 8.4), (-13.2, -2.2), fence_root)
    fence_line("Fence_Front_Left", (-13.2, -2.2), (-8.8, -8.6), fence_root)
    fence_line("Fence_Front_Right", (13.1, -2.2), (8.9, -8.6), fence_root)


build_fences()


def build_rock(name, location, scale, rotation, material):
    add_ico(
        name,
        location,
        scale,
        [material, rock_light, rock_dark],
        rocks_root,
        subdivisions=2,
        rotation=rotation,
        jitter=0.16,
    )
    # A second, subtly offset boulder form prevents a perfectly round silhouette.
    add_ico(
        name + "_Base",
        (location[0] - scale[0] * 0.10, location[1] + scale[1] * 0.12, location[2] - scale[2] * 0.20),
        (scale[0] * 0.82, scale[1] * 0.88, scale[2] * 0.52),
        [rock_dark, material, rock_gray],
        rocks_root,
        subdivisions=1,
        rotation=(rotation[0] * 0.7, rotation[1] * 0.6, rotation[2] + 0.6),
        jitter=0.08,
    )


def build_rocks():
    build_rock("Foreground_Rock_Left", (-6.6, -2.9, 0.95), (2.0, 1.35, 0.90), (0.1, -0.1, 0.2), rock_gray)
    build_rock("Midground_Rock_Left", (-5.8, 4.0, 0.85), (1.35, 1.00, 0.70), (-0.1, 0.1, -0.3), rock_light)
    build_rock("Foreground_Rock_Right", (7.1, -2.1, 0.92), (1.85, 1.25, 0.82), (0.1, 0.1, -0.15), rock_light)
    build_rock("Midground_Rock_Right", (6.5, 5.3, 0.80), (1.25, 0.90, 0.62), (-0.12, 0.15, 0.2), rock_gray)
    build_rock("Small_Rock_Back", (3.6, 6.7, 0.42), (0.78, 0.64, 0.43), (0.05, -0.08, 0.5), rock_dark)
    for i in range(9):
        angle = random.uniform(0, math.tau)
        radius = random.uniform(8.5, 13.5)
        if abs(math.cos(angle) * radius) > 12 and math.sin(angle) * radius < -5:
            continue
        size = random.uniform(0.18, 0.46)
        build_rock(
            f"Small_Rock_{i + 1:02d}",
            (math.cos(angle) * radius, math.sin(angle) * radius, size * 0.45),
            (size * 1.3, size, size * 0.72),
            (random.uniform(-0.2, 0.2), random.uniform(-0.2, 0.2), random.uniform(0, math.tau)),
            random.choice((rock_gray, rock_light, rock_dark)),
        )


build_rocks()


def build_background_tree(name, x, y, scale_factor, variant):
    tree_root = make_empty(name, background_root, (x, y, 0))
    tree_root.scale = (scale_factor, scale_factor, scale_factor)
    height = random.uniform(3.2, 4.7)
    trunk_mid = Vector(
        (
            random.uniform(-0.10, 0.10),
            random.uniform(-0.10, 0.10),
            height * 0.58,
        )
    )
    trunk_top = Vector(
        (
            random.uniform(-0.16, 0.16),
            random.uniform(-0.16, 0.16),
            height,
        )
    )
    add_cylinder_between(
        name + "_Trunk_Lower",
        (0, 0, 0.05),
        trunk_mid,
        0.42,
        bark if variant % 3 else bark_dark,
        tree_root,
        vertices=7,
        radius_end=0.29,
    )
    add_cylinder_between(
        name + "_Trunk_Upper",
        trunk_mid,
        trunk_top,
        0.29,
        bark,
        tree_root,
        vertices=7,
        radius_end=0.16,
    )
    for root_i in range(3):
        angle = root_i * math.tau / 3 + random.uniform(-0.2, 0.2)
        add_cylinder_between(
            f"{name}_Root_{root_i + 1}",
            (0, 0, 0.35),
            (math.cos(angle) * 0.74, math.sin(angle) * 0.74, 0.02),
            0.19,
            bark_dark if root_i % 2 else bark,
            tree_root,
            vertices=5,
            radius_end=0.045,
        )
    branch_ends = [
        (
            random.uniform(-1.05, -0.55),
            random.uniform(-0.45, 0.45),
            height * random.uniform(0.88, 1.02),
        ),
        (
            random.uniform(0.55, 1.05),
            random.uniform(-0.45, 0.45),
            height * random.uniform(0.84, 0.98),
        ),
    ]
    for branch_i, branch_end in enumerate(branch_ends):
        add_cylinder_between(
            f"{name}_Branch_{branch_i + 1}",
            trunk_mid,
            branch_end,
            0.18,
            bark,
            tree_root,
            vertices=5,
            radius_end=0.065,
        )
    palette = (leaf_dark, leaf_mid, leaf_light)
    blobs = [
        ((0.0, 0.0, height + 0.55), (1.85, 1.65, 1.45)),
        ((0.75, -0.1, height + 0.18), (1.45, 1.30, 1.15)),
        ((-0.65, 0.25, height + 0.2), (1.42, 1.35, 1.18)),
        ((0.05, 0.08, height + 1.05), (1.28, 1.18, 1.05)),
    ]
    for i, (loc, size) in enumerate(blobs):
        base_material = palette[(variant + i) % len(palette)]
        add_ico(
            f"{name}_Canopy_{i + 1}",
            loc,
            size,
            [base_material, leaf_mid, leaf_light],
            tree_root,
            subdivisions=2 if i < 2 else 1,
            rotation=(random.uniform(-0.1, 0.1), random.uniform(-0.1, 0.1), random.uniform(0, 1.5)),
            jitter=0.08,
        )


def build_background_trees():
    positions = [
        (-12.5, 4.7, 1.15),
        (-10.4, 7.0, 0.95),
        (-8.0, 4.8, 0.82),
        (-5.4, 7.2, 1.08),
        (4.9, 7.0, 1.12),
        (7.5, 5.5, 1.03),
        (9.7, 7.2, 0.96),
        (12.4, 4.2, 1.20),
        (-15.2, 1.7, 1.28),
        (15.0, 1.1, 1.25),
        (-11.9, -5.0, 0.95),
        (12.2, -5.3, 1.02),
        (-7.7, 10.4, 1.35),
        (-3.4, 11.1, 1.18),
        (1.5, 11.5, 1.30),
        (6.2, 10.8, 1.24),
        (10.5, 10.2, 1.10),
    ]
    for i, (x, y, scale_factor) in enumerate(positions):
        build_background_tree(f"Background_Tree_{i + 1:02d}", x, y, scale_factor, i)
    for i in range(14):
        side = -1 if i % 2 == 0 else 1
        x = side * random.uniform(17.5, 21.5)
        y = random.uniform(-10.0, 12.0)
        build_background_tree(
            f"Outer_Tree_{i + 1:02d}",
            x,
            y,
            random.uniform(1.15, 1.55),
            i + 4,
        )


build_background_trees()


def build_shrubs():
    placements = [
        (-9.8, 1.7, 0.72),
        (-8.4, -0.8, 0.58),
        (9.3, 1.8, 0.66),
        (10.6, -0.9, 0.56),
        (-5.0, 7.0, 0.60),
        (5.3, 7.2, 0.66),
        (-13.8, -4.2, 0.74),
        (13.5, -4.0, 0.70),
    ]
    for i, (x, y, size) in enumerate(placements):
        shrub = make_empty(f"Shrub_{i + 1:02d}", bush_root, (x, y, 0))
        shrub.scale = (1.0, 1.0, 1.0)
        blobs = [
            ((-0.36, 0.02, 0.46), (0.62, 0.58, 0.48)),
            ((0.28, 0.12, 0.50), (0.70, 0.60, 0.53)),
            ((0.00, -0.28, 0.58), (0.55, 0.58, 0.42)),
        ]
        for j, (loc, scale) in enumerate(blobs):
            add_ico(
                f"Shrub_{i + 1:02d}_Lobe_{j + 1}",
                loc,
                tuple(value * size for value in scale),
                [leaf_dark, leaf_mid, leaf_light],
                shrub,
                subdivisions=1,
                rotation=(0, 0, random.uniform(0, math.tau)),
                jitter=0.10,
            )


build_shrubs()


def add_grass_tuft(name, x, y, size, angle_seed):
    verts = []
    faces = []
    for i in range(3):
        angle = angle_seed + i * math.tau / 3
        dx = math.cos(angle) * size * 0.30
        dy = math.sin(angle) * size * 0.30
        zlean_x = math.cos(angle + 0.5) * size * 0.18
        zlean_y = math.sin(angle + 0.5) * size * 0.18
        base = len(verts)
        w = size * 0.13
        verts.extend(
            (
                (x - dx * 0.35 - dy * 0.25, y - dy * 0.35 + dx * 0.25, 0.02),
                (x + dx * 0.35 + dy * 0.25, y + dy * 0.35 - dx * 0.25, 0.02),
                (x + zlean_x, y + zlean_y, size),
                (x - zlean_x * 0.8, y - zlean_y * 0.8, size * 0.92),
            )
        )
        faces.extend(
            (
                (base, base + 1, base + 2),
                (base, base + 3, base + 1),
                (base, base + 2, base + 3),
                (base + 1, base + 3, base + 2),
            )
        )
    create_mesh_object(
        name,
        verts,
        faces,
        random.choice((grass_light, grass_dark, grass_light)),
        grass_root,
    )


def build_grass():
    for i in range(58):
        angle = random.uniform(0, math.tau)
        radius = random.uniform(3.7, 14.0)
        x = math.cos(angle) * radius
        y = math.sin(angle) * radius
        add_grass_tuft(
            f"Grass_Tuft_{i + 1:02d}",
            x,
            y,
            random.uniform(0.20, 0.48),
            random.uniform(0, math.tau),
        )


build_grass()


def build_mountains():
    # A distant faceted ridge fills the horizon without requiring a custom skybox.
    x_positions = [-25, -21, -18, -15, -12, -9, -6, -3, 0, 3, 6, 9, 12, 15, 18, 21, 25]
    heights = [2.8, 4.6, 3.8, 5.8, 4.2, 5.0, 3.4, 4.5, 3.8, 5.4, 4.4, 5.2, 3.6, 4.9, 3.8, 4.4, 2.7]
    y_front = 15.8
    y_back = 18.3
    z_bottom = -0.2
    verts = []
    for x, h in zip(x_positions, heights):
        verts.extend(
            (
                (x, y_front, z_bottom),
                (x, y_front, h),
                (x, y_back, max(0.4, h * 0.55)),
            )
        )
    faces = []
    for i in range(len(x_positions) - 1):
        a = i * 3
        b = (i + 1) * 3
        faces.extend(
            (
                (a, b, b + 1, a + 1),
                (a + 1, b + 1, b + 2, a + 2),
                (a + 2, b + 2, b, a),
            )
        )
    create_mesh_object("Distant_Mountain_Ridge", verts, faces, mountain, mountain_root)


build_mountains()


def build_clouds():
    clusters = [
        (-15.0, 12.0, 10.4, 1.20),
        (14.0, 14.0, 10.8, 1.35),
        (0.0, 21.0, 12.0, 1.15),
    ]
    for i, (cx, cy, cz, size) in enumerate(clusters):
        cloud = make_empty(f"Cloud_{i + 1:02d}", cloud_root, (cx, cy, cz))
        cloud.scale = (size, size, size)
        forms = [
            ((-1.7, 0.0, 0.0), (1.20, 0.50, 0.42)),
            ((-0.55, 0.08, 0.20), (1.05, 0.55, 0.55)),
            ((0.65, 0.0, 0.17), (1.15, 0.54, 0.50)),
            ((1.65, -0.05, 0.0), (0.90, 0.45, 0.35)),
        ]
        for j, (loc, scale) in enumerate(forms):
            add_ico(
                f"Cloud_{i + 1:02d}_Puff_{j + 1}",
                loc,
                scale,
                cloud_white,
                cloud,
                subdivisions=1,
                rotation=(0, 0, random.uniform(-0.2, 0.2)),
                jitter=0.06,
            )


build_clouds()


def build_camera_and_lights():
    camera_data = bpy.data.cameras.new("Preview_Camera")
    camera = bpy.data.objects.new("Preview_Camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (14.5, -23.5, 8.3)
    target = Vector((0.0, 0.0, 4.35))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 46
    camera_data.sensor_width = 36
    scene.camera = camera

    sun_data = bpy.data.lights.new("Warm_Sun", "SUN")
    sun_data.energy = 3.8
    sun_data.angle = math.radians(8)
    sun_data.color = (1.0, 0.88, 0.70)
    sun = bpy.data.objects.new("Warm_Sun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(48), math.radians(-18), math.radians(-32))

    fill_data = bpy.data.lights.new("Sky_Fill", "AREA")
    fill_data.energy = 900
    fill_data.shape = "DISK"
    fill_data.size = 15
    fill_data.color = (0.58, 0.76, 1.0)
    fill = bpy.data.objects.new("Sky_Fill", fill_data)
    scene.collection.objects.link(fill)
    fill.location = (-8, -5, 12)
    fill.rotation_euler = (math.radians(-35), 0, math.radians(-35))


build_camera_and_lights()

os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

bpy.ops.object.select_all(action="DESELECT")
for obj in bpy.context.scene.objects:
    if obj.type in {"MESH", "EMPTY"} and obj.name not in {"Preview_Camera"}:
        obj.select_set(True)
bpy.context.view_layer.objects.active = root

bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"MESH", "EMPTY"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_anim=False,
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    path_mode="AUTO",
    embed_textures=True,
)

bpy.ops.render.render(write_still=True)

mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]
vertices = sum(len(obj.data.vertices) for obj in mesh_objects)
triangles = sum(len(obj.data.loop_triangles) for obj in mesh_objects)
for obj in mesh_objects:
    obj.data.calc_loop_triangles()
triangles = sum(len(obj.data.loop_triangles) for obj in mesh_objects)

print(
    "TREEHOUSE_BUILD_OK "
    f"objects={len(mesh_objects)} vertices={vertices} triangles={triangles} "
    f"blend={BLEND_PATH} fbx={FBX_PATH} preview={PREVIEW_PATH}"
)
