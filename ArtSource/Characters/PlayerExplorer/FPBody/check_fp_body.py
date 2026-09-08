"""Run with PlayerExplorer_FPBody.blend open; checks source preservation and pose seams."""
import bpy
from mathutils import Vector

scene = bpy.data.scenes['Explorer_FPBody']
bpy.context.window.scene = scene
rig = scene.objects['FPBody_Armature']
source = bpy.data.objects['Armature']
assert len(rig.data.bones) == len(source.data.bones) == 101
for bone in rig.data.bones:
    original = source.data.bones[bone.name]
    assert (bone.head_local-original.head_local).length < 1e-5
    assert (bone.tail_local-original.tail_local).length < 1e-5
    assert (bone.parent.name if bone.parent else None) == (original.parent.name if original.parent else None)
triangles = 0
for obj in scene.objects:
    if obj.type != 'MESH' or 'Helmetmesh' in obj.name:
        continue
    original = bpy.data.objects[obj.name.removeprefix('FPBody_')]
    assert obj.data is not original.data
    assert len(obj.data.vertices) == len(original.data.vertices)
    assert len(obj.data.uv_layers) == len(original.data.uv_layers)
    assert all((a.co-b.co).length < 1e-7 for a,b in zip(obj.data.vertices,original.data.vertices))
    for layer, original_layer in zip(obj.data.uv_layers, original.data.uv_layers):
        original_uvs = {}
        for loop in original.data.loops:
            original_uvs.setdefault(loop.vertex_index, []).append(original_layer.data[loop.index].uv.copy())
        for loop in obj.data.loops:
            assert any((layer.data[loop.index].uv-uv).length < 1e-7 for uv in original_uvs[loop.vertex_index])
    assert any(m.type=='ARMATURE' and m.object==rig for m in obj.modifiers)
    for vertex in obj.data.vertices:
        weights = [g.weight for g in vertex.groups]
        assert weights and min(weights) >= 0 and abs(sum(weights)-1) < 0.0001
        assert [(g.group,g.weight) for g in vertex.groups] == [(g.group,g.weight) for g in original.data.vertices[vertex.index].groups]
    triangles += sum(len(p.vertices)-2 for p in obj.data.polygons)
    assert all(len(p.vertices) == 3 and p.area > 1e-12 for p in obj.data.polygons)
assert triangles == 108698
saved = rig.animation_data.action
try:
    for name in ('AN_FPBody_Relaxed','AN_FPBody_ToolReady','AN_FPBody_TwoHandReady'):
        rig.animation_data.action = bpy.data.actions[name]
        assert tuple(rig.animation_data.action.frame_range) == (1,61)
        scene.frame_set(1)
        start = {p.name:p.matrix.copy() for p in rig.pose.bones}
        for frame in (16,31,46,61):
            scene.frame_set(frame)
            for side in ('L','R'):
                names = ['CC_Base_'+side+'_'+n for n in ('Upperarm','Forearm','Hand')]
                a,b,c = [rig.pose.bones[n].head for n in names]
                x,y,z = [start[n].translation for n in names]
                assert abs((a-b).length-(x-y).length) < 0.001
                assert abs((b-c).length-(y-z).length) < 0.001
        for bone in rig.pose.bones:
            delta = start[bone.name].inverted() @ bone.matrix
            assert delta.translation.length < 0.0001
            assert abs(delta.to_quaternion().angle) < 0.0001
finally:
    rig.animation_data.action = saved
    scene.frame_set(1)
print('PASS: original vertex positions/UV/weights/rest skeleton preserved; 108698 nondegenerate triangles; 101 bones; no arm stretching; all three complete pose loops match.')
