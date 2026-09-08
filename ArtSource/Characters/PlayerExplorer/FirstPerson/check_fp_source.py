"""Open in Blender's Text Editor with PlayerExplorer_FPArms.blend, then Run Script.

Source-data checks only; does not validate the exported FBX or Unity playback.
Left for the user to run, as requested.
"""
import bpy

scene = bpy.data.scenes["PlayerExplorer_FP"]
rig = scene.objects["FP_Armature"]
assert len(rig.data.bones) == 54
assert rig.animation_data.action.name == "AN_FP_Idle"
assert (scene.frame_start, scene.frame_end, scene.render.fps) == (1, 91, 30)
triangles = 0
for name in ("FP_Arm_L", "FP_Arm_R"):
    arm = scene.objects[name]
    assert arm.parent == rig
    assert len(arm.data.uv_layers) > 0
    assert all(abs(value - 1) < 1e-5 for value in arm.scale)
    assert any(m.type == "ARMATURE" and m.object == rig for m in arm.modifiers)
    assert all(slot.material is not None for slot in arm.material_slots)
    for vertex in arm.data.vertices:
        assert vertex.groups, (name, vertex.index, "unweighted vertex")
        total = sum(group.weight for group in vertex.groups)
        assert abs(total - 1) < 0.002, (name, vertex.index, total)
        assert all(arm.vertex_groups[g.group].name in rig.data.bones
                   for g in vertex.groups if g.weight > 1e-6)
    triangles += sum(len(p.vertices) - 2 for p in arm.data.polygons)
assert triangles == 6788
assert "Armature" in bpy.data.scenes["Scene"].objects
print("FP source checks passed. FBX import and Unity/visual acceptance still required.")
