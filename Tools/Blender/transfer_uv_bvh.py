import bpy
import json
import sys
from pathlib import Path
from mathutils import Vector, geometry
from mathutils.bvhtree import BVHTree


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


rigged_blend = Path(argument_after("--blend"))
source_glb = Path(argument_after("--source"))
output_blend = Path(argument_after("--output-blend"))
output_fbx = Path(argument_after("--fbx"))
report_path = Path(argument_after("--report"))
texture_size = int(argument_after("--texture-size"))

bpy.ops.wm.open_mainfile(filepath=str(rigged_blend))
low = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
rig.data.pose_position = "REST"

bpy.ops.object.select_all(action="DESELECT")
low.select_set(True)
bpy.context.view_layer.objects.active = low
bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

existing = set(obj.name for obj in bpy.context.scene.objects if obj.type == "MESH")
bpy.ops.import_scene.gltf(filepath=str(source_glb))
high = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name not in existing)
high.name = "Meshy_UV_Source"
high.data.calc_loop_triangles()

source_vertices = [high.matrix_world @ vertex.co for vertex in high.data.vertices]
source_polygons = [tuple(poly.vertices) for poly in high.data.polygons]
if any(len(poly) != 3 for poly in source_polygons):
    raise RuntimeError("The reduced UV source must be triangulated")
bvh = BVHTree.FromPolygons(source_vertices, source_polygons, all_triangles=True)

if low.data.uv_layers:
    low.data.uv_layers.remove(low.data.uv_layers[0])
target_uv = low.data.uv_layers.new(name="UVMap")
source_uv = high.data.uv_layers.active
if source_uv is None:
    raise RuntimeError("Meshy source has no UV map")

mapped_uv_by_vertex = {}
misses = 0
for loop in low.data.loops:
    vertex_index = loop.vertex_index
    mapped = mapped_uv_by_vertex.get(vertex_index)
    if mapped is None:
        point = low.matrix_world @ low.data.vertices[vertex_index].co
        hit = bvh.find_nearest(point)
        if hit[0] is None:
            mapped = Vector((0.0, 0.0))
            misses += 1
        else:
            location, _, polygon_index, _ = hit
            polygon = high.data.polygons[polygon_index]
            loop_indices = list(polygon.loop_indices)
            positions = [source_vertices[polygon.vertices[i]] for i in range(3)]
            uv_values = [source_uv.data[loop_indices[i]].uv for i in range(3)]
            uv3 = geometry.barycentric_transform(
                location,
                positions[0], positions[1], positions[2],
                Vector((uv_values[0].x, uv_values[0].y, 0.0)),
                Vector((uv_values[1].x, uv_values[1].y, 0.0)),
                Vector((uv_values[2].x, uv_values[2].y, 0.0)),
            )
            mapped = Vector((uv3.x, uv3.y))
        mapped_uv_by_vertex[vertex_index] = mapped
    target_uv.data[loop.index].uv = mapped

low.data.materials.clear()
for slot in high.material_slots:
    if slot.material:
        low.data.materials.append(slot.material.copy())

for image in bpy.data.images:
    width, height = image.size
    if width <= 0 or height <= 0 or image.name in {"Render Result", "Viewer Node"}:
        continue
    scale = min(1.0, texture_size / max(width, height))
    if scale < 1.0:
        image.scale(max(1, round(width * scale)), max(1, round(height * scale)))
    image.pack()

bpy.data.objects.remove(high, do_unlink=True)
output_blend.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))

bpy.ops.object.select_all(action="DESELECT")
low.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(output_fbx), use_selection=True, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
    object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True,
    add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
    path_mode="COPY", embed_textures=True,
)

low.data.calc_loop_triangles()
report = {
    "method": "BVH nearest triangle barycentric UV transfer",
    "mapped_vertices": len(mapped_uv_by_vertex), "misses": misses,
    "vertices": len(low.data.vertices), "triangles": len(low.data.loop_triangles),
    "texture_limit": texture_size, "blend": str(output_blend), "fbx": str(output_fbx),
}
report_path.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

