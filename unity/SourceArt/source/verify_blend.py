"""Standalone Blender conversion/verification; not a Unity or runtime build.

Pass --normalize after staging the clean library through the live builder.
Otherwise only reopen and verify the current final editable project.
"""
import bpy
import hashlib
import json
import sys
from pathlib import Path

ART=Path(__file__).resolve().parents[1]
TARGET=ART/'Ammunition.blend'

def verify():
    if '--normalize' in sys.argv:
        stage=ART.parents[2]/'Custom/.asset-staging/shotgun-ammunition-v2/Ammunition-clean-library.blend'
        bpy.ops.wm.open_mainfile(filepath=str(stage))
        bpy.context.preferences.filepaths.save_version=0
        # Library writing intentionally omitted unrelated scenes, but a normal save
        # supplies standalone UI/workspace state and a predictable initial view.
        s=bpy.data.scenes['TLDShotgun_Ammo_Workshop'];bpy.context.window.scene=s
        for screen in bpy.data.screens:
            for area in screen.areas:
                if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
        bpy.ops.wm.save_as_mainfile(filepath=str(TARGET))
    bpy.ops.wm.open_mainfile(filepath=str(TARGET))
    assert [s.name for s in bpy.data.scenes]==['TLDShotgun_Ammo_Workshop']
    s=bpy.context.scene
    assert s.unit_settings.scale_length==1
    assert not any(o.type in {'FONT','ARMATURE'} for o in bpy.data.objects)
    assert not any(o.name in {'Cube','ShotgunAmmoSingleMesh','ShotgunAmmoBox_Mesh'} for o in bpy.data.objects)
    script=ART/'source/ammunition_assets.py'
    ns={'__file__':str(script),'__name__':'ammunition_verifier'}
    exec(compile(script.read_text(encoding='utf-8-sig'),str(script),'exec'),ns)
    expected=json.loads((ART/'asset-geometry.json').read_text())
    report={'scene':s.name,'units':'metres','meshes':{},'packed_textures':{},'sphere_clearances_m':{}}
    assert len([o for o in bpy.data.objects if o.type=='MESH'])==6
    for name,objects in ns['assets']():
        actual=ns['geometry_report'](objects)
        assert actual['triangles']==expected[name]['triangles']
        assert actual['nonmanifold_edges']==0
        assert max(abs(a-b) for a,b in zip(actual['dimensions_m'],expected[name]['dimensions_m']))<1e-7
        report['meshes'][name]=actual
        for o in objects:
            assert max(abs(v-1) for v in o.scale)<1e-7
            if 'minimum_sphere_clearance_m' in o:
                clearance=o['minimum_sphere_clearance_m'];assert clearance>=-1e-7
                report['sphere_clearances_m'][name]=clearance
            for mat in o.data.materials:
                for node in mat.node_tree.nodes:
                    if node.type!='TEX_IMAGE':continue
                    img=node.image;assert img and img.packed_file and len(img.pixels)>0
                    digest=hashlib.sha256(img.packed_file.data).hexdigest()
                    disk=ART.parent/'Assets/Shotgun/Textures'/Path(img.filepath).name
                    assert disk.exists() and digest==hashlib.sha256(disk.read_bytes()).hexdigest(),disk
                    report['packed_textures'][disk.name]=digest
    contents=bpy.data.objects['ShotgunScrapshotBox_Contents']
    assert contents['visible_shell_count']==6
    # Six disconnected closed shell surfaces, not a repeated image on the opening.
    vertices=contents.data.vertices
    adjacency={v.index:set() for v in vertices}
    for edge in contents.data.edges:
        a,b=edge.vertices;adjacency[a].add(b);adjacency[b].add(a)
    remaining=set(adjacency);components=0
    while remaining:
        stack=[remaining.pop()];components+=1
        while stack:
            for neighbor in adjacency[stack.pop()]:
                if neighbor in remaining:remaining.remove(neighbor);stack.append(neighbor)
    assert components==6
    report['visible_box_shells']=components
    assert len(report['packed_textures'])==4
    report['blend_sha256']=hashlib.sha256(TARGET.read_bytes()).hexdigest()
    (ART/'asset-blend-validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('AMMUNITION_BLEND_VERIFIED: 1 scene, 6 closed meshes, 6 visible carton shells, 4 matching packed textures; no lettering geometry.')

verify()
