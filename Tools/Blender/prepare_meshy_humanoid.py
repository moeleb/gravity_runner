import bpy
import json
import sys
from pathlib import Path
from mathutils import Vector


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


def add_bone(armature, name, head, tail, parent=None, deform=True):
    bone = armature.edit_bones.new(name)
    bone.head = Vector(head)
    bone.tail = Vector(tail)
    bone.use_deform = deform
    if parent:
        bone.parent = armature.edit_bones[parent]
    return bone


source = Path(argument_after("--source"))
fbx_output = Path(argument_after("--fbx"))
blend_output = Path(argument_after("--blend"))
report_output = Path(argument_after("--report"))
target_triangles = int(argument_after("--triangles"))
texture_size = int(argument_after("--texture-size"))

for path in (fbx_output, blend_output, report_output):
    path.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))

meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(meshes) != 1:
    raise RuntimeError(f"Expected one character mesh, found {len(meshes)}")
character = meshes[0]
character.name = "Nova_Girl_Mesh"

# Reduce the source's excessive 426k triangles while preserving its UV layout/material.
character.data.calc_loop_triangles()
source_triangles = len(character.data.loop_triangles)
ratio = min(1.0, target_triangles / max(1, source_triangles))
modifier = character.modifiers.new("Mobile_Retopology", "DECIMATE")
modifier.decimate_type = "COLLAPSE"
modifier.ratio = ratio
modifier.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = character
character.select_set(True)
bpy.ops.object.modifier_apply(modifier=modifier.name)

# Downscale embedded 2K/4K maps. This is the largest file-size and runtime-memory saving.
for image in bpy.data.images:
    width, height = image.size
    if width <= 0 or height <= 0 or image.name in {"Render Result", "Viewer Node"}:
        continue
    scale = min(1.0, texture_size / max(width, height))
    if scale < 1.0:
        image.scale(max(1, round(width * scale)), max(1, round(height * scale)))
    image.pack()

# Custom humanoid skeleton fitted to this particular bent-arm bind pose.
armature_data = bpy.data.armatures.new("Nova_Girl_Humanoid")
rig = bpy.data.objects.new("Nova_Girl_Humanoid", armature_data)
bpy.context.scene.collection.objects.link(rig)
rig.show_in_front = True
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")

add_bone(armature_data, "Root", (0, 0, -0.91), (0, 0, -0.79), deform=False)
add_bone(armature_data, "Hips", (0, 0, -0.22), (0, 0, -0.04), "Root")
add_bone(armature_data, "Spine", (0, 0, -0.04), (0, 0, 0.13), "Hips")
add_bone(armature_data, "Chest", (0, 0, 0.13), (0, 0, 0.30), "Spine")
add_bone(armature_data, "UpperChest", (0, 0, 0.30), (0, 0, 0.39), "Chest")
add_bone(armature_data, "Neck", (0, -0.005, 0.39), (0, -0.015, 0.49), "UpperChest")
add_bone(armature_data, "Head", (0, -0.015, 0.49), (0, -0.02, 0.83), "Neck")

# Character-left arm (screen-right) is bent toward the pocket in the supplied model.
add_bone(armature_data, "LeftShoulder", (0.05, 0, 0.35), (0.18, -0.005, 0.32), "UpperChest")
add_bone(armature_data, "LeftUpperArm", (0.18, -0.005, 0.32), (0.285, -0.055, 0.10), "LeftShoulder")
add_bone(armature_data, "LeftLowerArm", (0.285, -0.055, 0.10), (0.235, -0.105, -0.10), "LeftUpperArm")
add_bone(armature_data, "LeftHand", (0.235, -0.105, -0.10), (0.19, -0.13, -0.18), "LeftLowerArm")

# Character-right arm (screen-left) hangs naturally beside the body.
add_bone(armature_data, "RightShoulder", (-0.05, 0, 0.35), (-0.18, 0.0, 0.31), "UpperChest")
add_bone(armature_data, "RightUpperArm", (-0.18, 0.0, 0.31), (-0.285, -0.015, 0.06), "RightShoulder")
add_bone(armature_data, "RightLowerArm", (-0.285, -0.015, 0.06), (-0.29, -0.035, -0.19), "RightUpperArm")
add_bone(armature_data, "RightHand", (-0.29, -0.035, -0.19), (-0.285, -0.055, -0.29), "RightLowerArm")

add_bone(armature_data, "LeftUpperLeg", (0.105, 0, -0.20), (0.13, 0.0, -0.49), "Hips")
add_bone(armature_data, "LeftLowerLeg", (0.13, 0.0, -0.49), (0.13, -0.015, -0.76), "LeftUpperLeg")
add_bone(armature_data, "LeftFoot", (0.13, -0.015, -0.76), (0.13, -0.16, -0.90), "LeftLowerLeg")
add_bone(armature_data, "LeftToes", (0.13, -0.16, -0.90), (0.13, -0.28, -0.90), "LeftFoot")
add_bone(armature_data, "RightUpperLeg", (-0.105, 0, -0.20), (-0.13, 0.0, -0.49), "Hips")
add_bone(armature_data, "RightLowerLeg", (-0.13, 0.0, -0.49), (-0.13, -0.015, -0.76), "RightUpperLeg")
add_bone(armature_data, "RightFoot", (-0.13, -0.015, -0.76), (-0.13, -0.16, -0.90), "RightLowerLeg")
add_bone(armature_data, "RightToes", (-0.13, -0.16, -0.90), (-0.13, -0.28, -0.90), "RightFoot")

bpy.ops.object.mode_set(mode="OBJECT")

# Blender's heat weighting supplies a first production-test skin without altering source data.
bpy.ops.object.select_all(action="DESELECT")
character.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
try:
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    weight_method = "automatic_heat_weights"
except RuntimeError as error:
    # Keep a valid export/report if heat weighting cannot solve this generated topology.
    modifier = character.modifiers.new("Humanoid Armature", "ARMATURE")
    modifier.object = rig
    character.parent = rig
    weight_method = f"failed: {error}"

character.data.calc_loop_triangles()
final_triangles = len(character.data.loop_triangles)

bpy.ops.wm.save_as_mainfile(filepath=str(blend_output))

bpy.ops.object.select_all(action="DESELECT")
character.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(fbx_output),
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    use_armature_deform_only=True,
    bake_anim=False,
    path_mode="COPY",
    embed_textures=True,
)

report = {
    "source": str(source),
    "source_triangles": source_triangles,
    "final_triangles": final_triangles,
    "target_triangles": target_triangles,
    "texture_limit": texture_size,
    "weight_method": weight_method,
    "bones": [bone.name for bone in armature_data.bones],
    "fbx": str(fbx_output),
    "blend": str(blend_output),
}
report_output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

