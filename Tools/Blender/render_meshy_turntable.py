import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


def point_camera(camera, target: Vector):
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


source = Path(argument_after("--source"))
output_dir = Path(argument_after("--output"))
output_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 500
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True
scene.world.color = (0.015, 0.015, 0.025)

camera_data = bpy.data.cameras.new("Inspection Camera")
camera = bpy.data.objects.new("Inspection Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.lens = 58

views = {
    "front": (0.0, -4.0, 0.0),
    "back": (0.0, 4.0, 0.0),
    "left": (-4.0, 0.0, 0.0),
    "right": (4.0, 0.0, 0.0),
}

for name, position in views.items():
    camera.location = position
    point_camera(camera, Vector((0.0, 0.0, 0.0)))
    scene.render.filepath = str(output_dir / f"meshy-girl-{name}.png")
    bpy.ops.render.render(write_still=True)
