import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


def point_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


blend_path = Path(argument_after("--blend"))
output_dir = Path(argument_after("--output"))
output_dir.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(blend_path))

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
scene.render.film_transparent = False
scene.world.color = (0.015, 0.02, 0.04)

mesh = next(obj for obj in scene.objects if obj.type == "MESH")
for slot in mesh.material_slots:
    if slot.material:
        slot.material.diffuse_color = (0.08, 0.62, 0.86, 1.0)

camera_data = bpy.data.cameras.new("Rig Validation Camera")
camera = bpy.data.objects.new("Rig Validation Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera.location = (0.0, -4.0, 0.0)
camera.data.lens = 58
point_at(camera, Vector((0, 0, 0)))

scene.render.filepath = str(output_dir / "rest-pose.png")
bpy.ops.render.render(write_still=True)

rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
for bone in rig.pose.bones:
    bone.rotation_mode = "XYZ"
rig.pose.bones["Head"].rotation_euler[2] = math.radians(14)
rig.pose.bones["Chest"].rotation_euler[2] = math.radians(-8)
rig.pose.bones["RightUpperArm"].rotation_euler[1] = math.radians(-32)
rig.pose.bones["RightLowerArm"].rotation_euler[0] = math.radians(28)
rig.pose.bones["LeftUpperArm"].rotation_euler[1] = math.radians(24)
rig.pose.bones["LeftLowerArm"].rotation_euler[0] = math.radians(-20)
rig.pose.bones["LeftUpperLeg"].rotation_euler[0] = math.radians(-15)
rig.pose.bones["RightLowerLeg"].rotation_euler[0] = math.radians(18)

scene.render.filepath = str(output_dir / "deformed-pose.png")
bpy.ops.render.render(write_still=True)

