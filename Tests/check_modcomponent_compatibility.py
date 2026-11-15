"""Execute production package/crafting hooks for 7.x policy and API compatibility.

Reuses the shared real-consumer harness, SDK project and locked local references.
All generated state stays under Custom; no installation or game launch occurs.
"""
import argparse
import json
import os
from pathlib import Path
import sys
import tempfile

MOD = Path(__file__).resolve().parents[1]
WORKSPACE = MOD.parent
TOOLS = WORKSPACE / 'Custom/Scripts/ModBuild'
sys.path.insert(0, str(TOOLS))
from common import configure_libraries, read_json, sha, write_json
configure_libraries()
from build import asset_package, prune_cache, run
from environment import verify_environment


def main(package=None):
    local = read_json(WORKSPACE / 'Custom/.modbuild/local.json')
    game = Path(local['gamePath'])
    libraries = verify_environment()
    prune_cache(libraries)
    if package is None:
        profile = read_json(TOOLS / 'profiles.json')['TLDShotgun']
        package, _, _ = asset_package(MOD, profile, libraries)
    package = Path(package).resolve()
    upstream = WORKSPACE / 'Custom/.modbuild/references/DoubleBarrel/ModComponent.dll'
    installed = game / 'Mods/ModComponent.dll'
    if not upstream.is_file() or not installed.is_file():
        raise RuntimeError('Both cached upstream and installed ModComponent DLLs are required.')
    os.environ.setdefault('DOTNET_CLI_HOME', str(WORKSPACE / 'Custom/.dotnet-home'))
    os.environ['DOTNET_SKIP_FIRST_TIME_EXPERIENCE'] = '1'
    os.environ['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    root = WORKSPACE / 'Custom/.modbuild/build/ShotgunModComponentCompatibility'
    results = []
    for version in ('7.0.0', '7.1.0', '7.2.0', '7.99.0'):
        state = root / 'fixture'
        run(['dotnet', 'build', TOOLS/'Inspector/Inspector.csproj', '-c', 'Release',
             '-p:ModBuildRuntimeHarness=true', '-p:Version='+version, '-p:UseAppHost=false',
             '-p:NuGetAudit=false', '-p:RestoreSources='+str(WORKSPACE/'Custom/.nuget/packages'),
             '-p:RestorePackagesPath='+str(WORKSPACE/'Custom/.nuget/packages'),
             '-p:BaseIntermediateOutputPath='+str(state/'obj')+'/',
             '-p:OutputPath='+str(state/'bin')+'/',
             '-p:ModBuildRuntimePackage='+str(package),
             '-p:ModBuildHarmonyDirectory='+str(game/'MelonLoader/net6'),
             '-p:CustomAfterMicrosoftCommonTargets='+str(MOD/'Tests/ModComponentCompatibility.targets'),
             '--nologo', '--verbosity', 'quiet'],
            WORKSPACE/'Custom/.build-logs'/('shotgun-modcomponent-'+version+'-build.log'))
        log = WORKSPACE/'Custom/.build-logs'/('shotgun-modcomponent-'+version+'.log')
        # Reuse build outputs and discard sandbox packs; retain logs and the report.
        with tempfile.TemporaryDirectory(prefix='run-', dir=state) as work:
            assert Path(work).resolve().is_relative_to(state.resolve())
            run(['dotnet', state/'bin/ModComponent.dll', package,
                 TOOLS/'tests/fixtures/legacy-mapping-data.zip', MOD/'DoubleBarrel.modcomponent',
                 work, upstream, installed], log)
        result = json.loads(log.read_text(encoding='utf-8').strip().splitlines()[-1])
        assert result['status'] == 'PASS', result
        results.append(result)
        prune_cache(libraries)
        print(f"PASS: ModComponent {version}: {result['checks']} checks, real loader lifecycle and 8 crafting-option combinations.", flush=True)
    write_json(WORKSPACE/'Custom/.modbuild/reports/shotgun-modcomponent-compatibility.json',
               {'status': 'PASS', 'package': str(package), 'sha256': sha(package),
                'upstreamReference': {'path': str(upstream), 'sha256': sha(upstream)},
                'installedReference': {'path': str(installed), 'sha256': sha(installed)},
                'results': results, 'nativeGameExecuted': False})


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', nargs='?')
    main(parser.parse_args().package)
