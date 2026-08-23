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
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 500
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True

camera_data = bpy.data.cameras.new("Final Camera")
camera = bpy.data.objects.new("Final Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.location = (0.0, -4.0, 0.0)
camera.data.lens = 58
point_at(camera, Vector((0, 0, 0)))

for name, location, energy, color in (
    ("Key", (-3.0, -4.0, 4.0), 1000, (0.80, 0.92, 1.0)),
    ("Fill", (3.2, -2.0, 2.0), 750, (1.0, 0.45, 0.25)),
    ("Rim", (0.0, 3.0, 3.0), 900, (0.25, 0.70, 1.0)),
):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = 3.0
    light = bpy.data.objects.new(name, data)
    light.location = location
    point_at(light, Vector((0, 0, 0)))
    scene.collection.objects.link(light)

scene.render.filepath = str(output)
bpy.ops.render.render(write_still=True)

