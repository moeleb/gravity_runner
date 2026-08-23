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


def distance_to_segment(point, start, end):
    segment = end - start
    if segment.length_squared <= 1e-10:
        return (point - start).length
    amount = max(0.0, min(1.0, (point - start).dot(segment) / segment.length_squared))
    return (point - (start + segment * amount)).length


blend_path = Path(argument_after("--blend"))
fbx_output = Path(argument_after("--fbx"))
report_output = Path(argument_after("--report"))
bpy.ops.wm.open_mainfile(filepath=str(blend_path))

mesh_object = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
mesh_object.vertex_groups.clear()

deform_bones = [bone for bone in rig.data.bones if bone.use_deform]
group_indices = {bone.name: mesh_object.vertex_groups.new(name=bone.name).index for bone in deform_bones}
segments = {bone.name: (bone.head_local.copy(), bone.tail_local.copy()) for bone in deform_bones}


def nearest_weights(point, names, count=2):
    distances = sorted(
        (distance_to_segment(point, *segments[name]), name) for name in names
    )[:count]
    raw = [(name, 1.0 / math.pow(distance + 0.02, 3.5)) for distance, name in distances]
    total = sum(weight for _, weight in raw)
    return [(name, weight / total) for name, weight in raw]


def weights_for(point):
    if point.z > 0.36:
        return [("Head", 1.0)]
    if point.z > 0.04 and (point.y > 0.075 or abs(point.x) > 0.325):
        return [("Head", 0.94), ("Neck", 0.06)]
    if point.z < -0.71:
        side = "Left" if point.x >= 0 else "Right"
        return [(side + "Foot", 1.0)]
    if point.z < -0.18:
        side = "Left" if point.x >= 0 else "Right"
        return nearest_weights(point, [side + "UpperLeg", side + "LowerLeg", side + "Foot", "Hips"])
    if abs(point.x) > 0.18:
        side = "Left" if point.x >= 0 else "Right"
        return nearest_weights(point, [
            side + "Shoulder", side + "UpperArm", side + "LowerArm", side + "Hand"
        ])
    return nearest_weights(point, ["Hips", "Spine", "Chest", "UpperChest", "Neck"])


bm = bmesh.new()
bm.from_mesh(mesh_object.data)
bm.verts.ensure_lookup_table()
deform_layer = bm.verts.layers.deform.verify()
remaining = set(vertex.index for vertex in bm.verts)
island_count = 0

while remaining:
    seed = remaining.pop()
    stack = [bm.verts[seed]]
    island = [bm.verts[seed]]
    while stack:
        vertex = stack.pop()
        for edge in vertex.link_edges:
            other = edge.other_vert(vertex)
            if other.index in remaining:
                remaining.remove(other.index)
                island.append(other)
                stack.append(other)

    center = sum((vertex.co for vertex in island), Vector()) / len(island)
    candidates = weights_for(center)
    for vertex in island:
        deform = vertex[deform_layer]
        for name, weight in candidates:
            deform[group_indices[name]] = weight
    island_count += 1

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
    filepath=str(fbx_output), use_selection=True, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
    object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True,
    add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
    path_mode="COPY", embed_textures=True,
)

report = {
    "method": "rigid_island_consistent_weights",
    "islands": island_count,
    "vertices": len(mesh_object.data.vertices),
    "fbx": str(fbx_output),
}
report_output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

