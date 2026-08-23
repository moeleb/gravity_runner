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
texture_output = Path(argument_after("--texture"))
report_output = Path(argument_after("--report"))
texture_size = int(argument_after("--texture-size"))

for path in (output_blend, output_fbx, texture_output, report_output):
    path.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=str(rigged_blend))
low = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
rig.data.pose_position = "REST"

# Unity/mobile supports four high-quality bone influences per vertex efficiently.
bpy.ops.object.select_all(action="DESELECT")
low.select_set(True)
bpy.context.view_layer.objects.active = low
bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

# The voxel-retopologized mesh needs a fresh non-overlapping UV layout.
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.015, area_weight=0.2)
bpy.ops.object.mode_set(mode="OBJECT")

# Bring the original textured geometry into the same scene as the bake source.
existing_meshes = set(obj.name for obj in bpy.context.scene.objects if obj.type == "MESH")
bpy.ops.import_scene.gltf(filepath=str(source_glb))
high = next(
    obj for obj in bpy.context.scene.objects
    if obj.type == "MESH" and obj.name not in existing_meshes
)
high.name = "Meshy_Color_Source"

bake_image = bpy.data.images.new(
    "Nova_Girl_BaseColor_1K", width=texture_size, height=texture_size, alpha=True
)
bake_image.generated_color = (0.8, 0.8, 0.8, 1.0)

material = bpy.data.materials.new("Nova_Girl_Mobile_Material")
material.use_nodes = True
nodes = material.node_tree.nodes
links = material.node_tree.links
for node in list(nodes):
    nodes.remove(node)
output = nodes.new("ShaderNodeOutputMaterial")
shader = nodes.new("ShaderNodeBsdfPrincipled")
shader.inputs["Roughness"].default_value = 0.68
texture = nodes.new("ShaderNodeTexImage")
texture.image = bake_image
nodes.active = texture
texture.select = True
links.new(texture.outputs["Color"], shader.inputs["Base Color"])
links.new(texture.outputs["Alpha"], shader.inputs["Alpha"])
links.new(shader.outputs["BSDF"], output.inputs["Surface"])
low.data.materials.clear()
low.data.materials.append(material)

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 1
scene.render.bake.use_selected_to_active = True
scene.render.bake.use_cage = False
scene.render.bake.cage_extrusion = 0.012
scene.render.bake.max_ray_distance = 0.028
scene.render.bake.margin = 5

bpy.ops.object.select_all(action="DESELECT")
high.select_set(True)
low.select_set(True)
bpy.context.view_layer.objects.active = low
bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})

bake_image.filepath_raw = str(texture_output)
bake_image.file_format = "PNG"
bake_image.save()
bake_image.pack()

# Remove the large color source so it can never leak into the Unity export.
bpy.data.objects.remove(high, do_unlink=True)

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
    "vertices": len(low.data.vertices),
    "triangles": len(low.data.loop_triangles),
    "texture_size": texture_size,
    "texture": str(texture_output),
    "blend": str(output_blend),
    "fbx": str(output_fbx),
}
report_output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
