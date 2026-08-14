import bpy, os, json

RES = 128

MATERIALS = [
    'Back Jet Metal', 'Bolts Window', 'Cannons Paint Colored', 'Cannons Silver',
    'Cords', 'Engines Back Metal', 'Front Cannons Metal',
    'Front Cannons Metal Dark', 'Front Cannons With Stripes', 'Glass Window',
    'Jet Connectors', 'Jet Paint Metal', 'Landing Gear', 'Landing Gear Dark',
    'Metal Pipe', 'Orange Straps', 'Ship Circle Back Parts 1', 'Ship Door Hinge',
    'Ship Metal Smooth 1', 'Tanks', 'Window Bars', 'Yoke',
]


def log(m):
    print('[measure] %s' % m)


def uv_ok(obj):
    if not obj.data.uv_layers:
        return False
    uv = obj.data.uv_layers.active.data
    if not uv:
        return False
    us = [d.uv[0] for d in uv]
    vs = [d.uv[1] for d in uv]
    return (max(us) - min(us)) > 0.02 and (max(vs) - min(vs)) > 0.02


def host_for(mat):
    best = None
    for obj in bpy.data.objects:
        if obj.type != 'MESH' or not obj.data.materials:
            continue
        if mat not in list(obj.data.materials):
            continue
        if not uv_ok(obj):
            continue
        tris = len(obj.data.polygons)
        if best is None or tris > best[1]:
            best = (obj, tris)
    return best[0] if best else None


def make_img(name, colorspace, fill):
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, RES, RES, alpha=True)
    img.colorspace_settings.name = colorspace
    img.pixels[:] = list(fill) * (RES * RES)
    return img


def bind(mat, img):
    nt = mat.node_tree
    n = nt.nodes.get('__M_TARGET__')
    if n is None:
        n = nt.nodes.new('ShaderNodeTexImage')
        n.name = '__M_TARGET__'
        n.location = (-1400, 900)
    n.image = img
    for x in nt.nodes:
        x.select = False
    n.select = True
    nt.nodes.active = n


def unbind(mat):
    n = mat.node_tree.nodes.get('__M_TARGET__')
    if n:
        mat.node_tree.nodes.remove(n)


def cfg(samples):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.render.bake.margin = 0
    sc.render.bake.use_selected_to_active = False
    sc.render.bake.use_clear = True


def coverage_mask(mat, img):
    nt = mat.node_tree
    out = next((n for n in nt.nodes if n.bl_idname == 'ShaderNodeOutputMaterial'
                and n.is_active_output), None)
    original = out.inputs['Surface'].links[0].from_socket \
        if out.inputs['Surface'].is_linked else None
    emit = nt.nodes.new('ShaderNodeEmission')
    emit.inputs['Color'].default_value = (1, 1, 1, 1)
    nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])
    bind(mat, img)
    cfg(1)
    bpy.ops.object.bake(type='EMIT')
    nt.nodes.remove(emit)
    if original is not None:
        nt.links.new(original, out.inputs['Surface'])
    return [img.pixels[i * 4] > 0.5 for i in range(RES * RES)]


def masked_mean(img, mask, channels=1):
    px = list(img.pixels)
    acc = [0.0] * channels
    n = 0
    for i in range(RES * RES):
        if not mask[i]:
            continue
        j = i * 4
        for c in range(channels):
            acc[c] += px[j + c]
        n += 1
    if n == 0:
        return None
    return [a / n for a in acc]


def main():
    sc = bpy.context.scene
    prev = (sc.view_settings.view_transform, sc.view_settings.look)
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    result = {}
    for name in MATERIALS:
        mat = bpy.data.materials.get(name)
        if mat is None or not mat.node_tree:
            log('SKIP %s -- missing' % name)
            continue
        host = host_for(mat)
        if host is None:
            log('SKIP %s -- no host object with usable UVs' % name)
            result[name] = {'error': 'no_uv_host'}
            continue

        saved = [m for m in host.data.materials]
        for i in range(len(host.data.materials)):
            host.data.materials[i] = mat

        bpy.ops.object.select_all(action='DESELECT')
        host.select_set(True)
        bpy.context.view_layer.objects.active = host

        mask_img = make_img('__MSK__', 'Non-Color', (0, 0, 0, 1))
        mask = coverage_mask(mat, mask_img)
        covered = sum(1 for m in mask if m)

        base = make_img('__BC__', 'sRGB', (0, 0, 0, 1))
        nt = mat.node_tree
        out = next((n for n in nt.nodes
                    if n.bl_idname == 'ShaderNodeOutputMaterial'
                    and n.is_active_output), None)
        original = out.inputs['Surface'].links[0].from_socket \
            if out.inputs['Surface'].is_linked else None
        bsdf = next((n for n in nt.nodes
                     if n.bl_idname == 'ShaderNodeBsdfPrincipled'), None)
        emit = nt.nodes.new('ShaderNodeEmission')
        helper = None
        if bsdf is not None and bsdf.inputs['Base Color'].is_linked:
            nt.links.new(bsdf.inputs['Base Color'].links[0].from_socket,
                         emit.inputs['Color'])
        elif bsdf is not None:
            helper = nt.nodes.new('ShaderNodeRGB')
            helper.outputs[0].default_value = \
                tuple(bsdf.inputs['Base Color'].default_value)
            nt.links.new(helper.outputs[0], emit.inputs['Color'])
        nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])
        bind(mat, base)
        cfg(8)
        bpy.ops.object.bake(type='EMIT')
        nt.nodes.remove(emit)
        if helper is not None:
            nt.nodes.remove(helper)
        if original is not None:
            nt.links.new(original, out.inputs['Surface'])
        bc = masked_mean(base, mask, 3)

        rough = make_img('__RG__', 'Non-Color', (0, 0, 0, 1))
        bind(mat, rough)
        cfg(8)
        bpy.ops.object.bake(type='ROUGHNESS')
        rg = masked_mean(rough, mask, 1)

        unbind(mat)
        for img in (mask_img, base, rough):
            bpy.data.images.remove(img)
        for i in range(len(host.data.materials)):
            host.data.materials[i] = saved[i]

        entry = {
            'host': host.name,
            'covered_px': covered,
            'base_color_linear': [round(v, 4) for v in bc] if bc else None,
            'roughness': round(rg[0], 4) if rg else None,
            'smoothness': round(1.0 - rg[0], 4) if rg else None,
        }
        result[name] = entry
        log('%-30s host=%-22s base=%s rough=%s' % (
            name, host.name,
            ('[%.3f,%.3f,%.3f]' % tuple(entry['base_color_linear'])) if bc else '-',
            ('%.3f' % entry['roughness']) if rg else '-'))

    out = os.path.join(os.path.dirname(bpy.data.filepath), 'flat_materials_measured.json')
    with open(out, 'w', encoding='utf-8') as f:
        json.dump(result, f, indent=2)
    sc.view_settings.view_transform, sc.view_settings.look = prev
    log('written -> %s' % out)


main()
