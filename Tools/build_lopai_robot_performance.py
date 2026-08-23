import bpy
import math
import os
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "ThirdParty", "LopaiRobot", "Animated")
os.makedirs(OUT_DIR, exist_ok=True)


def mat(name, base, emission=None, strength=0.0, metallic=0.0, roughness=0.35):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*base, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    return material


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def key(obj, frame, path, index=-1):
    obj.keyframe_insert(data_path=path, index=index, frame=frame)


def set_interp(action, mode="BEZIER"):
    for curve in action.fcurves:
        for point in curve.keyframe_points:
            point.interpolation = mode


scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = 360
scene.render.fps = 30
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 540
scene.render.resolution_y = 960
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.world.color = (0.002, 0.004, 0.012)
scene.view_settings.look = "Medium High Contrast"

# Remove original studio lights and visible custom-shape helpers.
for obj in list(bpy.data.objects):
    if obj.type == "LIGHT" or obj.name.startswith("Bone.") or obj.name in {"FKBone", "IKBone"}:
        bpy.data.objects.remove(obj, do_unlink=True)

arm = bpy.data.objects["Armature"]
mesh = bpy.data.objects["Retopo.005"]
arm.hide_render = False
mesh.hide_render = False

# The source rig uses IK constraints for posing. Muting them gives predictable,
# portable FK animation curves for the Unity FBX export.
# Its hand controls normally live outside the deform chain, so reparent them
# while preserving their rest positions; this keeps both rigid hands attached
# when the shoulder and forearm bones move in FK.
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
for side in ("L", "R"):
    arm.data.edit_bones[f"handIK.{side}"].parent = arm.data.edit_bones[f"forearm.{side}"]
bpy.ops.object.mode_set(mode="OBJECT")
for pb in arm.pose.bones:
    pb.rotation_mode = "XYZ"
    for constraint in pb.constraints:
        constraint.mute = True

action = bpy.data.actions.new("GHD_Robot_Dance_Jump_Spray_Hi")
arm.animation_data_create()
arm.animation_data.action = action

animated_bones = [
    "pelvis", "spine1", "spine2", "neck", "head",
    "collar.L", "bicept.L", "elbow.L", "forearm.L",
    "collar.R", "bicept.R", "elbow.R", "forearm.R",
    "hip.L", "knee.L", "shin.L", "foot.L",
    "hip.R", "knee.R", "shin.R", "foot.R",
]


def pose(frame, rotations=None, root=(0, 0, 0), root_z=0.0):
    rotations = rotations or {}
    arm.location = root
    arm.rotation_euler = (0.0, 0.0, math.radians(root_z))
    key(arm, frame, "location")
    key(arm, frame, "rotation_euler")
    for name in animated_bones:
        pb = arm.pose.bones.get(name)
        if not pb:
            continue
        degrees = rotations.get(name, (0, 0, 0))
        pb.rotation_euler = tuple(math.radians(value) for value in degrees)
        pb.keyframe_insert(data_path="rotation_euler", frame=frame)


neutral = {
    "bicept.L": (0, 0, -4), "bicept.R": (0, 0, 4),
    "elbow.L": (0, 0, 8), "elbow.R": (0, 0, -8),
}
pose(1, neutral)

# Dance: alternating hip, shoulder and arm shapes with a strong readable beat.
for i, frame in enumerate(range(15, 121, 15)):
    side = 1 if i % 2 == 0 else -1
    bounce = -8 if i % 2 == 0 else 3
    dance = {
        "pelvis": (bounce, side * 7, side * 13),
        "spine1": (-bounce * 0.45, -side * 5, -side * 10),
        "spine2": (0, side * 5, -side * 7),
        "head": (side * 5, -side * 7, side * 5),
        "bicept.L": (side * 22, -18, -48 - side * 14),
        "elbow.L": (0, side * 10, 58 + side * 14),
        "forearm.L": (side * 8, 0, 12),
        "bicept.R": (-side * 22, 18, 48 - side * 14),
        "elbow.R": (0, -side * 10, -58 + side * 14),
        "forearm.R": (-side * 8, 0, -12),
        "hip.L": (side * 8, 0, -side * 8),
        "knee.L": (14 if side > 0 else 4, 0, 0),
        "hip.R": (-side * 8, 0, -side * 8),
        "knee.R": (4 if side > 0 else 14, 0, 0),
    }
    pose(frame, dance, root=(side * 0.18, 0, 0.08 if i % 2 else 0))

# Jump: crouch, lift, airborne tuck, landing and recovery.
pose(126, {
    "pelvis": (18, 0, 0), "spine1": (-10, 0, 0),
    "hip.L": (-30, 0, 0), "knee.L": (55, 0, 0),
    "hip.R": (-30, 0, 0), "knee.R": (55, 0, 0),
    "bicept.L": (15, 0, -25), "bicept.R": (15, 0, 25),
}, root=(0, 0, 0))
pose(145, {
    "spine1": (-8, 0, 0), "spine2": (-8, 0, 0),
    "bicept.L": (-35, 0, -105), "elbow.L": (0, 0, 20),
    "bicept.R": (-35, 0, 105), "elbow.R": (0, 0, -20),
    "hip.L": (18, 0, -8), "knee.L": (30, 0, 0),
    "hip.R": (18, 0, 8), "knee.R": (30, 0, 0),
}, root=(0, 0, 1.4))
pose(158, {
    "spine1": (-12, 0, 0), "head": (8, 0, 0),
    "bicept.L": (-48, 0, -120), "elbow.L": (0, 0, 25),
    "bicept.R": (-48, 0, 120), "elbow.R": (0, 0, -25),
    "hip.L": (-28, 0, -14), "knee.L": (58, 0, 0),
    "hip.R": (-28, 0, 14), "knee.R": (58, 0, 0),
}, root=(0, 0, 2.25))
pose(176, {
    "pelvis": (22, 0, 0), "spine1": (-12, 0, 0),
    "hip.L": (-35, 0, 0), "knee.L": (62, 0, 0),
    "hip.R": (-35, 0, 0), "knee.R": (62, 0, 0),
    "bicept.L": (20, 0, -35), "bicept.R": (20, 0, 35),
}, root=(0, 0, 0))
pose(192, neutral)

# Spray performance: shift left, turn three-quarters, aim left arm at wall.
spray_pose = {
    "pelvis": (0, 0, -8), "spine1": (0, -8, 15), "spine2": (0, 8, 12),
    "head": (0, -12, -18),
    "bicept.L": (-10, -20, -72), "elbow.L": (0, 0, 36), "forearm.L": (0, 18, 16),
    "bicept.R": (12, 8, 24), "elbow.R": (0, 0, -46),
    "hip.L": (4, 0, -5), "hip.R": (-4, 0, -5),
}
pose(205, spray_pose, root=(-1.65, 0, 0), root_z=-12)
for i, frame in enumerate((220, 235, 250, 265, 280)):
    p = dict(spray_pose)
    p["bicept.L"] = (-10 + (i % 2) * 8, -20, -72 + i * 5)
    p["elbow.L"] = (0, (-1) ** i * 8, 34 + (i % 2) * 10)
    p["head"] = (0, -12 + i * 2, -18)
    pose(frame, p, root=(-1.65, 0, 0), root_z=-12)

# Greeting: return center, enthusiastic wave, settle in a friendly pose.
pose(294, neutral, root=(0, 0, 0))
for i, frame in enumerate((305, 315, 325, 335, 345)):
    wave_side = 1 if i % 2 == 0 else -1
    greet = {
        "pelvis": (0, 0, -5), "spine1": (0, 0, 7), "spine2": (0, 0, 8),
        "head": (-5, 0, -8),
        "bicept.L": (-28, 0, -105), "elbow.L": (0, 0, 78),
        "forearm.L": (0, wave_side * 22, wave_side * 18),
        "bicept.R": (5, 0, 18), "elbow.R": (0, 0, -34),
    }
    pose(frame, greet, root=(0, 0, 0.05 if i % 2 == 0 else 0))
pose(360, {
    "pelvis": (0, 0, -4), "spine1": (0, 0, 6), "head": (-4, 0, -7),
    "bicept.L": (-25, 0, -100), "elbow.L": (0, 0, 72),
    "bicept.R": (4, 0, 20), "elbow.R": (0, 0, -32),
})
set_interp(action)

# Scene materials.
dark = mat("GHD_Dark", (0.004, 0.008, 0.018), metallic=0.25, roughness=0.5)
cyan = mat("GHD_Cyan", (0.0, 0.06, 0.08), (0.0, 0.95, 1.0), 8.0, metallic=0.3)
magenta = mat("GHD_Magenta", (0.08, 0.0, 0.06), (1.0, 0.02, 0.65), 9.0, metallic=0.3)
orange = mat("GHD_Orange", (0.12, 0.025, 0.0), (1.0, 0.18, 0.015), 5.0, metallic=0.4)

# Floor.
bpy.ops.mesh.primitive_plane_add(size=55, location=(0, 2, -0.12))
floor = bpy.context.object
floor.name = "Neon Tunnel Floor"
floor.data.materials.append(dark)

# Neon tunnel rings receding behind the character.
for i in range(10):
    bpy.ops.mesh.primitive_torus_add(major_radius=5.2 + i * 0.18, minor_radius=0.055,
                                    major_segments=48, minor_segments=6,
                                    location=(0, 2.5 + i * 3.2, 4.1),
                                    rotation=(math.radians(90), 0, 0))
    ring = bpy.context.object
    ring.name = f"Neon Tunnel Ring {i:02d}"
    ring.data.materials.append(cyan if i % 2 == 0 else magenta)

# Vertical wall/canvas behind the spray portion.
bpy.ops.mesh.primitive_cube_add(location=(1.9, 1.55, 4.0), scale=(2.8, 0.09, 3.35))
wall = bpy.context.object
wall.name = "Neon Graffiti Wall"
wall.data.materials.append(dark)

# Spray can, parented to the left hand, shown only during the spray section.
bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.18, depth=0.62, location=(0, 0, 0))
can = bpy.context.object
can.name = "Neon Spray Can"
can.data.materials.append(orange)
can.parent = arm
can.parent_type = "BONE"
can.parent_bone = "hand3.L"
can.location = (0.12, 0.0, 0.12)
can.rotation_euler = (math.radians(90), 0, 0)
can.hide_render = True
can.hide_viewport = True
key(can, 1, "hide_render"); key(can, 1, "hide_viewport")
can.hide_render = False; can.hide_viewport = False
key(can, 202, "hide_render"); key(can, 202, "hide_viewport")
key(can, 280, "hide_render"); key(can, 280, "hide_viewport")
can.hide_render = True; can.hide_viewport = True
key(can, 286, "hide_render"); key(can, 286, "hide_viewport")

# Animated neon graffiti strokes spelling GHD. Geometry grows while spraying.
curve_data = bpy.data.curves.new("GHD Graffiti Curve", "CURVE")
curve_data.dimensions = "3D"
curve_data.bevel_depth = 0.045
curve_data.bevel_resolution = 3
curve_data.resolution_u = 8
curve_data.materials.append(magenta)
strokes = [
    [(-0.6, 0.0), (-1.2, 0.0), (-1.4, 0.5), (-1.4, 1.2), (-0.9, 1.6), (-0.3, 1.4), (-0.35, 0.8), (-0.85, 0.8)],
    [(-0.05, 1.6), (-0.05, 0.0)], [(0.75, 1.6), (0.75, 0.0)], [(0.0, 0.82), (0.75, 0.82)],
    [(1.05, 1.6), (1.05, 0.0), (1.65, 0.0), (2.0, 0.4), (2.0, 1.2), (1.65, 1.6), (1.05, 1.6)],
]
for points in strokes:
    spline = curve_data.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for p, (x, z) in zip(spline.points, points):
        p.co = (x, 0.0, z, 1.0)
graffiti = bpy.data.objects.new("GHD Neon Graffiti", curve_data)
bpy.context.collection.objects.link(graffiti)
graffiti.location = (1.05, 1.43, 3.25)
curve_data.bevel_factor_end = 0.0
curve_data.keyframe_insert(data_path="bevel_factor_end", frame=205)
curve_data.bevel_factor_end = 1.0
curve_data.keyframe_insert(data_path="bevel_factor_end", frame=278)

# HI text pops in for the greeting.
bpy.ops.object.text_add(location=(1.65, 0.55, 7.25), rotation=(math.radians(90), 0, 0))
hi = bpy.context.object
hi.name = "HI Greeting"
hi.data.body = "HI!"
hi.data.align_x = "CENTER"
hi.data.size = 1.15
hi.data.extrude = 0.035
hi.data.bevel_depth = 0.015
hi.data.materials.append(cyan)
hi.scale = (0.001, 0.001, 0.001)
key(hi, 294, "scale")
hi.scale = (1.15, 1.15, 1.15)
key(hi, 310, "scale")
hi.scale = (1.0, 1.0, 1.0)
key(hi, 324, "scale")

# Lights.
for name, location, color, energy in [
    ("Cyan Key", (-5, -5, 8), (0.0, 0.8, 1.0), 1200),
    ("Magenta Rim", (5, 1, 7), (1.0, 0.0, 0.55), 1000),
    ("Orange Fill", (0, -3, 3), (1.0, 0.18, 0.02), 700),
]:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = 4.0
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    look_at(light, (0, 0, 4))

# Vertical mobile camera.
camera_data = bpy.data.cameras.new("Gravity Performance Camera")
camera = bpy.data.objects.new("Gravity Performance Camera", camera_data)
bpy.context.collection.objects.link(camera)
scene.camera = camera
camera.location = (0, -20.5, 5.1)
camera_data.lens = 52
look_at(camera, (0, 0.2, 4.1))

# Timeline markers make the sequence easy to edit.
for frame, name in [(1, "DANCE"), (126, "JUMP"), (205, "SPRAY"), (294, "HI_WAVE")]:
    scene.timeline_markers.new(name, frame=frame)

# Pack original textures so the animated source remains self-contained.
bpy.ops.file.pack_all()
blend_path = os.path.join(OUT_DIR, "GHD_LopaiRobot_Performance.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

# Unity-ready animation FBX. Props and the neon set are included so it can be
# previewed immediately; Unity can selectively use the character/action.
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT_DIR, "GHD_LopaiRobot_Performance.fbx"),
    use_selection=False,
    object_types={"ARMATURE", "MESH", "EMPTY", "OTHER"},
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_simplify_factor=0.0,
    path_mode="COPY",
    embed_textures=True,
)

# Preview still from the greeting pose.
scene.frame_set(325)
scene.render.filepath = os.path.join(OUT_DIR, "GHD_LopaiRobot_Performance_Preview.png")
bpy.ops.render.render(write_still=True)
print("OUTPUT_BLEND", blend_path)
print("OUTPUT_FBX", os.path.join(OUT_DIR, "GHD_LopaiRobot_Performance.fbx"))
