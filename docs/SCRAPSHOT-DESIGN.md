# Scrapshot implementation and balance

Status: **implemented**, with game acceptance pending. Runtime code, recipes, projectile portions, scrapshot single/box prefabs and inventory/HUD icons are included in the rebuilt DLL. The Unity-free pipeline verifies package contents, object references, catalog and CRC before embedding. Follow the [source handoff](../unity/SourceArt/README.md).

## Role

Scrapshot is an improvised alternative to lead buckshot. Its advantage is common scrap metal and ordinary-workbench preparation/assembly. Lead buckshot retains greater damage and pattern density. There is one scrapshot load; no separate small-game scrap load, wad item, or cloth requirement.

## Source defaults

| Parameter | Lead buckshot | Scrapshot |
|---|---:|---:|
| Pellets per shell | 9 | 6 |
| Base damage per pellet | 15 | 13 |
| Full-hit base damage | 135 | 78 |
| Full spread angle | 2 degrees | 2.2 degrees |
| Range before falloff | 25 m | 25 m |
| Falloff per metre beyond range | 1 per pellet | 1 per pellet |
| Minimum out-of-range damage | 5 per pellet | 5 per pellet |
| Hits on one animal required to bleed | 3 | 3 |
| In-range hits required for one critical roll | 6 | 6 |
| Gunpowder per shell | 10 g | 10 g |
| Finished shell weight | 40 g | 40 g |

The 2.2-degree spread is the full cone, a 1.1-degree maximum deviation under the existing spread convention. Damage totals exclude body-part scales, skills, misses, and criticals. One material portion supplies a shell independently of the configurable pellet count.

## Two-stage crafting

1. **Prepare scrap buckshot:** 1 scrap metal produces 6 portions at an ordinary workbench with simple tools, in 30 base minutes. Each portion weighs 20 g, has no passive decay, and belongs to Materials.
2. **Assemble a scrapshot shell:** 1 scrap buckshot portion + 1 reusable shotgun casing + 10 g gunpowder produces 1 shell at an ordinary workbench, in 5 base minutes, without an additional tool.

Both stages require light and use Gunsmithing progression. Preparing and assembling six shells totals 60 base minutes. The ammunition workbench is not mandatory at either scrapshot stage. The three optional lead-crafting location settings do not gate scrapshot.

Disassembly recovers a casing and matching scrap buckshot portion per shell using existing recovery behavior, with no new powder-recovery mechanism. It never produces vanilla Bullets or lead. The earlier direct scrap-metal-to-six-shell recipe is superseded.

## Balance and integration

- Scrapshot deals approximately 13% less damage per pellet with one-third fewer pellets: about 58% of lead buckshot's full-hit base damage.
- Three hits require half its pattern to connect for bleeding eligibility. All six must connect in range for one critical roll. Thresholds apply independently to each animal and shell; animals that cannot bleed or ignore criticals retain those protections.
- Use the existing shotgun skill/critical calculation and ordinary ammo-quality behavior. No additional misfire, weapon-wear, or range penalty is added.
- At damage scale 1, full patterns deal 78 at 25 m, 48 at 30 m, and 30 at 35 m. These are calculations, not measured game results.
- Both barrels consume two shells and resolve separate thresholds, for up to 156 base damage. Mixed-barrel loading is separate TODO work.
- Scrapshot is crafting-only initially. It appends to the existing load order as enum value 4, with distinct single-shell, six-shell-box, and HUD addresses. Existing load IDs/indices remain intact.
- Unmarked grey reused-shell artwork, an unprinted crumpled cardboard carton open at one end with six shells visible inside, inventory/HUD icons, three projectile-pile models, and editable Blender source are supplied. The artwork passed FBX/GLB reverse-import and standalone Blender-file checks; see the [asset handoff and previews](../unity/SourceArt/README.md). The Unity-free builder creates/registers these prefabs and icons directly in a generated package while preserving the original content.

## Pending acceptance

Verify crafting, material consumption, disassembly, quality/progression, stacks, loaded weights, inventory/radial/HUD names, reload/swapping, both barrels, casing recovery, and save/load after building. Compare close-range wolf defense and partial patterns against lead buckshot; test large animals and small game before adding penalties. Combat-mod compatibility remains unverified.

Sources: [ammo catalog](../Main/ShotgunAmmoCatalog.cs), [pellet resolution](../Main/ShotgunPellets.cs), [settings](../Settings/Settings.cs), [shared ammunition reference](AMMUNITION.md), and [authoring definitions](../unity/SourceArt/ammunition-definitions.json).
