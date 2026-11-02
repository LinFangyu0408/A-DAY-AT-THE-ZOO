import math
import os

import bpy
from mathutils import Vector


OUTPUT_DIR = os.path.join(os.path.dirname(os.path.dirname(__file__)), "QA")
os.makedirs(OUTPUT_DIR, exist_ok=True)

scene = bpy.context.scene
camera = bpy.data.objects.get("Preview_Camera")
target = Vector((0.0, 0.0, 4.6))

scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 1000
scene.render.resolution_y = 620
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False

angles = {
    "front": math.radians(-90),
    "three_quarter": math.radians(-56),
    "side": math.radians(0),
    "back_three_quarter": math.radians(58),
}

for name, angle in angles.items():
    camera.location = (
        math.cos(angle) * 27.0,
        math.sin(angle) * 27.0,
        8.4,
    )
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.render.filepath = os.path.join(OUTPUT_DIR, f"Treehouse_{name}.png")
    bpy.ops.render.render(write_still=True)

print("TREEHOUSE_TURNTABLE_OK " + OUTPUT_DIR)
