"""Export the connected FPS body plus three full-length clips from Blender."""
from pathlib import Path
import bpy
import bmesh

OUT = Path(r'C:\Users\Koray\UnityProjects\farion\ArtSource\Characters\PlayerExplorer\FPBody')
scene = bpy.data.scenes['Explorer_FPBody']
bpy.context.window.scene = scene
rig = scene.objects['FPBody_Armature']
meshes = [o for o in scene.objects if o.type == 'MESH' and 'Helmetmesh' not in o.name]
assert len(meshes) == 3
for o in scene.objects:
    o.select_set(False)
rig.select_set(True)
for o in meshes:
    for modifier in list(o.modifiers):
        if modifier.type == 'TRIANGULATE':
            o.modifiers.remove(modifier)
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    bmesh.ops.triangulate(mesh, faces=list(mesh.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
    degenerate = [face for face in mesh.faces if face.calc_area() < 1e-12]
    if degenerate:
        bmesh.ops.delete(mesh, geom=degenerate, context='FACES_ONLY')
    mesh.to_mesh(o.data)
    mesh.free()
    o.data.update()
    print(o.name, 'removed zero-area faces:', len(degenerate))
    o.select_set(True)
bpy.context.view_layer.objects.active = rig
action = rig.animation_data.action
options = dict(use_selection=True, object_types={'ARMATURE','MESH'},
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
    add_leaf_bones=False, use_armature_deform_only=False,
    bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
    bake_anim_simplify_factor=0, path_mode='STRIP', embed_textures=False,
    use_mesh_modifiers=True)
try:
    rig.animation_data.action = None
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(OUT/'SM_PlayerExplorer_FPBody_A.fbx'), bake_anim=False, **options)
    rig.data.pose_position = 'POSE'
    for mesh in meshes:
        mesh.select_set(False)
    for name in ('AN_FPBody_Relaxed','AN_FPBody_ToolReady','AN_FPBody_TwoHandReady'):
        rig.animation_data.action = bpy.data.actions[name]
        scene.frame_set(1)
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')), bake_anim=True, **options)
finally:
    rig.data.pose_position = 'POSE'
    rig.animation_data.action = action
    scene.frame_set(1)
print('Exported head-free connected body, 3 renderers, 101 bones, and three 2-second clips.')
