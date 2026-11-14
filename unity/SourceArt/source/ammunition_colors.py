"""Explicit live Blender phases for load colors. Importing changes nothing."""
import bpy, json
import numpy as np
from pathlib import Path

ART=Path(__file__).resolve().parents[1]
ASSETS=ART.parent/'Assets/Shotgun'
COLORS={'Buckshot':(.70,.075,.055),'Slug':(.045,.14,.36),'Birdshot':(.10,.43,.16)}

def attach():
    path=ART/'source/ammunition_assets.py'
    ns={'__file__':str(path),'__name__':'ammunition_builder'}
    exec(compile(path.read_text(encoding='utf-8-sig'),str(path),'exec'),ns)
    return ns

def recolor(ns, source, name, target, shell=False):
    a=ns['pixels'](ASSETS/'Textures'/source);rgb=a[:,:,:3]
    r,g,b=rgb[:,:,0],rgb[:,:,1],rgb[:,:,2]
    mask=(r>g*1.18)&(r>b*1.18) if shell else (g>r*1.08)&(g>b*1.08)
    if not shell and 'Buckshot' in source:
        mask=(r>g*1.08)&(g>b*1.08)&(g>r*.5)
    elif not shell and 'Birdshot' in source:
        mask=(b>r*1.08)&(b>g*1.08)
    # Keep texture wear, ink, brass and neutral panels. Only load-color pixels change.
    shade=np.max(rgb,axis=2)
    if not (shell and name.startswith('ShotgunBuckshot')):
        rgb[mask]=np.clip(shade[mask,None]*np.array(target)/.70,0,1)
    return ns['write_image'](name,a)

def author():
    ns=attach();ns['scene']()
    for kind,color in COLORS.items():
        for form in ['Shell','Box']:
            name='Shotgun'+kind+form
            if bpy.data.collections.get(name):raise RuntimeError('Already authored: '+name)
            if form=='Shell':
                recolor(ns,'ShotgunAmmoSingleMesh_standardSurface1_BaseColor.1001.png',name+'_BaseColor',color,True)
            else:
                recolor(ns,'ShotgunAmmoBox'+kind+'_BaseColor.png',name+'_BaseColor',color)
            mat=ns['material'](name,name+'_BaseColor',.15 if form=='Shell' else 0,.70 if form=='Shell' else .82)
            col=ns['collection'](name);before=set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=str(ASSETS/'Imports'/('ShotgunAmmoSingleMesh.fbx' if form=='Shell' else 'ShotgunAmmoBoxMesh_v2.fbx')))
            imported=set(bpy.data.objects)-before;objects=[o for o in imported if o.type=='MESH']
            baseline=ns['normalize'](objects)
            for i,o in enumerate(objects):
                o.name=name+('_'+str(i) if i else '');ns['move'](o,col)
                for j in range(len(o.data.materials)):
                    o.data.materials[j]=mat if form=='Shell' or j==0 else bpy.data.materials['Shotgun'+kind+'Shell']
                o['baseline_dimensions_m']=baseline
            for o in imported:
                if o.type!='MESH':bpy.data.objects.remove(o,do_unlink=True)
    print('Six load-specific shell/box assets authored; existing geometry and UVs retained.')

def export_and_check():
    ns=attach();ns['export_phase']();ns['roundtrip_phase']()

def grey_carton():
    ns=attach();a=ns['pixels'](ASSETS/'Textures/ShotgunScrapshotBox_BaseColor.png')
    lum=a[:,:,:3]@np.array([.30,.59,.11]);a[:,:,:3]=lum[:,:,None]
    img=ns['write_image']('ShotgunScrapshotBox_BaseColor',a)
    for node in bpy.data.materials['ShotgunScrapshotBox'].node_tree.nodes:
        if node.type=='TEX_IMAGE':node.image=img

def render(kind):
    ns=attach()
    for form in ['Shell','Box']:
        name='Shotgun'+kind+form
        ns['render_asset'](name);ns['render_asset'](name,256,True)

def stage():
    ns=attach();path=ART.parents[2]/'Custom/.asset-staging/shotgun-ammunition-v2/Ammunition-clean-library.blend'
    path.parent.mkdir(parents=True,exist_ok=True);ns['save_library'](path)
