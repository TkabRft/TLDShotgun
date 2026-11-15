# Ammunition reference and balance targets

Baseline checked: 2026-10-31. Shotgun values below come from current source and item definitions. Vanilla references come from the installed game's serialized assets, read on the same date. They describe the standard rifle and revolver; named variants require separate comparisons.

Status: **implemented**, with game acceptance pending. The generated package includes the three projectile-material prefabs, scrapshot single/box prefabs, inventory/HUD icons, current recipes and 40 g ammunition units. The Unity-free pipeline reopens the package, verifies references/catalog/CRC and compiles a DLL containing those exact bytes. The checked-in older package remains an immutable template, not the full build output. See the [source handoff](../unity/SourceArt/README.md).

The initial generated package failed game registration because of case-sensitive JSON lookup and retained old definitions/recipes. The corrected builder replaces complete data sections and validates all 18 gear definitions through ModComponent lookup semantics, including an isolated test of the real JsonHandler source. Nine current default blueprints remain. The runtime now streams the package from the DLL; see [embedded asset loading](ASSET-LOADING.md). Game acceptance of the correction is pending.

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

Shotgun boxes contain six ammunition units, giving a 0.240 kg total without additional packaging mass. `WeightKG` is the weight of the full default item: singles use `0.040` with `UnitsPerItem: 1`; boxes use `0.240` with `UnitsPerItem: 6`. Native stacks divide the base weight by the default unit count before multiplying by the current quantity. Both forms therefore weigh 0.040 kg per round. The former box value of `0.040` produced the reported 0.09 kg for fourteen rounds; the corrected total is 0.56 kg. Source and package validators now check this ratio instead of requiring the same raw field value for singles and boxes.

Weapon-only references: standard rifle 4.00 kg, standard revolver 1.50 kg, current shotgun 3.00 kg. The shotgun's weapon weight has not been rebalanced here.

## Quality labels and registration

Singles and boxes share the English base names Slug Ammunition, Buckshot Ammunition, Birdshot Ammunition and Scrapshot Ammunition under their existing localization keys. The generator adds six full-name localization variants for each key, with `_Perfect`, `_Great`, `_Good`, `_Mediocre`, `_Poor` and `_Ruined` suffixes. Existing translations are preserved; new entries use the existing English fallback. The earlier implementation only forwarded to a native formatter without validating its actual displayed strings and did not establish working quality labels.

After configuring each prefab, `ShotgunAmmoPresentation.Validate` checks its gear ID, runtime gear-data identity, localization key, ammunition condition category and custom gun type. A mismatch fails ammunition configuration rather than being treated as a text-only issue. The current embedded definitions resolve as `ModAmmoComponent`, and the resolved ModComponent 7.0 mapper assigns `ConditionTableType.Ammunition`; no item-ID migration is needed.

[ShotgunAmmoPresentation](../Main/ShotgunAmmoPresentation.cs) patches only recognized shotgun ammunition in `GearItem.GetDisplayNameForInventoryInterfaces`, `GetTitleToDisplayWhenSelected` and the `DisplayNameWithCondition` getter. It reads the native ammunition condition ranges and resolves the full localized variant directly. The installed `SCRIPT_ConditionTable.asset` ammunition entry uses Perfect at 100%, Great at 80%, Good at 60%, Mediocre at 40%, Poor at 20% and Ruined at 0%. The ranges are stored in percent; the helper converts normalized condition to percent. It honors explicit localization overrides when their quality variant exists, otherwise retaining the original result. Names are rebuilt without cached or duplicate adjectives. Inventory naming is independent of the crafting/world presentation below. Vanilla ammunition, casings and materials keep their existing naming. Existing gear/save identifiers and quantities are preserved.

### Crafting and loose-world presentation

Status: **implemented**, with native UI acceptance pending. Presentation source checked: 2026-11-14.

[ShotgunCraftingPresentation](../Main/ShotgunCraftingPresentation.cs) supplies localized Slug Shell, Buckshot Shell, Birdshot Shell and Scrapshot Shell names to blueprint rows and selected-recipe headings. Default and optional station recipes share the output-based presentation; their internal names and registration identities are unchanged. The four additive `GAMEPLAY_DoubleBarrel_<load>ShellName` keys leave all existing ammunition and quality keys intact.

World hover text and the pickup-inspection title use the shell name only for recognized ammunition outside player inventory with exactly one native stack unit. This also covers a one-round item represented by a Box prefab after dropping/splitting. Multi-round boxes retain their original ammunition names. Inventory selection and inventory examination retain quality names such as Perfect Birdshot Ammunition, including at quantity one. Explicit name overrides and missing localization retain native text. Hover markup/status text and native heading capitalization are preserved. No gear data, condition, quantity, localization override or save field is mutated.

Blueprint icons use the existing weapon HUD silhouettes, rotated toward the upper right; casing artwork uses the empty-shell HUD silhouette. Projectile materials use monochrome derivatives of their existing inventory images. These textures have their own `ico_CraftItem__...` Texture2D addresses, matching ModComponent's recipe lookup. Requirement rows replace only recognized shotgun casing/projectile artwork within shotgun ammunition/material/casing recipes, leaving native tinting, quantities and unrelated ingredients intact. The selected shell image uses a separate colored inventory-image derivative angled 40 degrees clockwise. Original inventory/HUD art, 3D inspection settings, models and shell colors remain unchanged.

The [presentation generator](../unity/SourceArt/prepare_crafting_presentation.py) owns twelve separate textures under `Icons/Crafting/`, their metadata, additive source Addressables entries and manifest registrations. White RGB values are retained even in transparent silhouette texels to prevent dark BC3 fringes. Its `--check` mode verifies deterministic source pixels, transparent margins, monochrome values, metadata/addresses and localization without regenerating files. The ordinary ammunition validator includes these checks. The [focused harness](../Tests/check_crafting_presentation.py) reuses the workspace inspector project with mod-owned fixture sources, executes production UI postfixes and the existing quality helper, and checks actual cooked textures/catalog addresses and packaged localization. Native loading, GPU rendering and widget refresh order remain game checks.

Validation on 2026-11-14 passed 129 crafting/world checks and all 462 original inventory-quality checks using the packaged localization, plus cooked BC3/alpha and typed-address checks for all twelve new textures. The full package/runtime build and reference/Harmony-target audit passed. In game, compare default and optional recipe rows/headings/previews with vanilla; check ingredient tinting with available and missing materials; switch between shell and vanilla recipes; then find/drop/pick up one round and a partial/full box, inspect from the world and inventory, and repeat after save/reload. These checks remain pending; no game was launched or mod installed during validation.

## Gunsmithing crafting condition and experience

All four shell loads share five independent integer condition settings. Gunsmithing levels 1–5 default to **40 / 60 / 80 / 100 / 100%**. The Mod Options section **Crafted Shell Condition** starts collapsed; its Collapsed/Expanded dropdown controls visibility of the five 0–100% sliders. Loaded and confirmed values are clamped without sorting the progression. Invalid loaded values are persisted after correction so Cancel cannot restore them. Existing settings retain their values; missing fields receive these defaults.

[ShotgunAmmoQuality](../Main/ShotgunAmmoQuality.cs) scopes native `CraftingOperation.ApplyCraftingProgress` and `HandleSuccess` callbacks to the recognized blueprint output. Prefixes on both `PlayerManager.InstantiateItemInPlayerInventory` overloads set the normalized condition before native creation and stack merging, using the current zero-based Gunsmithing tier. Both scopes restore their previous state through finalizers, including exceptions and nested unrelated crafting. Conditions are read for each new output, including resumed work. Rifle and optional Shotgun skill levels do not affect this policy. Found/saved ammunition and in-progress placeholders outside these scopes are not reconditioned.

Default and optional shell recipes retain `AppliedSkill: Gunsmithing` and `ImprovedSkill: Gunsmithing`. The quality patches leave original crafting and XP handling running and add no duplicate award. Recipe regression checks reject losing the Gunsmithing XP declaration. Documented vanilla behavior awards no Gunsmithing XP for ammunition harvesting, so no shell-harvesting XP hook is added; existing recovery yields are retained. See the [vanilla skill reference](https://thelongdark.fandom.com/wiki/Skills). This reference is separate from native serialized-asset evidence and is not a claim that the game was run during validation.

## Projectile and range comparison

All damage totals below exclude animal/body-part multipliers, skill effects, and critical kills. Pellet totals require every pellet to hit. A range value is the base threshold before damage falloff, not a maximum raycast distance or a guaranteed practical hunting distance.

| Load | Status | Projectiles | Base damage per projectile | Full-hit base total | Range before falloff | Added pellet cone, full angle |
|---|---|---:|---:|---:|---:|---|
| Vanilla rifle | Reference | 1 | 100 | 100 | 75 m | Not applicable |
| Vanilla revolver | Reference | 1 | 100 | 100 | 30 m | Not applicable |
| Slug | Implemented | 1 | 150 | 150 | 30 m | None added by the mod |
| Buckshot | Implemented | 9 | 15 | 135 | 25 m | 2 degrees |
| Birdshot | Implemented | 30 | 3 | 90 | 20 m | 4 degrees |
| Scrapshot | Implemented | 6 | 13 | 78 | 25 m | **2.2 degrees** |

The rifle and revolver both declare 100 base damage, but their animal damage is not equivalent. Vanilla animals have separate revolver-specific damage scales and critical/bleed data. Shotgun pellets request the generic scale with `WeaponSource.Unspecified`; their critical calculation references rifle critical chance. Do not compare raw totals as though every weapon shares every body-part multiplier.

[Wildlife damage research](WILDLIFE-DAMAGE-RESEARCH.md) records the inspected generic/revolver scales, preserved pre-rebalance tables and current per-animal damage tables. `ShotgunWildlifePolicy` owns the implemented policy for standalone Shotgun and the updated optional Arsenal adapter. After regional scaling and each mode's own distance/minimum calculation, pellet hits kill rabbits/ptarmigans through ordinary damage; buckshot/scrapshot moose damage is multiplied by 0.4; birdshot deer/wolf head and other-region damage is multiplied by 0.225 and 0.1 respectively, and bear/moose/cougar damage by 0.02. At defaults this gives 6.75 birdshot head damage on deer/wolves. Unknown types and special story/challenge actors retain their previous handling.

| Load | Damage lost per metre beyond range | Minimum damage on an out-of-range hit |
|---|---:|---:|
| Vanilla rifle bullet prefab | 0.5 | 1 |
| Vanilla revolver bullet prefab | 0.5 | 1 |
| Slug | 5 | 20 |
| Buckshot | 1 per pellet | 5 per pellet |
| Birdshot | 0.2 per pellet | 1 per pellet |
| Scrapshot | 1 per pellet | 5 per pellet |

Vanilla falloff entries are serialized bullet-prefab defaults, not an in-game measurement across skills or variants. Standalone pellets apply body-part scaling, subtract distance falloff and clamp the out-of-range minimum, then apply wildlife policy and sum hits per animal. Policy reductions therefore cannot be undone by the minimum. Arsenal applies its distance curve and generic region scale before that same policy. Missed pellets contribute nothing. Slugs retain native single-projectile resolution with scoped generic-scale and half-timer hooks; endpoint queries have an exception-safe recursion guard.

## Bleeding, criticals, and both barrels

| Load | Bleeding eligibility | Critical-roll eligibility |
|---|---|---|
| Vanilla rifle / revolver | Game handling and animal/body-part data | Game handling, weapon skill, and animal/body-part data |
| Slug | One hit; half the positive native generic timer | Single-projectile handling with the captured shotgun critical adjustment |
| Buckshot | At least 3 pellets hit one animal in one shell | At least 6 in-range pellets hit that animal |
| Birdshot | Deer/wolves only: at least 15 hits and one 15% roll; timer max(240 minutes, native timer × 4) | Disabled; legacy setting key retained and hidden |
| Scrapshot | At least 3 hits; native timer × 1.5 | All 6 pellets hit that animal within range |

Bleeding eligibility does not override an animal's inability to bleed. Pellet bleeding uses the shortest positive eligible timer among hit regions; it does not stack one bleed timer per pellet. Eligible pellet hits give one critical roll per animal per shell, using the region with the highest individual pellet damage. Targets that ignore critical hits retain that protection.

Current shotgun critical chance is the region's rifle critical chance multiplied by `0.3125 x GetCurrentTier()`. The selected tier comes from the separate Shotgun skill when available, otherwise Rifle when the default-on fallback option is enabled. Disabling that option without a separate skill retains fixed internal tier 3, giving 0.9375 times the region baseline. Tiers remain zero-based; see [skills and condition](SKILLS-AND-CONDITION.md).

For buckshot/scrapshot only, the critical chance above is multiplied by 0.1 on moose and 0.25 on cougars. Small-game pellet kills use ordinary damage, without critical or bleeding rolls. Birdshot's bleeding trial is consumed once per animal per shell; later streaming batches cannot retry a failure or lengthen a successful timer. The new hidden settings revision forces all existing slug-damage values to 150 once, saves/reloads the confirmed settings snapshot and preserves later customization. Other existing setting keys and values are retained.

Both-barrel fire resolves two separate shots and consumes two shells. Full-hit base totals are 300 for slugs, 270 for buckshot, 180 for birdshot, and 156 for scrapshot. Wildlife policy must still be applied to those totals. Bleeding and critical thresholds are checked separately per shell; pellets from the two barrels do not combine to meet one shell's threshold.

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

Assembly requires light, uses Gunsmithing, and requires no additional tool or lit fire. Scrap preparation and six shell assemblies total 60 base minutes. The existing casing recipe remains 1 scrap metal -> 6 casings at a lit forge, 30 base minutes, with no required tool. Disassembling single shells or boxes recovers the matching projectile material, casing, and 10 g gunpowder per round; buckshot/birdshot do not yield vanilla Bullets, and scrapshot cannot create lead.

`ShotgunAmmoHarvest.Configure` assigns one `POWDER_Gunpowder` entry and `ItemWeight.FromKilograms(0.01f)` to the native `Harvest` component of each single/box prefab during ammunition configuration. ModComponent's gear-only mapper remains unchanged. These are per-unit yields: do not multiply the box field by six. Reconfiguration replaces the arrays instead of appending. Empty casings are untouched. Existing gear/save identifiers are preserved; no save migration is introduced. Verify native stack multiplication on partial and full boxes in game.

## Ammunition colors

Hull colors and prominent box accents match: red buckshot, dark blue slug, green birdshot, grey scrapshot. Manufactured box artwork and shell geometry/UVs remain; the improvised scrapshot carton retains its six visible shells and folded construction with a grey finish. Inventory icons are rendered from the matching assets. White HUD silhouettes are unchanged. These changes cover pickup/inventory prefabs; first-person weapon animation meshes retain their existing materials.

The Unity-free package replaces the six existing manufactured-ammunition prefab/icon container entries and their catalog targets while preserving their public addresses. The immutable template and its original non-container objects remain unchanged. New source material variants also support the Unity authoring path.

Three ModSettings options default to Off and take effect on the next game load:

- Allow vanilla Bullets at ordinary forges: adds an alternative to vanilla casting with the same ingredients/yield/time/fire requirements.
- Allow lead shot at ordinary forges: adds alternatives for both new lead materials.
- Allow lead shotgun shells at workbenches: adds alternatives for slugs, buckshot, and birdshot, leaving vanilla rifle/revolver assembly unchanged.

Defaults remain available alongside alternatives. Scrap preparation/assembly remain workbenchable regardless of options. `ShotgunCrafting` removes only its previous alternative JSON from ModComponent's managed registry before registering the enabled recipes for the next game load.

Alternative registration requires successful embedded-package registration. If assets
are unavailable, previous owned alternatives are removed and no replacements are
added; one warning is emitted per process. Other mods' recipes remain untouched.
ModComponent 7.x minor versions are accepted when their required loader interfaces
match. The 2026-11-15 regression covered all eight option combinations and repeated
loads across 7.0/7.1/7.2/7.99 fixtures, with actual 7.0.0/7.1.0 API binding;
see [the compatibility regression](ASSET-LOADING.md#modcomponent-7x-compatibility-regression)
for evidence and native acceptance limits. Recipe ingredients, yields, identities,
settings and existing saves are unchanged.

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

Mod sources: [settings](../Settings/Settings.cs), [ammo parameter selection](../Main/Actions.cs), [pellet resolution](../Main/ShotgunPellets.cs), [skill fallback](../Main/ShotgunSkillProgression.cs), [casing ejection](../Main/AnimationEventProxy.cs), [slug recipe](../unity/Assets/_ModComponent/shotgun/blueprints/ShotgunAmmoSingleSlug.json), [buckshot recipe](../unity/Assets/_ModComponent/shotgun/blueprints/ShotgunAmmoSingleBuckshot.json), [birdshot recipe](../unity/Assets/_ModComponent/shotgun/blueprints/ShotgunAmmoSingleBirdshot.json), and [casing recipe](../unity/Assets/_ModComponent/shotgun/blueprints/ShotgunAmmoCasing.json). Current weight definitions are the `DoubleBarrelAmmoSingle*`, `DoubleBarrelAmmoBox*`, and `DoubleBarrelAmmoCasing` JSON files under `unity/Assets/_ModComponent/shotgun/auto-mapped/`.

Vanilla provenance: `Assets/Prefabs/Gear/Ammo/GearItemData_RifleAmmoSingle.asset` and `GearItemData_RevolverAmmoSingle.asset` for 20 g and 10 g unit weights; their casing data assets for empty weights; standard weapon data/prefabs under `Assets/Prefabs/Gear/Weapons/`; `PistolBullet.prefab` and `RevolverBullet.prefab` under `Assets/AssetStore/UltimateFPSCamera/Prefabs/`; and `BLUEPRINT_GEAR_Bullet_A`, `BLUEPRINT_GEAR_RifleAmmoSingle_A`, and `BLUEPRINT_GEAR_RevolverAmmoSingle_A` under `Assets/ScriptableObjects/Gear/Blueprints/`. These are serialized asset observations, not implementation bodies inferred from IL2CPP reference stubs. The project's reference-package pin is not proof of the installed game's version.

The full asset/package/runtime build passes structural checks without Unity Editor. Weight regressions check all four loads in single/box forms at quantities 1, 6, 14 and 100, and reject the former box definition. The real ModComponent JSON-consumer regression passes. The linked production presentation helper passes 462 checks using actual packaged English localization entries and a fixture of the inspected native condition ranges, including exact names at 0/19/20/39/40/59/60/79/80/99/100%. The production crafting helper passes 353 managed checks for all tiers, both instantiation overloads, pre-merge conditions, nested/failed scopes and current settings/skill reads. The production Settings harness passes 47 checks including defaults, dropdown visibility, Cancel, persistence and bounds. The native Unity crafting/merging, XP implementation and rendered UI are not executed by these harnesses.

Color updates previously passed all 22 FBX/GLB round trips, packed-texture/geometry checks, deterministic package and resolved-material checks. The production maintenance/harvest harness previously passed 68 checks with native objects stubbed. Before distribution, check exact rendered names; crafting at all five Gunsmithing levels; default/custom sliders and Cancel; interrupted/resumed work; optional workbench recipes; crafting XP and no harvesting XP; and mixed-quality stacking before/after save/reload. Also check inventory/loaded-shell/stack weights on new and existing saves, split/drop/pickup behavior, material consumption/recovery, icons, firing/reloading and saved ammo. Specifically harvest one round, a partially filled box, and a full six-round box of each load and confirm 10/30/60 g powder for one/three/six rounds, plus the existing casing/projectile yields. Named rifle/revolver variants, skill bonuses, and optional combat integrations still need focused in-game comparisons; see [TODO.md](TODO.md).

code-synced: 2026-11-08
