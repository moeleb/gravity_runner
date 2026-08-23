import bpy
import sys
from pathlib import Path
from mathutils import Vector


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


def point_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


bpy.ops.wm.open_mainfile(filepath=argument_after("--blend"))
output = Path(argument_after("--output"))
output.parent.mkdir(parents=True, exist_ok=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 500
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.world.color = (0.015, 0.02, 0.04)

camera_data = bpy.data.cameras.new("Front Camera")
camera = bpy.data.objects.new("Front Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.location = (0.0, -4.0, 0.0)
camera.data.lens = 58
point_at(camera, Vector((0, 0, 0)))
scene.render.filepath = str(output)
bpy.ops.render.render(write_still=True)

