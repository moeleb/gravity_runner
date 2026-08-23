import bpy
import json
import sys
from pathlib import Path
from mathutils import Vector


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


def add_bone(data, name, head, tail, parent=None, deform=True):
    bone = data.edit_bones.new(name)
    bone.head = Vector(head)
    bone.tail = Vector(tail)
    bone.use_deform = deform
    if parent:
        bone.parent = data.edit_bones[parent]


source_blend = Path(argument_after("--blend"))
output_blend = Path(argument_after("--output-blend"))
output_fbx = Path(argument_after("--fbx"))
report_path = Path(argument_after("--report"))
bpy.ops.wm.open_mainfile(filepath=str(source_blend))

mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
mesh.name = "Nova_Girl_Mobile_Mesh"
mesh.vertex_groups.clear()
for modifier in list(mesh.modifiers):
    mesh.modifiers.remove(modifier)
mesh.parent = None

data = bpy.data.armatures.new("Nova_Girl_Humanoid")
rig = bpy.data.objects.new("Nova_Girl_Humanoid", data)
bpy.context.scene.collection.objects.link(rig)
rig.show_in_front = True
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")

add_bone(data, "Root", (0, 0, -0.91), (0, 0, -0.79), deform=False)
add_bone(data, "Hips", (0, 0, -0.22), (0, 0, -0.04), "Root")
add_bone(data, "Spine", (0, 0, -0.04), (0, 0, 0.13), "Hips")
add_bone(data, "Chest", (0, 0, 0.13), (0, 0, 0.30), "Spine")
add_bone(data, "UpperChest", (0, 0, 0.30), (0, 0, 0.39), "Chest")
add_bone(data, "Neck", (0, -0.005, 0.39), (0, -0.015, 0.49), "UpperChest")
add_bone(data, "Head", (0, -0.015, 0.49), (0, -0.02, 0.83), "Neck")
add_bone(data, "LeftShoulder", (0.05, 0, 0.35), (0.18, -0.005, 0.32), "UpperChest")
add_bone(data, "LeftUpperArm", (0.18, -0.005, 0.32), (0.285, -0.055, 0.10), "LeftShoulder")
add_bone(data, "LeftLowerArm", (0.285, -0.055, 0.10), (0.235, -0.105, -0.10), "LeftUpperArm")
add_bone(data, "LeftHand", (0.235, -0.105, -0.10), (0.19, -0.13, -0.18), "LeftLowerArm")
add_bone(data, "RightShoulder", (-0.05, 0, 0.35), (-0.18, 0.0, 0.31), "UpperChest")
add_bone(data, "RightUpperArm", (-0.18, 0.0, 0.31), (-0.285, -0.015, 0.06), "RightShoulder")
add_bone(data, "RightLowerArm", (-0.285, -0.015, 0.06), (-0.29, -0.035, -0.19), "RightUpperArm")
add_bone(data, "RightHand", (-0.29, -0.035, -0.19), (-0.285, -0.055, -0.29), "RightLowerArm")
add_bone(data, "LeftUpperLeg", (0.105, 0, -0.20), (0.13, 0.0, -0.49), "Hips")
add_bone(data, "LeftLowerLeg", (0.13, 0.0, -0.49), (0.13, -0.015, -0.76), "LeftUpperLeg")
add_bone(data, "LeftFoot", (0.13, -0.015, -0.76), (0.13, -0.16, -0.90), "LeftLowerLeg")
add_bone(data, "LeftToes", (0.13, -0.16, -0.90), (0.13, -0.28, -0.90), "LeftFoot")
add_bone(data, "RightUpperLeg", (-0.105, 0, -0.20), (-0.13, 0.0, -0.49), "Hips")
add_bone(data, "RightLowerLeg", (-0.13, 0.0, -0.49), (-0.13, -0.015, -0.76), "RightUpperLeg")
add_bone(data, "RightFoot", (-0.13, -0.015, -0.76), (-0.13, -0.16, -0.90), "RightLowerLeg")
add_bone(data, "RightToes", (-0.13, -0.16, -0.90), (-0.13, -0.28, -0.90), "RightFoot")
bpy.ops.object.mode_set(mode="OBJECT")

bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")

output_blend.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(output_fbx), use_selection=True, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
    object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True,
    add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
    path_mode="COPY", embed_textures=True,
)

report = {
    "source": str(source_blend), "blend": str(output_blend), "fbx": str(output_fbx),
    "vertices": len(mesh.data.vertices), "bones": [bone.name for bone in data.bones],
}
report_path.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

