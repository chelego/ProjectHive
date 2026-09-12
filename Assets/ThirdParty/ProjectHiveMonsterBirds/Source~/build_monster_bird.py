"""Original ProjectHive creature. Run with Blender --background --python this_file.
Source reference: Cornell Lab Common Raven anatomy; no external meshes/textures/audio.
"""
import bpy, math, os, random, array, wave, struct
from mathutils import Vector, Matrix

if not bpy.app.background:
    raise RuntimeError('Run in a separate Blender --background --factory-startup process.')

random.seed(9313)
base = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = os.path.join(base, 'Model')
texdir = os.path.join(base, 'Textures')
audio_dir = os.path.join(base, 'Audio')
for directory in (out, texdir, audio_dir): os.makedirs(directory, exist_ok=True)
# Only the factory-startup background process created for this task is used.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

texture = bpy.data.images.new('AshRaven_Feathers', width=512, height=512)
pixels = array.array('f')
for y in range(512):
    for x in range(512):
        # Nested feather vanes / quill ridges; low contrast prevents glitter at distance.
        u, v = x / 512, y / 512
        quill = abs(math.sin(u * math.pi * 18)) ** 12
        vane = math.sin(v * 170 + abs(math.sin(u * math.pi * 18)) * 13)
        grain = random.uniform(-0.009, 0.009)
        shade = 0.09 + quill * 0.035 + vane * 0.012 + grain
        pixels.extend((shade * 0.75, shade * 0.86, shade, 1))
texture.pixels.foreach_set(pixels)
texture.filepath_raw = os.path.join(texdir, 'AshRaven_Feathers.png')
texture.file_format = 'PNG'; texture.save()

def material(name, color, roughness, textured=False):
    mat = bpy.data.materials.new(name); mat.diffuse_color=(*color,1); mat.use_nodes=True
    p=mat.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=roughness
    if textured:
        image=mat.node_tree.nodes.new('ShaderNodeTexImage'); image.image=texture
        mat.node_tree.links.new(image.outputs['Color'],p.inputs['Base Color'])
        bump=mat.node_tree.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=0.2; bump.inputs['Distance'].default_value=0.012
        mat.node_tree.links.new(image.outputs['Color'],bump.inputs['Height']); mat.node_tree.links.new(bump.outputs['Normal'],p.inputs['Normal'])
    return mat

feather=material('AshRaven_Feather',(.045,.05,.06),.72,True)
horn=material('AshRaven_Horn',(.065,.055,.043),.58)
flesh=material('AshRaven_ExposedSkin',(.065,.029,.025),.83)
eye=material('AshRaven_Eye',(.10,.08,.035),.22)
parts=[]
def bind(obj, name, bone, mat):
    obj.name=name; obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    vg=obj.vertex_groups.new(name=bone); vg.add(range(len(obj.data.vertices)),1,'REPLACE')
    for p in obj.data.polygons: p.use_smooth=True
    parts.append(obj); return obj

def ellipsoid(name, pos, scale, bone, mat, seg=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=pos)
    o=bpy.context.object; o.scale=scale
    return bind(o,name,bone,mat)

def wedge(name, points, thickness, bone, mat):
    verts=[(x,y,z+thickness/2) for x,y,z in points]+[(x,y,z-thickness/2) for x,y,z in points]
    n=len(points); faces=[tuple(range(n)),tuple(reversed(range(n,2*n)))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    return bind(obj,name,bone,mat)

def rod(name, a,b,radius,bone,mat):
    delta=Vector(b)-Vector(a)
    bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=radius,radius2=radius*.55,depth=delta.length,location=(Vector(a)+Vector(b))/2)
    obj=bpy.context.object; obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    return bind(obj,name,bone,mat)

# Upright raven with a narrow breast, robust bill, ragged throat, hooked toes.
ellipsoid('Breast',(0,0,.38),(.105,.22,.15),'Body',feather)
ellipsoid('Shoulders',(0,.12,.47),(.096,.13,.14),'Body',feather)
ellipsoid('Neck',(0,.18,.53),(.057,.085,.09),'Head',flesh)
ellipsoid('Skull',(0,.21,.60),(.073,.09,.075),'Head',feather)
wedge('Bill_Upper',[(-.045,.265,.60),(.045,.265,.60),(.026,.37,.585),(0,.407,.553),(-.027,.365,.565)],.035,'Head',horn)
wedge('Bill_Lower',[(-.031,.265,.558),(.031,.265,.558),(.015,.375,.55),(0,.385,.553),(-.015,.375,.55)],.012,'Jaw',horn)
for s in (-1,1):
    ellipsoid('Eye', (s*.069,.245,.62),(.006,.008,.007),'Head',eye,8,6)
    for k in range(3):
        x=s*(.024+k*.017)
        wedge('ThroatBarb',[(x-.009,.20,.51),(x+.009,.20,.51),(x,.24,.43-k*.013)],.008,'Head',feather)
    bone='Wing.L' if s<0 else 'Wing.R'
    wedge('WingMantle',[(s*.078,.115,.49),(s*.34,.09,.48),(s*.48,-.055,.45),(s*.34,-.22,.43),(s*.12,-.19,.43)],.042,bone,feather)
    for k in range(6):
        rootx=.21+k*.045; rooty=-.01-k*.012
        tipx=.40+k*.052; tipy=-.22-k*.035
        wedge('Primary',[(s*rootx,rooty,.46),(s*(rootx+.045),rooty,.46),(s*(tipx+.019),tipy,.435),(s*tipx,tipy-.036,.425),(s*(rootx-.01),rooty-.06,.435)],.012,bone,feather)
    for k in range(4):
        x=.105+k*.035
        wedge('Covert',[(s*x,.10,.51),(s*(x+.035),.08,.51),(s*(x+.08),-.13,.47),(s*(x+.035),-.17,.47)],.009,bone,feather)
    a=(s*.065,-.014,.285); b=(s*.074,-.066,.145); c=(s*.074,.011,.041)
    rod('Thigh',a,b,.023,'Legs',feather); rod('Tarsus',b,c,.011,'Legs',horn)
    for k in (-1,0,1):
        toe=(s*.074+k*.037,.112-abs(k)*.013,.017)
        rod('Toe',c,toe,.007,'Legs',horn)
        rod('Claw',toe,(toe[0],toe[1]+.019,.005),.005,'Legs',horn)
    rod('HindToe',c,(s*.08,-.071,.015),.006,'Legs',horn)
for k in range(7):
    x=(k-3)*.023; length=.23+(1-abs(k-3)/3)*.07
    wedge('TailFeather',[(x-.017,-.13,.38),(x+.017,-.13,.38),(x*1.7+.012,-.13-length,.33),(x*1.7,-.15-length,.33),(x*1.7-.012,-.13-length,.33)],.009,'Tail',feather)

# A single skinned mesh, rigid weighted appendages; clear authored silhouette at modest cost.
bpy.ops.object.select_all(action='DESELECT')
for p in parts:p.select_set(True)
bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
mesh=bpy.context.object; mesh.name='AshRaven_Mesh'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(island_margin=.015); bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.armature_add(location=(0,0,0)); rig=bpy.context.object; rig.name='AshRaven_Rig'
bpy.ops.object.mode_set(mode='EDIT'); eb=rig.data.edit_bones; eb.remove(eb[0])
def bone(name,head,tail,parent=None):
    b=eb.new(name); b.head=head; b.tail=tail
    if parent:b.parent=eb[parent]
bone('Root',(0,0,0),(0,0,.12))
bone('Body',(0,0,.30),(0,.10,.48),'Root')
bone('Head',(0,.13,.49),(0,.22,.61),'Body')
bone('Jaw',(0,.265,.558),(0,.375,.55),'Head')
bone('Wing.L',(-.075,.06,.47),(-.3,.06,.47),'Body')
bone('Wing.R',(.075,.06,.47),(.3,.06,.47),'Body')
bone('Tail',(0,-.1,.36),(0,-.37,.33),'Body')
bone('Legs',(0,-.01,.28),(0,0,.05),'Root')
bpy.ops.object.mode_set(mode='OBJECT')
mesh.parent=rig; mod=mesh.modifiers.new('Skin','ARMATURE'); mod.object=rig
for b in rig.pose.bones:b.rotation_mode='XYZ'
actions=[]
for name,end in [('Feed',72),('Flight',24)]:
    rig.animation_data_create(); action=bpy.data.actions.new(name); rig.animation_data.action=action
    for frame in range(1,end+1,3):
        phase=(frame-1)/(end-1)*math.pi*2
        for b in rig.pose.bones:b.rotation_euler=(0,0,0)
        for side,wingname in [(-1,'Wing.L'),(1,'Wing.R')]:
            rest=rig.data.bones[wingname].matrix_local.to_3x3()
            rotation=(Matrix.Rotation(math.radians(-side*78),3,'Z') @ Matrix.Rotation(math.radians(side*20),3,'Y')) if name=='Feed' else Matrix.Rotation(side*.85*math.sin(phase),3,'Y')
            rig.pose.bones[wingname].rotation_euler=(rest.inverted() @ rotation @ rest).to_euler('XYZ')
        if name=='Feed':
            rig.pose.bones['Head'].rotation_euler[0]=.30*max(0,math.sin(phase))
            rig.pose.bones['Head'].rotation_euler[2]=.18*math.sin(phase*.9)
        else:
            rig.pose.bones['Tail'].rotation_euler[0]=.12*math.sin(phase)
            rig.pose.bones['Legs'].rotation_euler[0]=-.55
            rig.pose.bones['Jaw'].rotation_euler[0]=-.18*max(0,math.sin(phase))
        for b in rig.pose.bones:b.keyframe_insert(data_path='rotation_euler',frame=frame,group=b.name)
    # Make the final key exactly equal to the first for seamless loops.
    bpy.context.scene.frame_set(1)
    for b in rig.pose.bones:b.keyframe_insert(data_path='rotation_euler',frame=end,group=b.name)
    action.use_fake_user=True; actions.append(action)
rig.animation_data.action=actions[0]; bpy.context.scene.frame_set(1)
bpy.context.scene.render.fps=30
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=os.path.join(out,'AshRaven.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
texture.pack()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'AshRaven.blend'))
mesh.data.calc_loop_triangles()
print('ASH_RAVEN_TRIANGLES',len(mesh.data.loop_triangles),'VERTICES',len(mesh.data.vertices))

# Original rough corvid-like alarm synthesized from harmonic pulses and breath noise.
rate=24000; duration=2.8
for variant in range(3):
    rng=random.Random(230+variant); samples=[]
    for n in range(int(duration*rate)):
        t=n/rate; val=0.0
        for start in (.12,.87,1.66):
            p=t-start
            if 0<=p<.48:
                envelope=(math.sin(math.pi*p/.48)**.7)*min(1,p/.035)
                frequency=320-160*p+variant*16+25*math.sin(36*p)
                # Integrated chirp phase with turbulent rasp; no copyrighted recordings.
                phase=2*math.pi*((320+variant*16)*p-80*p*p)
                voiced=sum(math.sin(phase*h)/h for h in (1,2,3,5,7))
                val+=envelope*(voiced*.32+rng.uniform(-1,1)*.20)*(0.8+.2*math.sin(t*132))
        samples.append(struct.pack('<h',int(max(-.92,min(.92,val))*32767)))
    with wave.open(os.path.join(audio_dir,f'AshRaven_Alarm_{variant+1:02}.wav'),'wb') as wav:
        wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(rate);wav.writeframes(b''.join(samples))

# Turntable evidence; separate camera/light objects are excluded from the FBX.
rig.animation_data.action=actions[0]; bpy.context.scene.frame_set(1)
world=bpy.context.scene.world; world.color=(.12,.12,.12)
for pos,power,size in [((2,1,3),350,3),((-2,0,1.8),200,2)]:
    bpy.ops.object.light_add(type='AREA',location=pos); light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,.35))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1.25,1.25,.85)); camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.34))-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.context.scene.camera=camera
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(os.path.dirname(__file__),'Bird_Preview.png');bpy.ops.render.render(write_still=True)
