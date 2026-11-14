"""Derive crafting-only artwork from existing HUD/inventory icons; no model edits.

Run with Pillow. --check verifies deterministic pixels, registration and names
without writing files. Existing inventory/HUD assets and localization are inputs.
"""
import argparse
import json
from pathlib import Path
import uuid

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ICONS = ROOT / 'unity/Assets/Shotgun/Icons'
MANIFEST = ROOT / 'unity/SourceArt/no-unity-build.json'
LOCALIZATION = ROOT / 'unity/Assets/_ModComponent/shotgun/localizations/Localization.json'
GROUP = ROOT / 'unity/Assets/AddressableAssetsData/AssetGroups/shotgun.asset'
LOADS = ('Slug', 'Buckshot', 'Birdshot', 'Scrapshot')


def framed(image, *, angle=0, monochrome=False):
    image = image.convert('RGBA')
    bounds = image.getchannel('A').getbbox()
    if not bounds:
        raise ValueError('Empty source artwork')
    image = image.crop(bounds)
    if monochrome:
        alpha = image.getchannel('A')
        image = Image.new('RGBA', image.size, (255, 255, 255, 0))
        image.putalpha(alpha)
    if angle:
        image = image.rotate(angle, Image.Resampling.BICUBIC, expand=True)
    image = image.crop(image.getchannel('A').getbbox())
    image.thumbnail((224, 224), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (256, 256))
    canvas.alpha_composite(image, ((256-image.width)//2, (256-image.height)//2))
    if monochrome:
        # BC3 encodes RGB separately from alpha. White transparent texels prevent
        # black compression fringes when native crafting tints the silhouette.
        alpha = canvas.getchannel('A')
        canvas = Image.new('RGBA', canvas.size, (255, 255, 255, 0))
        canvas.putalpha(alpha)
    return canvas


def artwork():
    for load in LOADS:
        with Image.open(ICONS / f'ico_DoubleBarrel{load}HUD.png') as image:
            yield f'ico_CraftItem__DoubleBarrelAmmoSingle{load}', framed(image, angle=-40, monochrome=True)
        suffix = '.png' if load == 'Scrapshot' else '.PNG'
        with Image.open(ICONS / f'ico_GearItem__DoubleBarrelAmmoSingle{load}{suffix}') as image:
            yield f'ico_CraftingPreview__DoubleBarrelAmmoSingle{load}', framed(image, angle=-40)
    with Image.open(ICONS / 'ico_DoubleBarrelEmptyHUD.png') as image:
        yield 'ico_CraftItem__DoubleBarrelAmmoCasing', framed(image, angle=-40, monochrome=True)
    for material in ('LeadBuckshot', 'LeadBirdshot', 'ScrapBuckshot'):
        with Image.open(ICONS / f'ico_GearItem__DoubleBarrel{material}.png') as image:
            yield f'ico_CraftItem__DoubleBarrel{material}', framed(image, monochrome=True)


def main(check=False, preview=None):
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8-sig'))
    localization = json.loads(LOCALIZATION.read_text(encoding='utf-8-sig'))
    group = GROUP.read_text(encoding='utf-8')
    entries = manifest.setdefault('additionalSprites', [])
    images = list(artwork())
    folder_meta = ICONS / 'Crafting.meta'
    folder_guid = uuid.uuid5(uuid.NAMESPACE_URL, 'TLDShotgun/crafting/folder').hex
    if check:
        assert f'guid: {folder_guid}\n' in folder_meta.read_text()
    elif not folder_meta.exists():
        folder_meta.write_text(f'fileFormatVersion: 2\nguid: {folder_guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n', encoding='utf-8')
    for name, image in images:
        relative = 'Crafting/' + name + '.png'
        path = ICONS / relative
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'TLDShotgun/crafting/' + name).hex
        entry = {'name': name, 'file': relative}
        if check:
            with Image.open(path) as actual:
                assert actual.mode == 'RGBA' and actual.size == (256, 256), name
                assert actual.tobytes() == image.tobytes(), 'Stale crafting artwork: ' + name
            assert entry in entries and sum(e['name'] == name for e in entries) == 1, name
            assert f'guid: {guid}\n' in Path(str(path)+'.meta').read_text(), name
            assert f'm_GUID: {guid}\n    m_Address: {name}\n' in group, name
            bounds = image.getchannel('A').getbbox()
            assert min(bounds[:2]) >= 16 and max(bounds[2:]) <= 240, name
            if name.startswith('ico_CraftItem__'):
                pixels = image.tobytes()
                assert all(pixels[i:i+3] == b'\xff\xff\xff' for i in range(0, len(pixels), 4) if pixels[i+3]), name
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            image.save(path)
            meta = Path(str(path)+'.meta')
            if meta.exists():
                assert f'guid: {guid}\n' in meta.read_text(), 'Unexpected existing GUID: ' + name
            else:
                meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n'
                                '  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n'
                                '  isReadable: 0\n  textureType: 8\n  spriteMode: 1\n'
                                '  alphaIsTransparency: 1\n  maxTextureSize: 256\n', encoding='utf-8')
            previous = [e for e in entries if e['name'] == name]
            if previous:
                assert previous == [entry], 'Conflicting crafting asset: ' + name
            else:
                entries.append(entry)
            if f'    m_Address: {name}\n' not in group:
                new_entry = (f'  - m_GUID: {guid}\n    m_Address: {name}\n    m_ReadOnly: 0\n'
                             '    m_SerializedLabels: []\n    FlaggedDuringContentUpdateRestriction: 0\n')
                group = group.replace('  m_ReadOnly: 0\n  m_Settings:', new_entry+'  m_ReadOnly: 0\n  m_Settings:')
    languages = next(iter(localization.values())).keys()
    for load in LOADS:
        key = f'GAMEPLAY_DoubleBarrel_{load}ShellName'
        if check:
            assert localization[key]['English'] == load+' Shell'
        else:
            localization.setdefault(key, {language: '' for language in languages})['English'] = load+' Shell'
    if not check:
        MANIFEST.write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
        LOCALIZATION.write_text(json.dumps(localization, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
        GROUP.write_text(group, encoding='utf-8')
    if preview:
        sheet = Image.new('RGB', (1024, 864), (25, 30, 29))
        draw = ImageDraw.Draw(sheet)
        for i, (name, image) in enumerate(images):
            x, y = (i % 4)*256, (i // 4)*288
            sheet.paste(image, (x, y), image)
            draw.text((x+10, y+258), name.split('__')[-1].replace('DoubleBarrel', ''), fill='white')
            draw.text((x+10, y+272), 'Color preview' if 'Preview' in name else 'Crafting silhouette', fill='white')
        preview = Path(preview)
        preview.parent.mkdir(parents=True, exist_ok=True)
        sheet.save(preview)
    print(('PASS: verified' if check else 'Prepared') + ' 12 crafting textures/addresses and 4 localized shell names; original inventory/HUD assets preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--preview', help='Optional contact-sheet output path')
    args = parser.parse_args()
    main(args.check, args.preview)
