import bpy

print('=' * 100)
print('%-34s %7s %6s %-22s %5s %s' % ('OBJECT', 'TRIS', 'MATS', 'UV MAPS', 'HIDE', 'UV COVERAGE'))
print('=' * 100)

total_tris = 0
for obj in sorted(bpy.data.objects, key=lambda o: o.name):
    if obj.type != 'MESH':
        continue
    me = obj.data
    tris = sum(max(len(p.vertices) - 2, 0) for p in me.polygons)
    total_tris += tris
    uvs = [l.name for l in me.uv_layers]

    cov = '-'
    if me.uv_layers:
        uv = me.uv_layers.active.data
        us = [d.uv[0] for d in uv]
        vs = [d.uv[1] for d in uv]
        if us:
            cov = 'u %.2f..%.2f  v %.2f..%.2f' % (min(us), max(us), min(vs), max(vs))

    print('%-34s %7d %6d %-22s %5s %s' % (
        obj.name[:34], tris, len(me.materials),
        (','.join(uvs) or 'NONE')[:22],
        'Y' if obj.hide_render else '', cov))

print('=' * 100)
print('total triangles: %d' % total_tris)
print()
print('--- material -> objects ---')
usage = {}
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        continue
    for m in obj.data.materials:
        if m:
            usage.setdefault(m.name, []).append(obj.name)
for name in sorted(usage):
    print('%-34s %s' % (name[:34], ', '.join(sorted(set(usage[name])))[:60]))

print()
print('--- non-principled materials ---')
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    if any(n.bl_idname == 'ShaderNodeBsdfPrincipled' for n in mat.node_tree.nodes):
        continue
    kinds = [n.bl_idname.replace('ShaderNode', '') for n in mat.node_tree.nodes]
    print('%-34s %s' % (mat.name[:34], ', '.join(kinds)))
