"""Scene-scoped Blender authoring. Import safely; invoke explicit phases live.

Original shell/box geometry and textures are read-only. No Unity build/deployment.
"""
import bpy, bmesh, json, math, random
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

ART=Path(__file__).resolve().parents[1]
ASSETS=ART.parent/'Assets/Shotgun'
SCENE='TLDShotgun_Ammo_Workshop'
PREFIX='AMMO2_'
CATALOG=[('ShotgunLeadBuckshot','ico_GearItem__DoubleBarrelLeadBuckshot.png'),
 ('ShotgunLeadBirdshot','ico_GearItem__DoubleBarrelLeadBirdshot.png'),
 ('ShotgunScrapBuckshot','ico_GearItem__DoubleBarrelScrapBuckshot.png'),
 ('ShotgunScrapshotShell','ico_GearItem__DoubleBarrelAmmoSingleScrapshot.png'),
 ('ShotgunScrapshotBox','ico_GearItem__DoubleBarrelAmmoBoxScrapshot.png')]

def scene():
    s=bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
    bpy.context.window.scene=s
    s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
    return s

def select(objects):
    for o in bpy.context.view_layer.objects:o.select_set(False)
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0] if objects else None

def bounds(objects):
    bpy.context.view_layer.update()
    pts=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box]
    return Vector([min(p[a] for p in pts) for a in range(3)]),Vector([max(p[a] for p in pts) for a in range(3)])

def configure(s):
    # Values checked live: dynamic engine/colour-management RNA lists under-report.
    s.render.engine='CYCLES';s.cycles.samples=48;s.cycles.use_denoising=True
    s.render.film_transparent=True
    s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGBA'
    s.view_settings.view_transform='Standard';s.view_settings.look='None'
    s.view_settings.exposure=0;s.view_settings.gamma=1
    s.world=s.world or bpy.data.worlds.new(PREFIX+s.name)
    s.world.use_nodes=True
    bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND')
    bg.inputs['Color'].default_value=(.32,.37,.43,1);bg.inputs['Strength'].default_value=.35

def pixels(path):
    img=bpy.data.images.load(str(path),check_existing=True)
    a=np.empty(img.size[0]*img.size[1]*4,np.float32);img.pixels.foreach_get(a)
    return a.reshape(img.size[1],img.size[0],4).copy()

def write_image(name,a):
    h,w=a.shape[:2]
    img=bpy.data.images.get(name) or bpy.data.images.new(name,width=w,height=h,alpha=True)
    img.scale(w,h);img.colorspace_settings.name='sRGB'
    img.pixels.foreach_set(np.ascontiguousarray(a,dtype=np.float32).ravel())
    img.filepath_raw=str(ASSETS/'Textures'/(name+'.png'));img.file_format='PNG';img.save()
    return img

def texture_phase():
    scene()
    a=pixels(ASSETS/'Textures/ShotgunAmmoSingleMesh_standardSurface1_BaseColor.1001.png');rgb=a[:,:,:3]
    red=(rgb[:,:,0]>rgb[:,:,1]*1.18)&(rgb[:,:,0]>rgb[:,:,2]*1.18)
    lum=rgb[:,:,0]*.35+rgb[:,:,1]*.45+rgb[:,:,2]*.2
    grey=np.stack([lum*.72+.11,lum*.77+.12,lum*.8+.13],axis=2);rgb[red]=grey[red]
    write_image('ShotgunScrapshotShell_BaseColor',a)
    # Unprinted kraft cardboard. No labels, branding, lettering or factory artwork.
    h=w=2048;y,x=np.indices((h,w));rng=np.random.default_rng(731)
    cloud=.032*np.sin(x*.012+np.sin(y*.008)*2)+.026*np.sin(y*.019+x*.007)
    fiber=(rng.random((h,w))-.5)*.065+.012*np.sin(x*1.7+np.sin(y*.07))
    scuff=np.maximum(0,np.sin(y*.004+x*.003)-.73)*.065
    a=np.ones((h,w,4),np.float32)
    tone=cloud+fiber+scuff
    a[:,:,:3]=np.stack([.48+tone,.375+tone*.88,.265+tone*.70],axis=2)
    # Worn panel folds in the inherited UV atlas; shallow broad folds with a narrow
    # bright exposed-fibre edge. Fine surface grain remains visible at inspect size.
    for ux,uy,length,angle in [(.25,.1,.47,.05),(.25,.59,.47,-.03),(.19,.87,.30,.09),(.896,.345,.18,1.57),(.25,.35,.49,-.08)]:
        dx=x/w-ux;dy=y/h-uy;t=dx*math.cos(angle)+dy*math.sin(angle);d=-dx*math.sin(angle)+dy*math.cos(angle)
        fold=np.exp(-(d/.003)**2)*.085-np.exp(-((d-.006)/.004)**2)*.045
        fold*=np.clip((length/2-np.abs(t))/.025,0,1)
        a[:,:,:3]+=fold[:,:,None]*np.array([1,.9,.72])
    write_image('ShotgunScrapshotBox_BaseColor',a)
    n=256;y,x=np.indices((n,n));rng=np.random.default_rng(1203)
    cloud=.5+.22*np.sin(x*.037+np.sin(y*.023)*2)+.12*np.sin(y*.073+x*.019)
    base=.25+.09*cloud+(rng.random((n,n))-.5)*.024
    for name,scrap in [('ShotgunCastLead_BaseColor',False),('ShotgunCutSteel_BaseColor',True)]:
        tile=np.ones((n,n,4),np.float32)
        if scrap:
            score=np.maximum(0,np.sin(y*1.6+np.sin(x*.04))-.97)*.6;rust=np.clip(np.sin(x*.045)*np.sin(y*.033)-.53,0,1)*.07
            tile[:,:,:3]=np.stack([base+rust,base*1.04-rust*.3,base*1.09-rust*.9],axis=2)-score[:,:,None]
        else:tile[:,:,:3]=np.stack([base*.95,base*1.02,base*1.10],axis=2)
        write_image(name,np.clip(tile,0,1))
    print('Four atlases authored: unmarked grey hull, unprinted worn kraft cardboard, two metals.')

def material(name,texture,metal,rough):
    mat=bpy.data.materials.get(name) or bpy.data.materials.new(name);mat.use_nodes=True
    nodes=mat.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');tex=nodes.new('ShaderNodeTexImage')
    tex.image=bpy.data.images.load(str(ASSETS/'Textures'/(texture+'.png')),check_existing=True)
    mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);mat.node_tree.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
    bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough;mat.diffuse_color=(.38,.41,.43,1)
    return mat

def collection(name):
    s=scene();c=bpy.data.collections.get(name)
    if c is None:c=bpy.data.collections.new(name);s.collection.children.link(c)
    elif c.name not in s.collection.children:raise RuntimeError('Collection outside task scene: '+name)
    if c.objects:raise RuntimeError('Already authored: inspect/attach instead of rebuilding '+name)
    c.hide_viewport=False;c.hide_render=False
    return c

def move(obj,col):
    for old in list(obj.users_collection):old.objects.unlink(obj)
    col.objects.link(obj)

def unwrap(obj):
    select([obj]);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')

def normalize(objects):
    lo,hi=bounds(objects);offset=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for o in objects:
        o.data=o.data.copy();o.data.transform(Matrix.Translation(-offset)@o.matrix_world);o.matrix_world=Matrix.Identity(4)
    return list(hi-lo)

def pile_phase():
    scene();lead=material('ShotgunCastLead','ShotgunCastLead_BaseColor',.30,.68);steel=material('ShotgunCutSteel','ShotgunCutSteel_BaseColor',.45,.62)
    for kind,radius in [('LeadBuckshot',.0036),('LeadBirdshot',.0017),('ScrapBuckshot',.003)]:
        col=collection('Shotgun'+kind);rng=random.Random(kind+'v2');objects=[]
        if kind=='LeadBuckshot':positions=[(-1.1,-1.9),(1.1,-1.9),(-2.2,0),(0,0),(2.2,0),(-1.1,1.9),(1.1,1.9),(0,-1.27),(0,1.27)]
        elif kind=='LeadBirdshot':
            positions=[];bird_heights=[]
            # Three close-packed layers, offset into triangular hollows. Small jitter
            # preserves clearance and a loose edge without a grid or unstable tower.
            for layer,ring in [(0,4),(1,2),(2,1)]:
                for row in range(-ring,ring+1):
                    for column in range(-ring,ring+1):
                        if max(abs(row),abs(column),abs(row+column))>ring:continue
                        if layer==0 and (row,column) in [(-4,0),(4,0)]:continue
                        x=(column+row*.5)*2.10+layer*1.05+rng.uniform(-.025,.025)
                        y=row*1.81865+layer*.6062+rng.uniform(-.025,.025)
                        positions.append((x,y));bird_heights.append(radius*(1+layer*1.65))
        else:positions=[(-2.8,-1.9),(-.4,-2.2),(2.1,-2),(-3.2,.6),(-.4,.3),(2.4,.4),(-2.5,2.8),(.2,3),(3,2.8),(4.3,-.9)]
        for i,(x,y) in enumerate(positions):
            if kind=='ScrapBuckshot':
                bpy.ops.mesh.primitive_cube_add(size=1,location=(x*radius,y*radius,0));o=bpy.context.object
                o.scale=(radius*rng.uniform(1.6,2.05),radius*rng.uniform(.7,1.15),radius*rng.uniform(.65,1.0))
                select([o]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
                for v in o.data.vertices:v.co.x+=v.co.y*.45+rng.uniform(-.00045,.00045);v.co.y+=rng.uniform(-.00025,.00025)
                mod=o.modifiers.new('Cut edge chamfer','BEVEL');mod.width=.00035;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
                o.rotation_euler=(rng.uniform(-.20,.20),rng.uniform(-.16,.16),rng.uniform(-1.1,1.1));o.data.materials.append(steel)
            else:
                bpy.ops.mesh.primitive_uv_sphere_add(segments=20 if kind=='LeadBuckshot' else 10,ring_count=12 if kind=='LeadBuckshot' else 6,radius=radius,location=(x*radius,y*radius,0));o=bpy.context.object
                for p in o.data.polygons:p.use_smooth=True
                o.data.materials.append(lead)
            o.name=PREFIX+kind+'_'+str(i+1);move(o,col)
            bpy.context.view_layer.update();o.location.z=-min((o.matrix_world.to_3x3()@Vector(c)).z for c in o.bound_box)
            if kind=='LeadBuckshot' and i>=7:o.location.z=radius*2.56
            if kind=='LeadBirdshot':
                o.location.z=bird_heights[i]
            if kind=='ScrapBuckshot':unwrap(o)
            objects.append(o)
        clearance=None
        if kind!='ScrapBuckshot':
            clearance=min((a.location-b.location).length-2*radius for i,a in enumerate(objects) for b in objects[i+1:])
            assert clearance>=-1e-7,(kind,'Intersecting spheres',clearance)
        select(objects);bpy.ops.object.join();o=bpy.context.object;o.name='Shotgun'+kind;normalize([o])
        if clearance is not None:o['minimum_sphere_clearance_m']=clearance
        o['visual_only_projectile_count']=len(positions);o['portion_weight_kg']=.02
        o['note']='Visual pile only; quantity and combat pellet count are separate.'
    print('Three distinct UV-mapped piles authored.')

def shell_box_phase():
    scene();hull=material('ShotgunScrapshotShell','ShotgunScrapshotShell_BaseColor',.15,.70);box=material('ShotgunScrapshotBox','ShotgunScrapshotBox_BaseColor',0,.82)
    for kind,filename in [('Shell','ShotgunAmmoSingleMesh.fbx'),('Box','ShotgunAmmoBoxMesh_v2.fbx')]:
        col=collection('ShotgunScrapshot'+kind);before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(ASSETS/'Imports'/filename))
        imported=[o for o in bpy.data.objects if o not in before];objects=[o for o in imported if o.type=='MESH'];original=normalize(objects)
        if kind=='Box':
            # The original closed model contains five embedded shells. Replace its
            # whole visual with an open, six-shell carton; never retain hidden ammo.
            for o in objects:
                mesh=o.data;bpy.data.objects.remove(o,do_unlink=True)
                if not mesh.users:bpy.data.meshes.remove(mesh)
            carton,contents=open_carton(original,box,hull)
            move(carton,col);move(contents,col)
            carton['source_fbx']=filename;carton['baseline_dimensions_m']=original
            continue
        for i,o in enumerate(objects):
            o.name='ShotgunScrapshot'+kind+(str(i) if i else '');move(o,col)
            if kind=='Shell':o.data.materials.clear();o.data.materials.append(hull)
            else:
                for index in range(len(o.data.materials)):o.data.materials[index]=box if index==0 else hull
            o['baseline_dimensions_m']=original;o['source_fbx']=filename;o['marking']='No added lettering or labels.'
        for o in imported:
            if o.type!='MESH':bpy.data.objects.remove(o,do_unlink=True)
    print('Original shell geometry/UVs retained; open six-shell carton fits baseline box proportions.')

def open_carton(size,mat,hull):
    """Rough folded carton, open at +X, containing six actual unchanged shell meshes.

    Keep baseline carton footprint/height and each shell's original proportions.
    Panels are thin closed solids; the open mouth is intentional, not a mesh hole.
    """
    s=scene();half_x=size[0]/2-.0006;half_y=size[1]/2-.0006;height=.100
    panels=[]
    def panel(name,nx,ny,position):
        verts=[position(i/(nx-1),j/(ny-1)) for j in range(ny) for i in range(nx)]
        faces=[]
        for j in range(ny-1):
            for i in range(nx-1):
                a=j*nx+i;faces.append((a,a+1,a+nx+1,a+nx))
        mesh=bpy.data.meshes.new(PREFIX+name);mesh.from_pydata(verts,[],faces);mesh.materials.append(mat)
        obj=bpy.data.objects.new(PREFIX+name,mesh);s.collection.objects.link(obj)
        uv=mesh.uv_layers.new(name='CardboardUV')
        for loop in mesh.loops:
            j,i=divmod(loop.vertex_index,nx);uv.data[loop.index].uv=(.055+i/(nx-1)*.41,.24+j/(ny-1)*.245)
        select([obj]);mod=obj.modifiers.new('Cardboard thickness','SOLIDIFY');mod.thickness=.0011;mod.offset=0
        bpy.ops.object.modifier_apply(modifier=mod.name);panels.append(obj)
    panel('CartonBottom',17,9,lambda u,v:((u*2-1)*half_x,(v*2-1)*half_y,.001+.0003*math.sin(u*12+v*7)))
    for side in [-1,1]:
        def wall(u,v,side=side):
            x=(u*2-1)*half_x
            # Broken top edge and broad inward dents, with a sharper diagonal fold.
            dent=.0024*math.sin(u*13+v*4)+.0015*math.exp(-((u+v*.3-.65)/.055)**2)
            y=side*(half_y-dent*math.sin(v*math.pi))
            z=v*(height+.0018*math.sin(u*16)+.0007*math.sin(u*43))
            if u>.97:x-=.0007*math.sin(v*23)**2
            return x,y,z
        panel('CartonSide'+str(side),23,8,wall)
    panel('CartonClosedEnd',11,8,lambda u,v:(-half_x+.0012*math.sin(u*7+v*4)*math.sin(v*math.pi),(u*2-1)*half_y,v*height))
    # Cover the rear portion. The open end remains clear so hulls and brass heads show.
    panel('CartonRearLid',17,11,lambda u,v:(-half_x+u*(half_x+.008),(v*2-1)*(half_y-.0004),height+.0015*math.sin(u*11+v*7)))
    # A short, bent-back end flap lifts away from the visible cartridges.
    panel('CartonBentFlap',13,6,lambda u,v:(.008-v*.037,(u*2-1)*(half_y-.0005),height+v*(size[2]-.002-height)+.001*math.sin(u*13)*v))
    select(panels);bpy.ops.object.join();carton=bpy.context.object;carton.name='ShotgunScrapshotBox'
    carton['surface']='Unprinted creased kraft carton, open at +X, rough bent-back flap.'
    shells=[];baseline=bpy.data.collections['ShotgunScrapshotShell'].objects[0]
    rotation=Matrix.Rotation(-math.pi/2,4,'Y')
    for row in range(2):
        for column in range(3):
            mesh=baseline.data.copy()
            transform=Matrix.Translation(Vector((half_x-.010,(column-1)*.0445,.0245+row*.0455)))@rotation
            mesh.transform(transform)
            obj=bpy.data.objects.new(PREFIX+'PackedShell'+str(row*3+column+1),mesh);s.collection.objects.link(obj);shells.append(obj)
    select(shells);bpy.ops.object.join();contents=bpy.context.object;contents.name='ShotgunScrapshotBox_Contents'
    contents['visible_shell_count']=6
    contents['note']='Six display meshes; inventory count is controlled by the stack definition.'
    return carton,contents

def assets():return [(name,[o for o in bpy.data.collections[name].objects if o.type=='MESH']) for name,_ in CATALOG]

def setup_studio():
    s=scene();configure(s);studio=bpy.data.collections.get(PREFIX+'Studio')
    if studio is None:studio=bpy.data.collections.new(PREFIX+'Studio');s.collection.children.link(studio)
    if s.camera is None:
        d=bpy.data.cameras.new(PREFIX+'Camera');cam=bpy.data.objects.new(d.name,d);studio.objects.link(cam);s.camera=cam;d.type='ORTHO'
    if not any(o.type=='LIGHT' for o in studio.objects):
        for name,pos,power,size,color in [('Key',(-.5,-.8,1.1),12,.8,(1,.92,.82)),('Fill',(.8,-.1,.7),8,1,(.78,.86,1)),('Rim',(.1,.8,.9),12,.65,(1,1,1))]:
            d=bpy.data.lights.new(PREFIX+name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
            o=bpy.data.objects.new(d.name,d);studio.objects.link(o);o.location=pos;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
    return s

def frame(name,resolution=512):
    s=setup_studio()
    for asset,_ in CATALOG:
        bpy.data.collections[asset].hide_render=asset!=name;bpy.data.collections[asset].hide_viewport=asset!=name
    objects=dict(assets())[name];lo,hi=bounds(objects);center=(lo+hi)*.5;direction=Vector((3,-6,4))
    if name.endswith('Shell'):
        o=objects[0];uv=o.data.uv_layers.active.data
        candidates=[(abs(uv[l.index].uv.x-.26)+abs(uv[l.index].uv.y-.62),o.matrix_world@o.data.vertices[l.vertex_index].co) for l in o.data.loops]
        direction=min(candidates,key=lambda item:item[0])[1]-center;direction.z=.03
    elif name.endswith('Box'):direction=Vector((6,-4,3.2))
    s.camera.location=center+direction.normalized()*.9;s.camera.rotation_euler=(center-s.camera.location).to_track_quat('-Z','Y').to_euler()
    bpy.context.view_layer.update();inv=s.camera.matrix_world.inverted();pts=[inv@(o.matrix_world@Vector(c)) for o in objects for c in o.bound_box]
    s.camera.data.ortho_scale=max(max(p.x for p in pts)-min(p.x for p in pts),max(p.y for p in pts)-min(p.y for p in pts))*1.20
    s.render.resolution_x=resolution;s.render.resolution_y=resolution;s.render.resolution_percentage=100
    return s

def render_asset(name,resolution=512,icon=False):
    s=frame(name,resolution);s.render.filepath=str(ASSETS/'Icons'/dict(CATALOG)[name] if icon else ART/'previews'/(name+'.png'))
    bpy.ops.render.render(write_still=True)
    if icon and name.endswith('Shell'):bpy.data.images['Render Result'].save_render(str(ASSETS/'Icons/ico_DoubleBarrelScrapshotHUD.png'),scene=s)
    print('Rendered '+Path(s.render.filepath).name)

def geometry_report(objects):
    lo,hi=bounds(objects);tris=verts=bad=0
    for o in objects:
        m=o.data;m.calc_loop_triangles();tris+=len(m.loop_triangles);verts+=len(m.vertices)
        assert all(math.isfinite(v) for p in m.vertices for v in p.co),'Nonfinite vertex'
        assert m.uv_layers.active,'Missing UVs'
        assert all(math.isfinite(v) for uv in m.uv_layers.active.data for v in uv.uv),'Nonfinite UV'
        assert all(mat and mat.use_nodes for mat in m.materials),'Missing material'
        bm=bmesh.new();bm.from_mesh(m);bad+=sum(not e.is_manifold for e in bm.edges);bm.free()
    return {'dimensions_m':list(hi-lo),'triangles':tris,'vertices':verts,'nonmanifold_edges':bad,'mesh_count':len(objects),'materials':sorted({m.name for o in objects for m in o.data.materials})}

def export_phase():
    scene();report={}
    for name,objects in assets():
        for asset,_ in CATALOG:bpy.data.collections[asset].hide_viewport=False
        select(objects);report[name]=geometry_report(objects);assert report[name]['triangles']<15000
        bpy.ops.export_scene.fbx(filepath=str(ASSETS/'Imports'/(name+'.fbx')),use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='RELATIVE',use_triangles=True)
        bpy.ops.export_scene.gltf(filepath=str(ART/'exports'/(name+'.glb')),export_format='GLB',use_selection=True,use_active_scene=True,export_apply=True)
    (ART/'asset-geometry.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(report))

def roundtrip_phase():
    original=scene();report={};expected=json.loads((ART/'asset-geometry.json').read_text())
    for name,_ in CATALOG:
        report[name]={}
        for fmt in ['fbx','glb']:
            snapshot={k:set(getattr(bpy.data,k)) for k in ['objects','meshes','materials','images','collections']}
            temp=bpy.data.scenes.new(PREFIX+'Roundtrip');bpy.context.window.scene=temp
            try:
                path=ASSETS/'Imports'/(name+'.fbx') if fmt=='fbx' else ART/'exports'/(name+'.glb')
                if fmt=='fbx':bpy.ops.import_scene.fbx(filepath=str(path))
                else:bpy.ops.import_scene.gltf(filepath=str(path))
                objs=[o for o in temp.objects if o.type=='MESH'];actual=geometry_report(objs)
                assert actual['mesh_count']==expected[name]['mesh_count'];assert actual['triangles']==expected[name]['triangles']
                delta=max(abs(a-b) for a,b in zip(actual['dimensions_m'],expected[name]['dimensions_m']));assert delta<1e-5,(name,fmt,delta)
                textures=[n.image for o in objs for m in o.data.materials for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
                # Imported embedded GLB images are lazy until their pixel buffers are read.
                assert textures and all(len(i.pixels)>0 and i.size[0]>0 for i in textures),'Missing imported textures'
                report[name][fmt]={'max_dimension_error_m':delta,'triangles':actual['triangles'],'textures_loaded':len(textures)}
            finally:
                bpy.context.window.scene=original;bpy.data.scenes.remove(temp)
                for k in ['objects','collections','meshes','materials','images']:
                    data=getattr(bpy.data,k)
                    for item in set(data)-snapshot[k]:
                        if k in ['objects','collections'] or not item.users:data.remove(item,do_unlink=True)
    (ART/'asset-roundtrip.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(report))

def save_library(path):
    s=scene();frame('ShotgunScrapshotBox',512)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
    for _,objects in assets():
        for o in objects:
            for m in o.data.materials:
                for n in m.node_tree.nodes:
                    if n.type=='TEX_IMAGE' and n.image and not n.image.packed_file:n.image.pack()
    bpy.data.libraries.write(str(path),{s},path_remap='RELATIVE_ALL',fake_user=False,compress=True)
    print('Isolated task scene staged for standalone save: '+str(path))
