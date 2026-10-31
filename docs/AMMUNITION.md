# Ammunition reference and balance targets

Baseline checked: 2026-10-31. Shotgun values below come from current source and item definitions. Vanilla references come from the installed game's serialized assets, read on the same date. They describe the standard rifle and revolver; named variants require separate comparisons.

Status: **partial** - ammunition runtime source, recipe definitions, 40 g source weights, and Blender artwork are implemented. Unity prefab authoring/import, asset-package rebuilding and game acceptance remain pending. Runtime C# compilation passed on 2026-10-31 using the current embedded package; that compilation artifact does not contain the new assets. The embedded package still contains the previous assets. Follow the [source handoff](../unity/SourceArt/README.md) before producing a playable build.

## Weight rule

**Every finished shotgun shell must weigh twice one vanilla rifle round: 2 x 0.020 kg = 0.040 kg (40 g).** Apply this to slugs, buckshot, birdshot, and scrapshot, regardless of crafting route. Empty casings are separate items and retain their current weight unless explicitly rebalanced later.

| Ammunition | Source unit weight | Selected unit-weight target | Target weight for 6 rounds | Target weight for 100 rounds |
|---|---:|---:|---:|---:|
| Vanilla rifle round, reference | 0.020 kg | 0.020 kg, unchanged | 0.120 kg | 2.00 kg |
| Vanilla revolver round, reference | 0.010 kg | 0.010 kg, unchanged | 0.060 kg | 1.00 kg |
| Shotgun slug shell | 0.040 kg | **0.040 kg** | **0.240 kg** | **4.00 kg** |
| Shotgun buckshot shell | 0.040 kg | **0.040 kg** | **0.240 kg** | **4.00 kg** |
| Shotgun birdshot shell | 0.040 kg | **0.040 kg** | **0.240 kg** | **4.00 kg** |
| Scrapshot shell | 0.040 kg | **0.040 kg** | **0.240 kg** | **4.00 kg** |

Current empty-casing weights: shotgun 0.010 kg; vanilla rifle 0.010 kg; vanilla revolver 0.005 kg. The finished shell's weight includes its casing; do not add another casing weight when calculating carried live ammunition.

Shotgun boxes contain six ammunition units. The selected box total is therefore 0.240 kg, with no additional packaging mass proposed. Confirm inventory stack and loaded-ammunition accounting when implementing the target; do not assume a box field represents the entire stack's mass.

Weapon-only references: standard rifle 4.00 kg, standard revolver 1.50 kg, current shotgun 3.00 kg. The shotgun's weapon weight has not been rebalanced here.

## Projectile and range comparison

All damage totals below exclude animal/body-part multipliers, skill effects, and critical kills. Pellet totals require every pellet to hit. A range value is the base threshold before damage falloff, not a maximum raycast distance or a guaranteed practical hunting distance.

| Load | Status | Projectiles | Base damage per projectile | Full-hit base total | Range before falloff | Added pellet cone, full angle |
|---|---|---:|---:|---:|---:|---|
| Vanilla rifle | Reference | 1 | 100 | 100 | 75 m | Not applicable |
| Vanilla revolver | Reference | 1 | 100 | 100 | 30 m | Not applicable |
| Slug | Implemented | 1 | 120 | 120 | 30 m | None added by the mod |
| Buckshot | Implemented | 9 | 15 | 135 | 25 m | 2 degrees |
| Birdshot | Implemented | 30 | 3 | 90 | 20 m | 4 degrees |
| Scrapshot | Source; unbuilt | 6 | 13 | 78 | 25 m | **2.2 degrees** |

The rifle and revolver both declare 100 base damage, but their animal damage is not equivalent. Vanilla animals have separate revolver-specific damage scales and critical/bleed data. Shotgun pellets request the generic scale with `WeaponSource.Unspecified`; their critical calculation references rifle critical chance. Do not compare raw totals as though every weapon shares every body-part multiplier.

| Load | Damage lost per metre beyond range | Minimum damage on an out-of-range hit |
|---|---:|---:|
| Vanilla rifle bullet prefab | 0.5 | 1 |
| Vanilla revolver bullet prefab | 0.5 | 1 |
| Slug | 5 | 20 |
| Buckshot | 1 per pellet | 5 per pellet |
| Birdshot | 0.2 per pellet | 1 per pellet |
| Scrapshot | 1 per pellet | 5 per pellet |

Vanilla falloff entries are serialized bullet-prefab defaults, not an in-game measurement across skills or variants. For implemented pellet loads, the mod applies body-part scaling first, subtracts distance falloff, clamps out-of-range damage to the per-pellet minimum, and sums hits per animal. Missed pellets contribute nothing. Slugs retain the game's single-projectile resolution path with the mod's falloff settings.

## Bleeding, criticals, and both barrels

| Load | Bleeding eligibility | Critical-roll eligibility |
|---|---|---|
| Vanilla rifle / revolver | Game handling and animal/body-part data | Game handling, weapon skill, and animal/body-part data |
| Slug | Single-projectile game handling | Single-projectile handling with the mod's shotgun critical adjustment |
| Buckshot | At least 3 pellets hit one animal in one shell | At least 6 in-range pellets hit that animal |
| Birdshot | At least 15 pellets hit one animal in one shell | Disabled by default |
| Scrapshot | At least 3 pellets hit one animal in one shell | All 6 pellets hit that animal within range |

Bleeding eligibility does not override an animal's inability to bleed. Pellet bleeding uses the shortest positive eligible timer among hit regions; it does not stack one bleed timer per pellet. Eligible pellet hits give one critical roll per animal per shell, using the region with the highest individual pellet damage. Targets that ignore critical hits retain that protection.

Current shotgun critical chance is the region's rifle critical chance multiplied by `0.3125 x GetCurrentTier()`. Without the optional skill integration, the fallback tier is 3, giving 0.9375 times that rifle baseline. Do not assume a different tier-numbering convention when changing the integration.

Both-barrel fire resolves two separate shots and consumes two shells. Full-hit base totals are 240 for slugs, 270 for buckshot, 180 for birdshot, and 156 for scrapshot. Bleeding and critical thresholds are checked separately per shell; pellets from the two barrels do not combine to meet one shell's threshold.

## Crafting pipeline

Each new stackable Material item is a **20 g portion for one shell**. Portions have no passive decay and cannot be loaded directly. Their quantity is independent of combat pellet-count settings. Vanilla Bullet remains unchanged and supplies slug shells.

| Projectile preparation | Result | Default station / tools | Base time |
|---|---|---|---:|
| 1 scrap lead | 6 vanilla Bullets | Lit ammunition forge | 60 min |
| 1 scrap lead | 6 lead buckshot portions | Lit ammunition forge | 60 min |
| 1 scrap lead | 12 lead birdshot portions | Lit ammunition forge | 60 min |
| 1 scrap metal | 6 scrap buckshot portions | Ordinary workbench, simple tools | 30 min |

Every cartridge below consumes one matching projectile item, one matching casing, and **10 g gunpowder**, produces one round, and takes five base minutes:

| Load | Projectile item | Casing | Default assembly station |
|---|---|---|---|
| Vanilla rifle | Vanilla Bullet | Rifle casing | Ammunition workbench, unchanged |
| Vanilla revolver | Vanilla Bullet | Revolver casing | Ammunition workbench, unchanged |
| Slug | Vanilla Bullet | Shotgun casing | Ammunition workbench |
| Buckshot | Lead buckshot portion | Shotgun casing | Ammunition workbench |
| Birdshot | Lead birdshot portion | Shotgun casing | Ammunition workbench |
| Scrapshot | Scrap buckshot portion | Shotgun casing | Ordinary workbench |

Assembly requires light, uses Gunsmithing, and requires no additional tool or lit fire. Scrap preparation and six shell assemblies total 60 base minutes. The existing casing recipe remains 1 scrap metal -> 6 casings at a lit forge, 30 base minutes, with no required tool. Disassembling single shells or boxes recovers the matching projectile material and casing per shell using existing recovery behavior; buckshot/birdshot do not yield vanilla Bullets, and scrapshot cannot create lead. No powder-recovery mechanism was added.

Three ModSettings options default to Off and take effect on the next game load:

- Allow vanilla Bullets at ordinary forges: adds an alternative to vanilla casting with the same ingredients/yield/time/fire requirements.
- Allow lead shot at ordinary forges: adds alternatives for both new lead materials.
- Allow lead shotgun shells at workbenches: adds alternatives for slugs, buckshot, and birdshot, leaving vanilla rifle/revolver assembly unchanged.

Defaults remain available alongside alternatives. Scrap preparation/assembly remain workbenchable regardless of options. `ShotgunCrafting` removes only its previous alternative JSON from ModComponent's managed registry before registering the enabled recipes for the next game load.

Material IDs are `GEAR_DoubleBarrelLeadBuckshot`, `GEAR_DoubleBarrelLeadBirdshot`, and `GEAR_DoubleBarrelScrapBuckshot`. Scrapshot appends to the existing ammo order as enum value 4; existing values and ammo IDs are preserved. No wad item or separate small-game scrap load is included.

Spent shotgun casings are reusable. Firing increments the spent-casing count; reload ejection places casings at the player's feet rather than directly into inventory. Reload can eject spent casings even without spare ammunition. Picked-up casings can be used for any compatible shotgun-shell recipe.

## Availability and balance roles

- Current ammunition appears through fixed percentage-chance placements and container loot in [gearspawns.txt](../unity/Assets/_ModComponent/shotgun/gear-spawns/gearspawns.txt), plus resource-level selections in [groupspawns.txt](../Spawns/groupspawns.txt).
- All ammunition groups select zero entries at Low base resource availability. This does not by itself remove the separate fixed-placement and container-loot sources.
- Shotgun ammunition and casings respect the custom shotgun ban and general firearm ban configured by [ShotgunAvailability.cs](../Main/ShotgunAvailability.cs) and [Configuration.cs](../Main/Configuration.cs).
- Scrapshot is crafting-only. Ordinary-workbench preparation and assembly are its accessibility advantage. See [restoration](RESTORATION.md) for complete-shotgun find controls.
- Lead slugs provide concentrated damage; lead buckshot provides the strongest full close-range pattern; birdshot provides dense small-game coverage. Scrapshot delivers about 58% of lead buckshot's full-hit base damage, with fewer slightly weaker pellets and a slightly wider cone.
- Initially give scrapshot no extra misfire or weapon-wear penalty and no further range reduction. Test the pellet-count/damage tradeoff before adding penalties.
- The 40 g shell target adds a carrying cost to every shotgun load: 100 shells weigh 4 kg, versus 2 kg for 100 rifle rounds. Weapon weight, difficulty, skill, partial patterns, and critical/bleed behavior still require practical balance testing.

## Evidence and remaining checks

Mod sources: [settings](../Settings/Settings.cs), [ammo parameter selection](../Main/Actions.cs), [pellet resolution](../Main/ShotgunPellets.cs), [skill fallback](../Main/ShotgunSkill.cs), [casing ejection](../Main/AnimationEventProxy.cs), [slug recipe](../unity/Assets/_ModComponent/shotgun/blueprints/ShotgunAmmoSingleSlug.json), [buckshot recipe](../unity/Assets/_ModComponent/shotgun/blueprints/ShotgunAmmoSingleBuckshot.json), [birdshot recipe](../unity/Assets/_ModComponent/shotgun/blueprints/ShotgunAmmoSingleBirdshot.json), and [casing recipe](../unity/Assets/_ModComponent/shotgun/blueprints/ShotgunAmmoCasing.json). Current weight definitions are the `DoubleBarrelAmmoSingle*`, `DoubleBarrelAmmoBox*`, and `DoubleBarrelAmmoCasing` JSON files under `unity/Assets/_ModComponent/shotgun/auto-mapped/`.

Vanilla provenance: `Assets/Prefabs/Gear/Ammo/GearItemData_RifleAmmoSingle.asset` and `GearItemData_RevolverAmmoSingle.asset` for 20 g and 10 g unit weights; their casing data assets for empty weights; standard weapon data/prefabs under `Assets/Prefabs/Gear/Weapons/`; `PistolBullet.prefab` and `RevolverBullet.prefab` under `Assets/AssetStore/UltimateFPSCamera/Prefabs/`; and `BLUEPRINT_GEAR_Bullet_A`, `BLUEPRINT_GEAR_RifleAmmoSingle_A`, and `BLUEPRINT_GEAR_RevolverAmmoSingle_A` under `Assets/ScriptableObjects/Gear/Blueprints/`. These are serialized asset observations, not implementation bodies inferred from IL2CPP reference stubs. The project's reference-package pin is not proof of the installed game's version.

Before shipping, run the deferred Unity authoring/import and asset-package export, verify all new addresses and embedded output, compile the runtime mod, and check inventory/loaded-shell/stack weights on new and existing saves. Current packaging JSON and existing shell-prefab source weights have been updated; new prefabs are created by the supplied Unity authoring command. Named rifle/revolver variants, skill bonuses, and optional combat integrations still need focused in-game comparisons; see [TODO.md](TODO.md).
