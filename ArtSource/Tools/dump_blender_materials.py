import bpy, json, os

PROCEDURAL = {
    'ShaderNodeTexNoise', 'ShaderNodeTexVoronoi', 'ShaderNodeTexMusgrave',
    'ShaderNodeTexWave', 'ShaderNodeTexMagic', 'ShaderNodeTexGradient',
    'ShaderNodeTexChecker', 'ShaderNodeTexBrick', 'ShaderNodeValToRGB',
    'ShaderNodeMixRGB', 'ShaderNodeMix', 'ShaderNodeMath', 'ShaderNodeVectorMath',
    'ShaderNodeBevel', 'ShaderNodeAmbientOcclusion', 'ShaderNodeNewGeometry',
    'ShaderNodeLayerWeight', 'ShaderNodeFresnel', 'ShaderNodeAttribute',
}

TRACKED = ['Base Color', 'Metallic', 'Roughness', 'Normal', 'Emission',
           'Emission Color', 'Emission Strength', 'Alpha', 'IOR', 'Specular',
           'Specular IOR Level', 'Transmission', 'Transmission Weight',
           'Coat Weight', 'Sheen Weight']


def trace(socket, depth=0):
    if not socket.is_linked:
        v = socket.default_value
        try:
            return {'kind': 'value', 'value': [round(x, 4) for x in v]}
        except TypeError:
            return {'kind': 'value', 'value': round(v, 4)}
    if depth > 12:
        return {'kind': 'too_deep'}

    node = socket.links[0].from_node
    if node.bl_idname == 'ShaderNodeTexImage':
        img = node.image
        return {
            'kind': 'image',
            'file': os.path.basename(img.filepath_raw) if img else None,
            'size': list(img.size) if img else None,
            'colorspace': img.colorspace_settings.name if img else None,
            'projection': node.projection,
            'extension': node.extension,
        }
    if node.bl_idname == 'ShaderNodeNormalMap':
        return {'kind': 'normal_map', 'strength': round(node.inputs['Strength'].default_value, 3),
                'source': trace(node.inputs['Color'], depth + 1)}
    if node.bl_idname == 'ShaderNodeBump':
        return {'kind': 'bump', 'strength': round(node.inputs['Strength'].default_value, 3),
                'distance': round(node.inputs['Distance'].default_value, 4),
                'source': trace(node.inputs['Height'], depth + 1)}

    entry = {'kind': 'node', 'type': node.bl_idname, 'label': node.name,
             'needs_bake': node.bl_idname in PROCEDURAL, 'inputs': {}}
    for inp in node.inputs:
        if inp.is_linked or inp.name in ('Color1', 'Color2', 'A', 'B', 'Fac', 'Color', 'Height', 'Vector'):
            entry['inputs'][inp.name] = trace(inp, depth + 1)
    return entry


def flatten_flags(node, acc):
    if isinstance(node, dict):
        if node.get('needs_bake'):
            acc.add(node.get('type'))
        for v in node.values():
            flatten_flags(v, acc)
    elif isinstance(node, list):
        for v in node:
            flatten_flags(v, acc)
    return acc


report = {}
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    bsdf = next((n for n in mat.node_tree.nodes
                 if n.bl_idname == 'ShaderNodeBsdfPrincipled'), None)
    if bsdf is None:
        report[mat.name] = {'_warning': 'no Principled BSDF',
                            '_output_nodes': [n.bl_idname for n in mat.node_tree.nodes]}
        continue

    entry = {'_blend_method': getattr(mat, 'blend_method', None),
             '_backface_culling': mat.use_backface_culling}
    for name in TRACKED:
        if name in bsdf.inputs:
            entry[name] = trace(bsdf.inputs[name])
    entry['_needs_bake'] = sorted(flatten_flags(entry, set()))
    report[mat.name] = entry

out = os.path.join(os.path.dirname(bpy.data.filepath), 'materials_dump.json')
with open(out, 'w', encoding='utf-8') as f:
    json.dump(report, f, indent=2, ensure_ascii=False)

print('=' * 78)
print('%-34s %-9s %-9s %s' % ('MATERIAL', 'METALLIC', 'ROUGH', 'BASE COLOR / BAKE'))
print('=' * 78)
for name, e in sorted(report.items()):
    if '_warning' in e:
        print('%-34s  !! %s' % (name[:34], e['_warning']))
        continue
    def brief(k):
        d = e.get(k)
        if not d:
            return '-'
        if d['kind'] == 'value':
            v = d['value']
            return ('%.2f' % v) if isinstance(v, float) else '[%s]' % ','.join('%.2f' % x for x in v[:3])
        if d['kind'] == 'image':
            return (d['file'] or '?')[:26]
        return '<%s>' % d['kind']
    bake = (' BAKE:' + ','.join(t.replace('ShaderNode', '') for t in e['_needs_bake'])) if e['_needs_bake'] else ''
    print('%-34s %-9s %-9s %s%s' % (name[:34], brief('Metallic'), brief('Roughness'), brief('Base Color'), bake))
print('=' * 78)
print('JSON written to: %s' % out)
