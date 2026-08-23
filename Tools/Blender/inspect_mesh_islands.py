import bpy
import bmesh
import json
import sys
from pathlib import Path


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


bpy.ops.wm.open_mainfile(filepath=argument_after("--blend"))
report_path = Path(argument_after("--report"))
mesh_object = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")

bm = bmesh.new()
bm.from_mesh(mesh_object.data)
bm.verts.ensure_lookup_table()

remaining = set(vertex.index for vertex in bm.verts)
islands = []
while remaining:
    seed_index = remaining.pop()
    stack = [bm.verts[seed_index]]
    component = [seed_index]
    while stack:
        vertex = stack.pop()
        for edge in vertex.link_edges:
            other = edge.other_vert(vertex)
            if other.index in remaining:
                remaining.remove(other.index)
                component.append(other.index)
                stack.append(other)
    points = [bm.verts[index].co for index in component]
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    center = [(minimum[axis] + maximum[axis]) * 0.5 for axis in range(3)]
    islands.append({
        "vertices": len(component),
        "bounds_min": minimum,
        "bounds_max": maximum,
        "center": center,
    })

bm.free()
islands.sort(key=lambda item: item["vertices"], reverse=True)
report = {"island_count": len(islands), "largest_islands": islands[:80]}
report_path.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

