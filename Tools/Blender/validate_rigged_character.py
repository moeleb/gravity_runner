import bpy
import json
import sys
from pathlib import Path


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


blend_path = Path(argument_after("--blend"))
report_path = Path(argument_after("--report"))
bpy.ops.wm.open_mainfile(filepath=str(blend_path))

mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")

group_names = {group.index: group.name for group in mesh.vertex_groups}
weighted_counts = {name: 0 for name in group_names.values()}
unweighted = 0
max_influences = 0
for vertex in mesh.data.vertices:
    influences = [item for item in vertex.groups if item.weight > 0.0001]
    max_influences = max(max_influences, len(influences))
    if not influences:
        unweighted += 1
    for influence in influences:
        name = group_names.get(influence.group)
        if name:
            weighted_counts[name] += 1

report = {
    "mesh": mesh.name,
    "vertices": len(mesh.data.vertices),
    "armature_modifier": any(mod.type == "ARMATURE" and mod.object == rig for mod in mesh.modifiers),
    "parented_to_rig": mesh.parent == rig,
    "unweighted_vertices": unweighted,
    "max_influences": max_influences,
    "weighted_vertex_counts": weighted_counts,
    "bones_without_weights": [
        bone.name for bone in rig.data.bones
        if bone.use_deform and weighted_counts.get(bone.name, 0) == 0
    ],
}
report_path.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

