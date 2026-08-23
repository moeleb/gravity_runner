import bpy
import sys
from pathlib import Path


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


bpy.ops.wm.open_mainfile(filepath=argument_after("--blend"))
output = Path(argument_after("--output"))
output.parent.mkdir(parents=True, exist_ok=True)
mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
for modifier in list(mesh.modifiers):
    if modifier.type == "ARMATURE":
        mesh.modifiers.remove(modifier)
mesh.parent = None
mesh.vertex_groups.clear()
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
bpy.context.view_layer.objects.active = mesh
bpy.ops.export_scene.gltf(
    filepath=str(output), export_format="GLB", use_selection=True,
    export_apply=True, export_animations=False,
)

