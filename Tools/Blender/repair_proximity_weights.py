import bpy
import bmesh
import json
import math
import sys
from pathlib import Path
from mathutils import Vector


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


def distance_to_segment(point: Vector, start: Vector, end: Vector) -> float:
    segment = end - start
    denominator = segment.length_squared
    if denominator <= 1e-10:
        return (point - start).length
    amount = max(0.0, min(1.0, (point - start).dot(segment) / denominator))
    return (point - (start + segment * amount)).length


blend_path = Path(argument_after("--blend"))
fbx_output = Path(argument_after("--fbx"))
report_output = Path(argument_after("--report"))
bpy.ops.wm.open_mainfile(filepath=str(blend_path))

mesh_object = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")

mesh_object.vertex_groups.clear()
deform_bones = [bone for bone in rig.data.bones if bone.use_deform]
group_indices = {
    bone.name: mesh_object.vertex_groups.new(name=bone.name).index for bone in deform_bones
}
segments = {
    bone.name: (bone.head_local.copy(), bone.tail_local.copy()) for bone in deform_bones
}


def nearest_weights(point, bone_names, influence_count=3):
    distances = []
    for name in bone_names:
        start, end = segments[name]
        distances.append((distance_to_segment(point, start, end), name))
    nearest = sorted(distances)[:influence_count]
    raw = [(name, 1.0 / math.pow(distance + 0.018, 3.4)) for distance, name in nearest]
    total = sum(weight for _, weight in raw)
    return [(name, weight / total) for name, weight in raw]

bm = bmesh.new()
bm.from_mesh(mesh_object.data)
deform_layer = bm.verts.layers.deform.verify()

for vertex in bm.verts:
    point = vertex.co

    # Keep the oversized chibi head rigid. Blending its face/cap between neck and chest
    # causes visible tearing even for small head turns.
    if point.z > 0.36:
        candidates = [("Head", 1.0)]
    # Long fused hair hangs behind and around the shoulders but must follow the head.
    elif point.z > 0.04 and (point.y > 0.075 or abs(point.x) > 0.325):
        candidates = [("Head", 0.92), ("Neck", 0.08)]
    # Treat the chunky shoes as rigid foot pieces; the foot already inherits leg motion.
    elif point.z < -0.71:
        side = "Left" if point.x >= 0 else "Right"
        candidates = [(side + "Foot", 0.94), (side + "Toes", 0.06)]
    # Keep each leg on its own side and blend only across its local chain.
    elif point.z < -0.18:
        side = "Left" if point.x >= 0 else "Right"
        candidates = nearest_weights(point, [
            side + "UpperLeg", side + "LowerLeg", side + "Foot", "Hips"
        ])
    # Sleeves/hands blend only along their matching arm chain.
    elif abs(point.x) > 0.18:
        side = "Left" if point.x >= 0 else "Right"
        candidates = nearest_weights(point, [
            side + "Shoulder", side + "UpperArm", side + "LowerArm", side + "Hand"
        ])
    else:
        candidates = nearest_weights(
            point, ["Hips", "Spine", "Chest", "UpperChest", "Neck"], influence_count=3
        )

    deform = vertex[deform_layer]
    for name, weight in candidates:
        if weight > 0.001:
            deform[group_indices[name]] = weight

bm.to_mesh(mesh_object.data)
bm.free()
mesh_object.data.update()

if not any(mod.type == "ARMATURE" and mod.object == rig for mod in mesh_object.modifiers):
    modifier = mesh_object.modifiers.new("Humanoid Armature", "ARMATURE")
    modifier.object = rig
mesh_object.parent = rig

bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

bpy.ops.object.select_all(action="DESELECT")
mesh_object.select_set(True)
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
    "method": "deterministic_proximity_four_influence",
    "vertices": len(mesh_object.data.vertices),
    "deform_bones": [bone.name for bone in deform_bones],
    "fbx": str(fbx_output),
    "blend": str(blend_path),
}
report_output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
