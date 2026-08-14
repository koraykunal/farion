import bpy, os

PREFIX = 'TX_PlayerStarterShuttle_'
OUTPUT_DIR = 'BakedForUnity'
MARGIN = 24
AO_SAMPLES = 64
NORMAL_SAMPLES = 32
COLOR_SAMPLES = 16
BAKE_AO = True
ONLY_AO = False

TARGETS = {
    'Ship Hull': 4096,
    'Ship Plates 1': 4096,
    'Ship Plates 2': 4096,
    'Wings': 4096,
    'Wing Fins': 4096,
    'Back Fins': 4096,
    'Ship Side Beams': 4096,
    'Door': 4096,
    'Back Plate': 2048,
    'Back Plates': 2048,
    'Cockpit Floor': 2048,
    'Seat 1': 2048,
    'Seat 2': 1024,
    'Monitors': 1024,
    'Monitor Stand': 1024,
}


def log(msg):
    print('[bake] %s' % msg)


def out_dir():
    base = os.path.dirname(bpy.data.filepath)
    if not base:
        raise RuntimeError('Save the .blend file first.')
    d = os.path.join(base, OUTPUT_DIR)
    os.makedirs(d, exist_ok=True)
    return d


def safe(name):
    return ''.join(c if c.isalnum() or c in '-_' else '_' for c in name)


def principled_of(mat):
    if not mat or not mat.node_tree:
        return None
    return next((n for n in mat.node_tree.nodes
                 if n.bl_idname == 'ShaderNodeBsdfPrincipled'), None)


def is_emissive(mat):
    bsdf = principled_of(mat)
    if bsdf is None:
        return any(n.bl_idname in ('ShaderNodeEmission', 'ShaderNodeBsdfPrincipled')
                   for n in mat.node_tree.nodes)

    strength = bsdf.inputs.get('Emission Strength')
    if strength is not None and not strength.is_linked \
            and float(strength.default_value) <= 0.0:
        return False

    for key in ('Emission Color', 'Emission'):
        socket = bsdf.inputs.get(key)
        if socket is None:
            continue
        if socket.is_linked:
            return True
        try:
            if max(socket.default_value[:3]) > 0.0:
                return True
        except TypeError:
            continue
    return False


def new_image(name, res, colorspace, fill):
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, res, res, alpha=True, float_buffer=False)
    img.colorspace_settings.name = colorspace
    img.pixels[:] = list(fill) * (res * res)
    return img


def bind(mat, img):
    nt = mat.node_tree
    node = nt.nodes.get('__BAKE_TARGET__')
    if node is None:
        node = nt.nodes.new('ShaderNodeTexImage')
        node.name = '__BAKE_TARGET__'
        node.location = (-1400, 800)
    node.image = img
    for n in nt.nodes:
        n.select = False
    node.select = True
    nt.nodes.active = node


def unbind(mat):
    nt = mat.node_tree
    node = nt.nodes.get('__BAKE_TARGET__')
    if node:
        nt.nodes.remove(node)


def active_output(mat):
    nt = mat.node_tree
    out = next((n for n in nt.nodes if n.bl_idname == 'ShaderNodeOutputMaterial'
                and n.is_active_output), None)
    if out is None:
        out = next((n for n in nt.nodes
                    if n.bl_idname == 'ShaderNodeOutputMaterial'), None)
    return out


def push_emit(mat, socket_name, fallback=0.0):
    nt = mat.node_tree
    out = active_output(mat)
    if out is None:
        return None
    original = out.inputs['Surface'].links[0].from_socket \
        if out.inputs['Surface'].is_linked else None

    emit = nt.nodes.new('ShaderNodeEmission')
    emit.name = '__BAKE_EMIT__'
    emit.location = (-600, 800)
    helper = None

    bsdf = principled_of(mat)
    if bsdf is not None and socket_name in bsdf.inputs \
            and bsdf.inputs[socket_name].is_linked:
        nt.links.new(bsdf.inputs[socket_name].links[0].from_socket,
                     emit.inputs['Color'])
    else:
        if bsdf is not None and socket_name in bsdf.inputs:
            raw = bsdf.inputs[socket_name].default_value
        else:
            raw = fallback
        try:
            col = (float(raw[0]), float(raw[1]), float(raw[2]), 1.0)
        except TypeError:
            v = float(raw)
            col = (v, v, v, 1.0)
        helper = nt.nodes.new('ShaderNodeRGB')
        helper.name = '__BAKE_CONST__'
        helper.location = (-900, 800)
        helper.outputs[0].default_value = col
        nt.links.new(helper.outputs[0], emit.inputs['Color'])

    nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])
    return (out, original, emit, helper)


def pop_emit(mat, state):
    if state is None:
        return
    out, original, emit, helper = state
    nt = mat.node_tree
    nt.nodes.remove(emit)
    if helper is not None:
        nt.nodes.remove(helper)
    if original is not None:
        nt.links.new(original, out.inputs['Surface'])


def enable_gpu():
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
    except KeyError:
        log('cycles addon unavailable -- staying on CPU')
        return False

    chosen = None
    for backend in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI', 'METAL'):
        try:
            prefs.compute_device_type = backend
        except TypeError:
            continue
        prefs.get_devices()
        if any(d.type == backend for d in prefs.devices):
            chosen = backend
            break

    if chosen is None:
        log('no GPU backend available -- staying on CPU')
        return False

    for device in prefs.devices:
        device.use = device.type in (chosen, 'CPU')
    names = [d.name for d in prefs.devices if d.use and d.type == chosen]
    log('GPU backend %s -> %s' % (chosen, ', '.join(names)))
    return True


def configure(samples, denoise=False):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = denoise
    sc.render.bake.margin = MARGIN
    sc.render.bake.use_selected_to_active = False
    sc.render.bake.use_clear = True
    try:
        sc.render.bake.margin_type = 'EXTEND'
    except (AttributeError, TypeError):
        pass


def isolate(target):
    hidden = []
    for other in bpy.data.objects:
        if other is target or other.type != 'MESH':
            continue
        if not other.hide_render:
            other.hide_render = True
            hidden.append(other)
    return hidden


def restore(hidden):
    for other in hidden:
        other.hide_render = False


def save(img, path):
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    log('  wrote %s' % os.path.basename(path))


def compose_ms(metallic, roughness, res, path):
    n = res * res
    m = list(metallic.pixels)
    r = list(roughness.pixels)
    tmp = bpy.data.images.get('__MS__')
    if tmp:
        bpy.data.images.remove(tmp)
    tmp = bpy.data.images.new('__MS__', res, res, alpha=True)
    tmp.colorspace_settings.name = 'Non-Color'
    buf = [0.0] * (n * 4)
    for i in range(n):
        j = i * 4
        buf[j] = m[j]
        buf[j + 3] = 1.0 - r[j]
    tmp.pixels[:] = buf
    tmp.alpha_mode = 'CHANNEL_PACKED'
    save(tmp, path)
    bpy.data.images.remove(tmp)


def main():
    sc = bpy.context.scene
    prev = (sc.view_settings.view_transform, sc.view_settings.look,
            sc.render.engine)
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    if enable_gpu():
        try:
            sc.cycles.device = 'GPU'
        except (AttributeError, TypeError):
            pass

    directory = out_dir()
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    missing = [n for n in TARGETS if n not in bpy.data.objects]
    if missing:
        log('WARNING objects not found: %s' % ', '.join(missing))

    for obj_name, res in sorted(TARGETS.items()):
        obj = bpy.data.objects.get(obj_name)
        if obj is None or obj.type != 'MESH':
            continue
        if not obj.data.uv_layers:
            log('SKIP %s -- no UV map' % obj_name)
            continue

        mats = []
        for m in obj.data.materials:
            if m and m.node_tree and m not in mats:
                mats.append(m)
        if not mats:
            log('SKIP %s -- no node materials' % obj_name)
            continue

        log('%s @ %d  (%d materials)' % (obj_name, res, len(mats)))
        was_hidden = obj.hide_render
        obj.hide_render = False
        obj.hide_viewport = False
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj

        stem = PREFIX + safe(obj_name)
        base = new_image('__BC__', res, 'sRGB', (0.5, 0.5, 0.5, 1.0))
        metal = new_image('__MT__', res, 'Non-Color', (0.0, 0.0, 0.0, 1.0))
        rough = new_image('__RG__', res, 'Non-Color', (0.5, 0.5, 0.5, 1.0))
        norm = new_image('__NM__', res, 'Non-Color', (0.5, 0.5, 1.0, 1.0))
        occ = new_image('__AO__', res, 'Non-Color', (1.0, 1.0, 1.0, 1.0)) \
            if BAKE_AO else None

        emissive = [] if ONLY_AO else [m for m in mats if is_emissive(m)]
        emission = None
        if emissive:
            log('  emissive: %s' % ', '.join(m.name for m in emissive))
            emission = new_image('__EM__', res, 'sRGB', (0.0, 0.0, 0.0, 1.0))
            configure(COLOR_SAMPLES)
            for m in mats:
                bind(m, emission)
            bpy.ops.object.bake(type='EMIT')

        all_principled = all(principled_of(m) is not None for m in mats)
        configure(COLOR_SAMPLES)
        if ONLY_AO:
            pass
        elif all_principled:
            states = [push_emit(m, 'Base Color') for m in mats]
            for m in mats:
                bind(m, base)
            bpy.ops.object.bake(type='EMIT')
            for m, st in zip(mats, states):
                pop_emit(m, st)
        else:
            log('  no Principled on every slot -- DIFFUSE fallback for BaseColor')
            for m in mats:
                bind(m, base)
            bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})

        if not ONLY_AO:
            configure(COLOR_SAMPLES)
            for m in mats:
                bind(m, rough)
            bpy.ops.object.bake(type='ROUGHNESS')

            states = [push_emit(m, 'Metallic') for m in mats]
            for m in mats:
                bind(m, metal)
            configure(COLOR_SAMPLES)
            bpy.ops.object.bake(type='EMIT')
            for m, st in zip(mats, states):
                pop_emit(m, st)

            configure(NORMAL_SAMPLES)
            for m in mats:
                bind(m, norm)
            bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT')

        if BAKE_AO:
            hidden = isolate(obj)
            configure(AO_SAMPLES, denoise=True)
            for m in mats:
                bind(m, occ)
            bpy.ops.object.bake(type='AO')
            restore(hidden)

            px = list(occ.pixels)
            count = res * res
            lit = sum(1 for i in range(0, count * 4, 4) if px[i] > 0.05)
            coverage = lit / float(count)
            if coverage < 0.02:
                log('  WARNING occlusion is black (%.1f%% lit) -- writing white '
                    'instead so it cannot kill indirect light' % (coverage * 100.0))
                occ.pixels[:] = [1.0, 1.0, 1.0, 1.0] * count

        if not ONLY_AO:
            save(base, os.path.join(directory, '%s_BaseColor.png' % stem))
            save(norm, os.path.join(directory, '%s_Normal.png' % stem))
            if emission is not None:
                save(emission, os.path.join(directory, '%s_Emission.png' % stem))
            compose_ms(metal, rough, res,
                       os.path.join(directory, '%s_MetallicSmoothness.png' % stem))

        if BAKE_AO:
            save(occ, os.path.join(directory, '%s_Occlusion.png' % stem))

        for m in mats:
            unbind(m)
        for img in (base, metal, rough, norm, occ, emission):
            if img:
                bpy.data.images.remove(img)
        obj.hide_render = was_hidden

    sc.view_settings.view_transform, sc.view_settings.look, sc.render.engine = prev
    log('DONE -> %s' % directory)


main()
