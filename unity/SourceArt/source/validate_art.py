"""Non-compiling validation of authored source art and its final-file manifest.

--record updates the manifest after deliberate final authoring/Blender validation.
Default mode verifies existing hashes and does not mutate assets.
"""
from pathlib import Path
import hashlib
import json
import re
import struct
import sys

ART=Path(__file__).resolve().parents[1]
UNITY=ART.parent
ASSETS=UNITY/'Assets/Shotgun'
NAMES=['ShotgunLeadBuckshot','ShotgunLeadBirdshot','ShotgunScrapBuckshot','ShotgunScrapshotShell','ShotgunScrapshotBox']
MATS=['ShotgunCastLead','ShotgunCutSteel','ShotgunScrapshotShell','ShotgunScrapshotBox']

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    files=[ART/'Ammunition.blend',ART/'asset-geometry.json',ART/'asset-roundtrip.json',ART/'asset-blend-validation.json',UNITY/'Assets/Editor/PrepareAmmunitionAssets.cs']
    for name in NAMES:
        fbx=ASSETS/'Imports'/(name+'.fbx');glb=ART/'exports'/(name+'.glb')
        assert fbx.read_bytes().startswith(b'Kaydara FBX Binary')
        header=glb.read_bytes()[:12];magic,version,length=struct.unpack('<4sII',header)
        assert magic==b'glTF' and version==2 and length==glb.stat().st_size
        files += [fbx,Path(str(fbx)+'.meta'),glb,ART/'previews'/(name+'.png')]
    icons=list((ASSETS/'Icons').glob('*Scrapshot*.png'))
    icons += [ASSETS/'Icons'/('ico_GearItem__DoubleBarrel'+kind+'.png') for kind in ['LeadBuckshot','LeadBirdshot','ScrapBuckshot']]
    assert len(icons)==6
    for icon in icons:
        png=icon.read_bytes();assert png[:8]==b'\x89PNG\r\n\x1a\n'
        assert struct.unpack('>II',png[16:24])==(256,256) and png[25]==6
        files += [icon,Path(str(icon)+'.meta')]
    guids=[]
    for name in MATS:
        mat=ASSETS/'Materials'/(name+'.mat');texture=ASSETS/'Textures'/(name+'_BaseColor.png')
        meta=Path(str(texture)+'.meta');guid=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(),re.M)[1]
        text=mat.read_text()
        assert f'm_Name: {name}\n' in text and f'guid: {guid}, type: 3' in text
        assert '_Mode: 0\n' in text and '_ZWrite: 1\n' in text
        files += [mat,Path(str(mat)+'.meta'),texture,meta]
    for path in files:
        if path.suffix=='.meta':guids.append(re.search(r'^guid: ([a-f0-9]{32})$',path.read_text(),re.M)[1])
    assert len(guids)==len(set(guids)),'Duplicate source-art GUID'
    geom=json.loads((ART/'asset-geometry.json').read_text());rt=json.loads((ART/'asset-roundtrip.json').read_text())
    for name in NAMES:
        assert geom[name]['nonmanifold_edges']==0 and geom[name]['triangles']<15000
        for fmt in ['fbx','glb']:
            assert rt[name][fmt]['triangles']==geom[name]['triangles']
            assert rt[name][fmt]['max_dimension_error_m']<1e-6 and rt[name][fmt]['textures_loaded']>0
    blend=json.loads((ART/'asset-blend-validation.json').read_text())
    assert blend['visible_box_shells']==6
    assert blend['blend_sha256']==digest(ART/'Ammunition.blend')
    for name,value in blend['packed_textures'].items():assert digest(ASSETS/'Textures'/name)==value
    files += [p for p in (ART/'source').glob('*.py')]+[ART/'author_ammunition.py']
    manifest={p.relative_to(UNITY).as_posix():digest(p) for p in sorted(set(files))}
    dest=ART/'asset-file-manifest.json'
    if '--record' in sys.argv:dest.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    assert json.loads(dest.read_text())==manifest,'Changed final art inputs; rerun affected verification before --record'
    print(f'PASS: {len(manifest)} source-art file hashes, 5 FBX/GLB pairs, 6 RGBA icons, 4 Unity material/texture references, 6 visible carton shells.')
    print('Source-art checks only; generated-package validation and game acceptance are separate checks.')

if __name__=='__main__':main()
