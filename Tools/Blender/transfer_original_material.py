import bpy
import json
import sys
from pathlib import Path


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

existing_meshes = set(obj.name for obj in bpy.context.scene.objects if obj.type == "MESH")
bpy.ops.import_scene.gltf(filepath=str(source_glb))
high = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name not in existing_meshes)
high.name = "Meshy_Material_Source"

if not low.data.uv_layers:
    low.data.uv_layers.new(name="UVMap")
low.data.uv_layers.active_index = 0
high.data.uv_layers.active_index = 0

modifier = low.modifiers.new("Transfer Meshy UV", "DATA_TRANSFER")
modifier.object = high
modifier.use_object_transform = True
modifier.use_loop_data = True
modifier.data_types_loops = {"UV"}
modifier.loop_mapping = "POLYINTERP_NEAREST"
modifier.layers_uv_select_src = high.data.uv_layers.active.name
modifier.layers_uv_select_dst = low.data.uv_layers.active.name
modifier.islands_precision = 0.1
bpy.context.view_layer.objects.active = low
low.select_set(True)
bpy.ops.object.modifier_apply(modifier=modifier.name)

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
    "method": "nearest_surface_original_uv_transfer",
    "vertices": len(low.data.vertices),
    "triangles": len(low.data.loop_triangles),
    "texture_limit": texture_size,
    "materials": [slot.material.name if slot.material else None for slot in low.material_slots],
    "blend": str(output_blend),
    "fbx": str(output_fbx),
}
report_path.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
