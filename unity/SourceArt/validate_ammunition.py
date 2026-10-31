"""Non-compiling checks of source recipes, source assets, and pure spawn expressions."""
import json
import re
import struct
from pathlib import Path
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "unity/Assets/_ModComponent/shotgun"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def evaluate(expression, values):
    """Evaluate the actual small C# arithmetic/ternary source expressions, not a copy."""
    expression = expression.strip()
    while expression.startswith('(') and expression.endswith(')'):
        depth = 0
        for i, c in enumerate(expression):
            depth += (c == '(') - (c == ')')
            if depth == 0: break
        if i != len(expression)-1: break
        expression = expression[1:-1].strip()
    depth = 0
    question = None
    for i, c in enumerate(expression):
        depth += (c == '(') - (c == ')')
        if c == '?' and depth == 0 and question is None: question = i
        elif c == ':' and depth == 0 and question is not None:
            condition = evaluate(expression[:question], values)
            return evaluate(expression[question+1:i] if condition else expression[i+1:], values)
    if expression.startswith('Outcome.'): return expression.split('.')[-1]
    expression = expression.replace('&&', ' and ').replace('||', ' or ').replace('(int)', '')
    for i, name in enumerate(['Low', 'Medium', 'High', 'VeryHigh']):
        expression = expression.replace('ShotgunResourceLevel.'+name, str(i))
    return eval(' '.join(expression.split()), {"__builtins__": {}}, values)


def main():
    projectiles = {'Slug': 'GEAR_Bullet', 'Buckshot': 'GEAR_DoubleBarrelLeadBuckshot',
                   'Birdshot': 'GEAR_DoubleBarrelLeadBirdshot', 'Scrapshot': 'GEAR_DoubleBarrelScrapBuckshot'}
    for kind, projectile in projectiles.items():
        bp = read(DATA / 'blueprints' / ('ShotgunAmmoSingle'+kind+'.json'))
        assert bp['RequiredGear'] == [{'Item': 'GEAR_DoubleBarrelAmmoCasing', 'Count': 1, 'Units': 'Count'},
                                      {'Item': projectile, 'Count': 1, 'Units': 'Count'}], kind
        assert bp['RequiredPowder'] == [{'Powder': 'POWDER_Gunpowder', 'QuantityInKilograms': 0.01}]
        assert bp['RequiredCraftingLocation'] == ('Workbench' if kind == 'Scrapshot' else 'AmmoWorkbench')
        assert bp['RequiresLight'] and not bp['RequiresLitFire'] and not bp['RequiredTool']
        assert bp['DurationMinutes'] == 5 and bp['CraftedResultCount'] == 1
        for form, count in [('Single', 1), ('Box', 6)]:
            data = read(DATA / 'auto-mapped' / ('DoubleBarrelAmmo'+form+kind+'.json'))
            assert data['ModAmmoComponent']['WeightKG'] == 0.04
            assert data['ModStackableBehaviour']['UnitsPerItem'] == count
            h = data['ModHarvestableBehaviour']
            assert h['YieldNames'] == ['GEAR_DoubleBarrelAmmoCasing', projectile] and h['YieldCounts'] == [1, 1]
    for kind, count, scrap in [('LeadBuckshot', 6, False), ('LeadBirdshot', 12, False), ('ScrapBuckshot', 6, True)]:
        data = read(DATA / 'auto-mapped' / ('DoubleBarrel'+kind+'.json'))
        assert 'ModAmmoComponent' not in data
        assert data['ModGenericComponent']['InventoryCategory'] == 'Material'
        assert data['ModGenericComponent']['WeightKG'] == 0.02 and data['ModGenericComponent']['DaysToDecay'] == 0
        bp = read(DATA / 'blueprints' / ('DoubleBarrel'+kind+'.json'))
        assert bp['CraftedResultCount'] == count and bp['ImprovedSkill'] == 'Gunsmithing'
        assert bp['RequiredGear'][0]['Item'] == ('GEAR_ScrapMetal' if scrap else 'GEAR_ScrapLead')
        assert bp['RequiredCraftingLocation'] == ('Workbench' if scrap else 'AmmoWorkbench')
        assert bp['RequiresLitFire'] == (not scrap) and not bp['RequiredPowder']
        assert bp['DurationMinutes'] == (30 if scrap else 60)
    alternatives = list((ROOT/'Crafting/Alternatives').glob('*.json'))
    assert len(alternatives) == 6
    assert len({read(p)['Name'] for p in alternatives}) == 6
    vanilla = read(ROOT/'Crafting/Alternatives/VanillaBulletForge.json')
    assert vanilla['AppliedSkill'] == 'None' and not vanilla['RequiresLight'] and vanilla['RequiresLitFire']
    assert vanilla['CraftedResult'] == 'GEAR_Bullet' and vanilla['CraftedResultCount'] == 6 and vanilla['DurationMinutes'] == 60
    assert read(DATA/'auto-mapped/DoubleBarrelAmmoCasing.json')['ModGenericComponent']['WeightKG'] == 0.01
    assert read(DATA/'auto-mapped/DoubleBarrel.json')['ModGenericComponent']['WeightKG'] == 3.0
    manifest = read(Path(__file__).with_name('ammunition-definitions.json'))['Items']
    assert len(manifest) == 11 and len({x['name'] for x in manifest}) == 11
    for item in manifest:
        assert item['components'] == read(DATA/'auto-mapped'/(item['name'][5:]+'.json'))
        if item['model']:
            model = ROOT/'unity/Assets/Shotgun/Imports'/item['model']
            assert model.read_bytes().startswith(b'Kaydara FBX Binary') and Path(str(model)+'.meta').exists()
        if item['material'] or 'Scrapshot' in item['name']:
            icon = ROOT/'unity/Assets/Shotgun/Icons'/item['icon']
            png = icon.read_bytes()
            assert png[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', png[16:24]) == (256, 256)
            assert png[25] == 6 and Path(str(icon)+'.meta').exists()
    policy = (ROOT/'Main/ShotgunSpawnPolicy.cs').read_text(encoding='utf-8')
    roll = re.search(r'internal static Outcome Roll\([^;]+?=>\s*(.*?);', policy, re.S)[1]
    for usable, ruined in [(0,0),(100,0),(0,100),(50,50),(0,50),(25,50),(37,13)]:
        outcomes = [evaluate(roll, {'roll': i, 'usable': usable, 'ruined': ruined}) for i in range(100)]
        assert outcomes.count('Usable') == usable and outcomes.count('Ruined') == ruined
        assert outcomes.count('Empty') == 100-usable-ruined
    body = re.search(r'internal static int CandidateCount\(string level\)\s*\{(.*?)\n        \}', policy, re.S)[1]
    eligible = re.search(r'bool eligible\s*=\s*(.*?);', body, re.S)[1]
    result = re.search(r'return (.*?);', body, re.S)[1]
    cases = 0
    for rank in range(4):
        for usable_min in range(4):
            for ruined_min in range(4):
                for use in [False,True]:
                    for ruin in [False,True]:
                        settings = SimpleNamespace(SpawnUsableShotguns=use, SpawnRuinedShotguns=ruin,
                            UsableShotgunMinimumResources=usable_min, RuinedShotgunMinimumResources=ruined_min)
                        env = {'s': settings, 'rank': rank}
                        env['eligible'] = evaluate(eligible, env)
                        actual = evaluate(result, env)
                        expected = (2 if rank == 3 else 1) if (use and rank >= usable_min or ruin and rank >= ruined_min) else 0
                        assert actual == expected
                        cases += 1
    print(f'PASS: material/shell recipes, yields, weights, recovery, 6 alternatives, 11 item definitions, artwork/metadata, 700 outcome rolls, and {cases} spawn eligibility cases.')
    print('Source validation only: no compilation, Unity import/package build, or in-game acceptance.')


if __name__ == '__main__':
    main()
