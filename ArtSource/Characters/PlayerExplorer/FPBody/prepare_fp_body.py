"""Run in the connected Blender scene containing the source Armature.

Creates an isolated, textured full-body authoring scene. Never edits the source.
"""
from pathlib import Path
import bpy
from math import radians
from mathutils import Vector

ROOT = Path(r"C:\Users\Koray\UnityProjects\farion")
OUT = ROOT / "ArtSource/Characters/PlayerExplorer/FPBody"
OUT.mkdir(parents=True, exist_ok=True)
assert "Explorer_FPBody" not in bpy.data.scenes, "Working scene already exists"
source = bpy.context.scene
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "Source_20260908.blend"), copy=True)
scene = bpy.data.scenes.new("Explorer_FPBody")
scene.unit_settings.system = "METRIC"
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.fps = 30
scene.world = bpy.data.worlds.new("FPBody_StudioWorld")
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.11, 0.14, 0.18, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.45
copies = {}
for obj in source.objects:
    if obj.type not in {'ARMATURE', 'MESH'}:
        continue
    clone = obj.copy()
    clone.data = obj.data.copy()
    clone.name = 'FPBody_' + obj.name
    scene.collection.objects.link(clone)
    copies[obj] = clone
for obj, clone in copies.items():
    clone.parent = copies.get(obj.parent)
    clone.matrix_world = obj.matrix_world.copy()
    for modifier in clone.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.object = copies[modifier.object]
    if clone.animation_data and clone.animation_data.action:
        clone.animation_data.action = clone.animation_data.action.copy()
    if clone.type == 'MESH' and clone.data.shape_keys:
        # Only remove the vendor's demonstrably empty placeholder shape key.
        keys = clone.data.shape_keys.key_blocks
        if len(keys) == 2 and keys[1].name == 'V_None':
            assert all((a.co - b.co).length < 1e-7 for a, b in zip(keys[0].data, keys[1].data))
            clone.shape_key_clear()

textures = ROOT / 'Assets/Project/Art/Textures/Characters/PlayerExplorer'
material_parts = {'Suit_Pbr': 'Suit', 'Hardsurface_Details_Pbr': 'HardSurface',
                  'Belt_Pbr': 'Belt', 'Helmet_Pbr': 'Helmet'}
materials = {}
for original, part in material_parts.items():
    mat = bpy.data.materials.new('FPBody_' + part)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    for channel, socket in [('BaseColor', 'Base Color'), ('Roughness', 'Roughness'),
                            ('Metallic', 'Metallic'), ('Normal', None)]:
        path = textures / ('TX_PlayerExplorer_' + part + '_White_' + channel + '_4K.png')
        assert path.is_file(), path
        tex = nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(str(path), check_existing=True)
        if channel != 'BaseColor':
            tex.image.colorspace_settings.name = 'Non-Color'
        if socket:
            links.new(tex.outputs['Color'], bsdf.inputs[socket])
        else:
            normal = nodes.new('ShaderNodeNormalMap')
            links.new(tex.outputs['Color'], normal.inputs['Color'])
            links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
    materials[original] = mat
for clone in copies.values():
    if clone.type == 'MESH':
        for slot in clone.material_slots:
            if slot.material and slot.material.name in materials:
                slot.material = materials[slot.material.name]

camera = bpy.data.objects.new('FPBody_Eyes', bpy.data.cameras.new('FPBody_Eyes'))
scene.collection.objects.link(camera)
rig = copies[bpy.data.objects['Armature']]
camera.location = rig.matrix_world @ rig.pose.bones['CC_Base_Head'].head + Vector((0, -0.18, 0.065))
camera.rotation_euler = Vector((0, -1, 0)).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'PERSP'
camera.data.sensor_fit = 'HORIZONTAL'
camera.data.lens = 17.537
camera.data.clip_start = 0.05
camera.data.clip_end = 100
scene.camera = camera
for name, location, power, size in [
        ('Key', (2, -3, 4), 280, 3), ('Fill', (-3, -1, 2), 160, 3),
        ('Rim', (0, 2, 3), 220, 2)]:
    light = bpy.data.objects.new('FPBody_' + name, bpy.data.lights.new('FPBody_' + name, 'AREA'))
    scene.collection.objects.link(light)
    light.location = location
    light.rotation_euler = (Vector((0, 0, 1)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    light.data.energy, light.data.shape, light.data.size = power, 'DISK', size
bpy.context.window.scene = scene
# Head visibility mirrors the current local-owner contract; full geometry stays in file.
for obj in scene.objects:
    if obj.type == 'MESH' and ('Helmetmesh' in obj.name):
        obj.hide_render = True
        obj.hide_set(True)
for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        area.spaces.active.region_3d.view_perspective = 'CAMERA'
        area.spaces.active.overlay.show_overlays = False
scene['purpose'] = 'Connected full-body FPS pose authoring; original rig, UVs and skinning retained.'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'PlayerExplorer_FPBody.blend'))
print('Created isolated textured full-body scene:', len(copies), 'objects')
