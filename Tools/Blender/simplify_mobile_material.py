import bpy
import sys
from pathlib import Path


def argument_after(flag: str) -> str:
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[args.index(flag) + 1]


blend_path = Path(argument_after("--blend"))
fbx_path = Path(argument_after("--fbx"))
bpy.ops.wm.open_mainfile(filepath=str(blend_path))
mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")

for slot in mesh.material_slots:
    material = slot.material
    if not material or not material.use_nodes:
        continue
    shader = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if shader is None:
        continue
    # Keep only Meshy's true base-color map. Emission/normal/ORM maps reference the old
    # fragmented topology and reveal every transferred seam on the reconstructed mesh.
    for socket_name in ("Emission Color", "Normal", "Metallic", "Roughness"):
        socket = shader.inputs.get(socket_name)
        if socket:
            for link in list(socket.links):
                material.node_tree.links.remove(link)
    shader.inputs["Emission Color"].default_value = (0, 0, 0, 1)
    shader.inputs["Metallic"].default_value = 0.0
    shader.inputs["Roughness"].default_value = 0.72

bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(fbx_path), use_selection=True, apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
    object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True,
    add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
    path_mode="COPY", embed_textures=True,
)

