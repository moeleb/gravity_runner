import bpy
import json
import mathutils
import sys
from pathlib import Path


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    index = args.index(flag)
    return args[index + 1]


source = Path(argument_after("--source"))
report_path = Path(argument_after("--report"))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))

meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
report = {
    "source": str(source),
    "blender_version": bpy.app.version_string,
    "objects": [],
    "totals": {"vertices": 0, "polygons": 0, "triangles": 0},
}

for obj in meshes:
    mesh = obj.data
    mesh.calc_loop_triangles()
    bounds = [obj.matrix_world @ mathutils.Vector(corner) for corner in obj.bound_box]
    minimum = [min(value[i] for value in bounds) for i in range(3)]
    maximum = [max(value[i] for value in bounds) for i in range(3)]
    item = {
        "name": obj.name,
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "bounds_min": minimum,
        "bounds_max": maximum,
        "dimensions": [maximum[i] - minimum[i] for i in range(3)],
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
        "shape_keys": list(mesh.shape_keys.key_blocks.keys()) if mesh.shape_keys else [],
        "vertex_groups": [group.name for group in obj.vertex_groups],
    }
    report["objects"].append(item)
    report["totals"]["vertices"] += item["vertices"]
    report["totals"]["polygons"] += item["polygons"]
    report["totals"]["triangles"] += item["triangles"]

report["images"] = [
    {
        "name": image.name,
        "size": list(image.size),
        "packed": image.packed_file is not None,
        "filepath": image.filepath,
    }
    for image in bpy.data.images
]

report_path.parent.mkdir(parents=True, exist_ok=True)
report_path.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
