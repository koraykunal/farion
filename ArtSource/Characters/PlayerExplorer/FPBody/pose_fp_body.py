"""Author FPS poses on the original connected skeleton, without stretching bones."""
from math import radians, sqrt, sin, pi
from pathlib import Path
import bpy
from mathutils import Vector, Matrix, Quaternion

OUT = Path(r'C:\Users\Koray\UnityProjects\farion\ArtSource\Characters\PlayerExplorer\FPBody')
scene = bpy.data.scenes['Explorer_FPBody']
bpy.context.window.scene = scene
rig = scene.objects['FPBody_Armature']
source = bpy.data.objects['Armature']
baseline = {p.name: p.matrix_basis.copy() for p in source.pose.bones}
rig.animation_data_clear()
scene.camera.rotation_euler = Vector((0, -1, 0)).to_track_quat('-Z', 'Y').to_euler()
scene.camera.location = rig.matrix_world @ source.pose.bones['CC_Base_Head'].head + Vector((0, -0.18, 0.065))

def reset():
    for p in rig.pose.bones:
        p.matrix_basis = baseline[p.name]
    bpy.context.view_layer.update()

def world(p):
    return rig.matrix_world @ p.matrix

def rotate_world(p, q):
    m = world(p)
    pos = m.translation.copy()
    m = q.to_matrix().to_4x4() @ m
    m.translation = pos
    p.matrix = rig.matrix_world.inverted() @ m
    bpy.context.view_layer.update()

def frame(forward, palm):
    y = Vector(forward).normalized()
    z = (Vector(palm) - y * Vector(palm).dot(y)).normalized()
    x = y.cross(z).normalized()
    return Matrix((x, y, z)).transposed()

def arm(side, wrist, fingers, palm, curl):
    prefix = 'CC_Base_' + side + '_'
    upper, fore, hand = [rig.pose.bones[prefix + n] for n in ('Upperarm', 'Forearm', 'Hand')]
    shoulder = world(upper).translation
    elbow = world(fore).translation
    old_wrist = world(hand).translation
    a, b = (elbow - shoulder).length, (old_wrist - elbow).length
    target = Vector(wrist)
    direction = target - shoulder
    distance = direction.length
    assert abs(a-b) + 0.001 < distance < a+b-0.001, (side, 'unreachable', distance, a+b)
    axis = direction.normalized()
    pole = Vector((0.4 if side == 'L' else -0.4, 0.05, 1.05)) - shoulder
    bend = (pole - axis * pole.dot(axis)).normalized()
    along = (a*a - b*b + distance*distance) / (2*distance)
    desired_elbow = shoulder + axis*along + bend*sqrt(max(0, a*a-along*along))
    rotate_world(upper, (elbow-shoulder).rotation_difference(desired_elbow-shoulder))
    rotate_world(fore, (world(hand).translation-world(fore).translation).rotation_difference(target-world(fore).translation))
    assert (world(hand).translation-target).length < 0.0001
    hand_pos = world(hand).translation
    middle = world(rig.pose.bones[prefix+'Mid1']).translation
    index = world(rig.pose.bones[prefix+'Index1']).translation
    pinky = world(rig.pose.bones[prefix+'Pinky1']).translation
    along_hand = middle-hand_pos
    palm_normal = along_hand.cross(index-pinky) * (1 if side=='L' else -1)
    rotation = frame(fingers, palm) @ frame(along_hand, palm_normal).transposed()
    rotate_world(hand, rotation.to_quaternion())
    curl_axis = Vector(fingers).cross(Vector(palm)).normalized()
    for finger, factor in [('Index', 0.8), ('Mid', 1), ('Ring', 1.05), ('Pinky', 1.1)]:
        for digit, amount in enumerate((0.72, 1.0, 0.7), 1):
            rotate_world(rig.pose.bones[prefix+finger+str(digit)], Quaternion(curl_axis, radians(curl*factor*amount)))
    # Oppose the thumb toward the index; actual prop dimensions still own final contact.
    if curl > 40:
        thumb = rig.pose.bones[prefix+'Thumb1']
        next_thumb = rig.pose.bones[prefix+'Thumb2']
        contact = world(rig.pose.bones[prefix+'Index2']).translation
        turn = (world(next_thumb).translation-world(thumb).translation).rotation_difference(
            contact-world(thumb).translation)
        rotate_world(thumb, Quaternion().slerp(turn, 0.65))
    for digit in (2, 3):
        rotate_world(rig.pose.bones[prefix+'Thumb'+str(digit)], Quaternion(curl_axis, radians(curl*0.2)))

POSES = {
    'AN_FPBody_Relaxed': [
        ('L', (0.23,-0.045,0.98), (0,-0.35,-1), ( -1,0,0), 12),
        ('R', (-0.23,-0.045,0.98), (0,-0.35,-1), (1,0,0), 12)],
    'AN_FPBody_ToolReady': [
        ('L', (0.23,-0.045,0.98), (0,-0.35,-1), (-1,0,0), 12),
        ('R', (-0.16,-0.285,1.43), (0,-0.92,0.38), (1,0,0), 65)],
    'AN_FPBody_TwoHandReady': [
        ('L', (0.17,-0.28,1.425), (-0.12,-0.97,0.20), (-1,0,0), 28),
        ('R', (-0.17,-0.28,1.425), (0.12,-0.97,0.20), (1,0,0), 28)]}

for name, targets in POSES.items():
    previous = bpy.data.actions.get(name)
    if previous:
        bpy.data.actions.remove(previous)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create().action = action
    for key in (1, 16, 31, 46, 61):
        scene.frame_set(key)
        reset()
        for side, wrist, fingers, palm, curl in targets:
            breathing = Vector((0, 0, 0.0015*sin((key-1)/60*2*pi)))
            arm(side, Vector(wrist)+breathing, fingers, palm, curl)
        for bone in rig.pose.bones:
            bone.rotation_mode = 'QUATERNION'
            for channel in ('location','rotation_quaternion','scale'):
                bone.keyframe_insert(channel, frame=key, group=bone.name)
    action['purpose'] = 'Connected full-body authoring pose. Item-specific grip and locomotion blending require Unity integration.'
scene.frame_start, scene.frame_end = 1, 61
rig.animation_data.action = bpy.data.actions['AN_FPBody_TwoHandReady']
scene.frame_set(1)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'PlayerExplorer_FPBody.blend'))
print('Authored three connected full-body poses, with original rest skeleton and skin weights.')
