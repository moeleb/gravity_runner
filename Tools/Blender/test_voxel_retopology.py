import bpy
import json
import sys
from pathlib import Path


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


source = Path(argument_after("--source"))
blend_output = Path(argument_after("--blend"))
report_output = Path(argument_after("--report"))
voxel_size = float(argument_after("--voxel"))
target_triangles = int(argument_after("--triangles"))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))
obj = next(item for item in bpy.context.scene.objects if item.type == "MESH")
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

obj.data.remesh_voxel_size = voxel_size
obj.data.remesh_voxel_adaptivity = 0.0
obj.data.use_remesh_preserve_volume = True
bpy.ops.object.voxel_remesh()

obj.data.calc_loop_triangles()
voxel_triangles = len(obj.data.loop_triangles)
ratio = min(1.0, target_triangles / max(1, voxel_triangles))
modifier = obj.modifiers.new("Mobile triangle target", "DECIMATE")
modifier.ratio = ratio
modifier.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=modifier.name)
obj.data.calc_loop_triangles()

for material in list(obj.data.materials):
    obj.data.materials.pop(index=0)
material = bpy.data.materials.new("Retopology Validation")
material.diffuse_color = (0.08, 0.62, 0.86, 1.0)
obj.data.materials.append(material)

blend_output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(blend_output))
report = {
    "voxel_size": voxel_size,
    "voxel_triangles": voxel_triangles,
    "final_vertices": len(obj.data.vertices),
    "final_triangles": len(obj.data.loop_triangles),
    "blend": str(blend_output),
}
report_output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

