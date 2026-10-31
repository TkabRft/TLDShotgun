"""Write explicit opaque Unity Standard materials and stable metadata, without Unity.

Only these four new materials/textures are owned here. Existing GUIDs are retained.
Run with regular Python after authoring the texture images in Blender.
"""
from pathlib import Path
import re
import uuid

ASSETS=Path(__file__).resolve().parents[2]/'Assets/Shotgun'
MATERIALS=[('ShotgunCastLead',.30,.68),('ShotgunCutSteel',.45,.62),
           ('ShotgunScrapshotShell',.15,.70),('ShotgunScrapshotBox',0,.82)]

def metadata(path,body):
    meta=Path(str(path)+'.meta')
    if meta.exists():return re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(),re.M)[1]
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'TLDShotgun/'+path.relative_to(ASSETS).as_posix()).hex
    meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+body,encoding='utf-8')
    return guid

def main():
    for name,metal,rough in MATERIALS:
        texture=ASSETS/'Textures'/(name+'_BaseColor.png')
        assert texture.exists(),texture
        guid=metadata(texture,'TextureImporter:\n  serializedVersion: 13\n  externalObjects: {}\n  mipmaps:\n    enableMipMap: 1\n    sRGBTexture: 1\n  isReadable: 0\n  textureType: 0\n  textureShape: 1\n  maxTextureSize: 2048\n  alphaIsTransparency: 0\n')
        mat=ASSETS/'Materials'/(name+'.mat')
        text=f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {{fileID: 2800000, guid: {guid}, type: 3}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _Metallic: {metal}
    - _Glossiness: {1-rough:.2f}
    - _Mode: 0
    - _SrcBlend: 1
    - _DstBlend: 0
    - _ZWrite: 1
    - _Cull: 2
    - _Cutoff: 0.5
    m_Colors:
    - _Color: {{r: 1, g: 1, b: 1, a: 1}}
    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 1}}
  m_BuildTextureStacks: []
'''
        mat.write_text(text,encoding='utf-8')
        metadata(mat,'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    print('Four Unity materials and texture GUID references prepared; no editor/build invoked.')

if __name__=='__main__':main()
