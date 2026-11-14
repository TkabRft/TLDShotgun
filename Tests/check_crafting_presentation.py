"""Check crafting assets and execute production UI/quality helpers against a package.

Reuses the workspace ModBuild environment and inspector SDK project. With no
package argument, prepare the normal validated asset cache without publishing.
No deployment, game launch, or edits to the shared tools are performed.
"""
import argparse
import json
import os
from pathlib import Path
import sys
from zipfile import ZipFile

MOD = Path(__file__).resolve().parents[1]
WORKSPACE = MOD.parent
TOOLS = WORKSPACE / 'Custom/Scripts/ModBuild'
sys.path.insert(0, str(TOOLS))
from common import configure_libraries, read_json, sha, write_json
configure_libraries()
from assets import validate_externals
from build import asset_package, prune_cache, run
from bundle import Bundle
from catalog import Catalog
from environment import verify_environment


def main(package=None):
    spec = read_json(MOD / 'unity/SourceArt/no-unity-build.json')
    if package is None:
        profile = read_json(TOOLS / 'profiles.json')['TLDShotgun']
        libraries = verify_environment()
        prune_cache(libraries)
        package, _, _ = asset_package(MOD, profile, libraries)
    package = Path(package).resolve()
    checked = []
    with ZipFile(package) as archive:
        validate_externals(archive)
        bundle = Bundle(archive.read(spec['bundleEntry']), 'crafting-check')
        catalog = Catalog(json.loads(archive.read(spec['catalogEntry'])))
        for entry in spec['additionalSprites']:
            if not entry['file'].startswith('Crafting/'):
                continue
            name = entry['name']
            assert len(catalog.locate(name, 'Texture2D')) == 1, name
            assert len(catalog.locate(name, 'Sprite')) == 1, name
            texture = bundle.names[('Texture2D', name)]
            raw = texture.read_typetree()
            assert (raw['m_Width'], raw['m_Height'], raw['m_TextureFormat']) == (256, 256, 12), name
            image = texture.read().image.convert('RGBA')
            assert image.getchannel('A').getbbox() is not None, name
            for border in [(0, 0, 256, 8), (0, 248, 256, 256), (0, 0, 8, 256), (248, 0, 256, 256)]:
                assert image.getchannel('A').crop(border).getextrema() == (0, 0), name
            if name.startswith('ico_CraftItem__'):
                pixels = image.tobytes()
                assert all(min(pixels[i:i+3]) >= 245 for i in range(0, len(pixels), 4) if pixels[i+3] > 200), name
            checked.append(name)
        assert len(checked) == 12
        localization = json.loads(archive.read('localizations/localization.json'))
        for load in ('Slug', 'Buckshot', 'Birdshot', 'Scrapshot'):
            assert localization[f'GAMEPLAY_DoubleBarrel_{load}ShellName']['English'] == load+' Shell'
    state = WORKSPACE / 'Custom/.modbuild/build/ShotgunCraftingHarness'
    os.environ.setdefault('DOTNET_CLI_HOME', str(WORKSPACE / 'Custom/.dotnet-home'))
    os.environ['DOTNET_SKIP_FIRST_TIME_EXPERIENCE'] = '1'
    os.environ['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    run(['dotnet', 'build', TOOLS/'Inspector/Inspector.csproj', '-c', 'Release',
         '-p:ModBuildWeaponHarness=true', '-p:AssemblyName=CraftingPresentationHarness',
         '-p:UseAppHost=false', '-p:NuGetAudit=false',
         '-p:RestoreSources='+str(WORKSPACE/'Custom/.nuget/packages'),
         '-p:RestorePackagesPath='+str(WORKSPACE/'Custom/.nuget/packages'),
         '-p:BaseIntermediateOutputPath='+str(state/'obj')+'/',
         '-p:OutputPath='+str(state/'bin')+'/',
         '-p:CustomAfterMicrosoftCommonTargets='+str(MOD/'Tests/CraftingPresentation.targets'),
         '--nologo', '--verbosity', 'quiet'], WORKSPACE/'Custom/.build-logs/shotgun-crafting-harness-build.log')
    log = WORKSPACE/'Custom/.build-logs/shotgun-crafting-harness.log'
    run(['dotnet', state/'bin/CraftingPresentationHarness.dll', package], log)
    print(log.read_text(encoding='utf-8').strip())
    report = {'status': 'PASS', 'package': str(package), 'sha256': sha(package), 'typedArtwork': checked,
              'productionSource': True, 'nativeRenderingExecuted': False}
    write_json(WORKSPACE/'Custom/.modbuild/reports/shotgun-crafting-presentation.json', report)
    print('PASS: 12 cooked crafting textures, typed catalog addresses, transparency, monochrome pixels, and packaged shell labels.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', nargs='?')
    main(parser.parse_args().package)
