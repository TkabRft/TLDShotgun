"""Run focused shared production tests with updated native surface/audio stand-ins.

The shared tooling stays unchanged. Only its native boundary fixture is extended;
production C# is linked directly by the existing harness.
"""
from pathlib import Path
import importlib.util
import sys
import unittest

MOD = Path(__file__).resolve().parents[1]
TOOLS = MOD.parent / 'Custom/Scripts/ModBuild'
sys.path.insert(0, str(TOOLS))
from common import STATE, configure_libraries
configure_libraries()


def main():
    state = STATE / 'build/ShotgunUpstreamMerge'
    state.mkdir(parents=True, exist_ok=True)
    boundary = (TOOLS / 'tests/WeaponHarness.cs').read_text(encoding='utf-8')
    replacements = {
        'public void PlayOneShot(AudioClip clip) => Plays++;': '''public float LastScale;
        public void PlayOneShot(AudioClip clip) => PlayOneShot(clip, 1f);
        public void PlayOneShot(AudioClip clip, float volumeScale) { Plays++; LastScale = volumeScale; }''',
        'public static class GameManager\n    {': '''public static class GameManager
    {
        public static Transform? SurfacePlayer = new();
        public static Transform? GetPlayerTransform() => SurfacePlayer;''',
        'namespace UnityEngine\n{': '''namespace UnityEngine
{
    public class Transform { public Vector3 position; }''',
        'int ammoQualityChecks = AmmoQualityHarness.Run();': 'int ammoQualityChecks = AmmoQualityHarness.Run(); UpstreamAudioTests.Run();',
        'legacyDurationRejects = PcmHarness.LegacyDurationRejects': 'upstreamAudioChecks = UpstreamAudioTests.Checks, legacyDurationRejects = PcmHarness.LegacyDurationRejects',
    }
    for old, new in replacements.items():
        assert boundary.count(old) == 1, 'Shared native boundary changed: ' + old
        boundary = boundary.replace(old, new)
    path = state / 'SurfaceAwareWeaponHarness.cs'
    path.write_text(boundary, encoding='utf-8')
    wildlife = (TOOLS / 'tests/ShotgunWildlifeBoundary.cs').read_text(encoding='utf-8')
    marker = 'public static class Utils\n{'
    assert wildlife.count(marker) == 1, 'Shared wildlife boundary changed'
    wildlife = wildlife.replace(marker, marker + '''
    public static string Surface = "Stone";
    public static int Queries;
    public static string GetMaterialTagUnderPosition(Vector3 position) { Queries++; return Surface; }
''')
    wildlife_path = state / 'SurfaceAwareWildlifeBoundary.cs'
    wildlife_path.write_text(wildlife, encoding='utf-8')
    spec = importlib.util.spec_from_file_location('shotgun_pipeline_checks', TOOLS / 'tests/test_pipeline.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    original_run = module.run

    def run(args, *rest, **kwargs):
        if len(args) > 1 and args[0] == 'dotnet' and args[1] == 'build':
            args = list(args) + ['-p:RestoreSources=' + str(MOD.parent / 'Custom/.nuget/packages'), '-p:NuGetAudit=false']
            if not any(str(a).startswith('-p:RestorePackagesPath=') for a in args):
                args.append('-p:RestorePackagesPath=' + str(MOD.parent / 'Custom/.nuget/packages'))
        if '-p:ModBuildWeaponHarness=true' in args:
            args = [a for a in args if not str(a).startswith('-p:CustomAfterMicrosoftCommonTargets=')]
            args += ['-p:CustomAfterMicrosoftCommonTargets=' + str(MOD / 'Tests/UpstreamWeapon.targets'),
                     '-p:MergeWeaponBoundaryPath=' + str(path),
                     '-p:MergeWildlifeBoundaryPath=' + str(wildlife_path)]
        return original_run(args, *rest, **kwargs)

    module.run = run
    tests = ['test_modcomponent_data_replacement_and_real_consumer',
             'test_production_maintenance_preflight_and_rifle_baseline',
             'test_production_shell_quality_settings',
             'test_startup_resources_reject_wrong_fir_address',
             'test_production_aim_audio_and_successful_shot_observer']
    result = unittest.TextTestRunner(verbosity=2).run(unittest.TestSuite(module.Pipeline(name) for name in tests))
    if not result.wasSuccessful():
        raise SystemExit(1)


if __name__ == '__main__':
    main()
