"""Synchronize load-specific Unity materials, source prefab overrides and build input."""
from pathlib import Path
import importlib.util, json, re

ART=Path(__file__).resolve().parents[1]
ROOT=ART.parents[1]
ASSETS=ART.parent/'Assets/Shotgun'

def main():
    p=ART/'source/prepare_art_materials.py'
    spec=importlib.util.spec_from_file_location('materials',p);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
    m.main()
    manifest=ART/'no-unity-build.json';data=json.loads(manifest.read_text())
    data['materials']=[{'name':name,'texture':'unity/Assets/Shotgun/Textures/'+name+'_BaseColor.png','metallic':metal,'roughness':rough} for name,metal,rough in m.MATERIALS]
    manifest.write_text(json.dumps(data,indent=2)+'\n')
    def guid(path):return re.search(r'^guid: ([a-f0-9]{32})$',Path(str(path)+'.meta').read_text(),re.M)[1]
    for kind in ['Buckshot','Slug','Birdshot']:
        group=(ART.parent/'Assets/AddressableAssetsData/AssetGroups/shotgun.asset').read_text()
        for form in ['Single','Box']:
            icon=ASSETS/'Icons'/('ico_GearItem__DoubleBarrelAmmo'+form+kind+'.PNG')
            meta=Path(str(icon)+'.meta')
            if not meta.exists():
                match=re.search(r'm_GUID: ([a-f0-9]{32})\s+m_Address: '+re.escape(icon.stem)+r'\s',group)
                assert match,icon
                meta.write_text('fileFormatVersion: 2\nguid: '+match[1]+'\nTextureImporter:\n  serializedVersion: 13\n  externalObjects: {}\n  mipmaps:\n    enableMipMap: 0\n  textureType: 0\n  alphaIsTransparency: 1\n')
        shell_guid=guid(ASSETS/'Materials'/('Shotgun'+kind+'Shell.mat'))
        box_guid=guid(ASSETS/'Materials'/('Shotgun'+kind+'Box.mat'))
        # Each gear prefab keeps its existing GUID and serialized component IDs.
        wrapper=ASSETS/'Prefabs/GEAR_DoubleBarrelAmmoSingle_Mesh.prefab'
        variant=ASSETS/'Prefabs'/('GEAR_DoubleBarrelAmmoSingle'+kind+'_Mesh.prefab')
        text=wrapper.read_text();text=text.replace('guid: a57564c710be70c47aeb2af8ebfe9733','guid: '+shell_guid)
        variant.write_text(text)
        variant_guid=m.metadata(variant,'PrefabImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
        prefab=ASSETS/'Prefabs'/('GEAR_DoubleBarrelAmmoSingle'+kind+'.prefab')
        text=prefab.read_text().replace('guid: f207eb9075c46ca4bb9e5945712c6a92','guid: '+variant_guid)
        prefab.write_text(text)
        prefab=ASSETS/'Prefabs'/('GEAR_DoubleBarrelAmmoBox'+kind+'.prefab')
        text=prefab.read_text()
        for slot,mat in [(0,box_guid),(1,shell_guid)]:
            pattern=r"(propertyPath: '?m_Materials.Array.data\["+str(slot)+r"\]'?\n      value: *\n      objectReference: \{fileID: 2100000, guid: )[a-f0-9]{32}"
            text,count=re.subn(pattern,lambda match:match[1]+mat,text)
            assert count==1,(kind,slot,count)
        prefab.write_text(text)
    for kind in ['Buckshot','Slug','Birdshot']:
        for form in ['Shell','Box']:
            p=ASSETS/'Imports'/('Shotgun'+kind+form+'.fbx')
            m.metadata(p,'ModelImporter:\n  serializedVersion: 22200\n  externalObjects: {}\n  globalScale: 1\n  useFileScale: 1\n')
    print('Load materials, stable source-prefab overrides and package manifest synchronized.')

if __name__=='__main__':main()
