# Shotgun distribution

code-synced: 2026-11-13

Status: implemented. Native pickup, physics, scene restoration and interactions
with other loot mods still require game acceptance.

## Placed finds

`SG_Shotgun` has a world-wide capacity of 0 / 2 / 3 / 4 selected locations at
Low / Medium / High / Very High resources. Availability settings gate selection.
Empty outcomes spend slots. Existing 50% usable / 50% ruined defaults and minimum
resource settings are unchanged: Medium is ruined-or-empty; High and Very High
are usable-or-ruined.

The original three gun locations remain. Eight additions cover Trapper's
Homestead (`SafeHouseA`), Hunting Lodge, both farmhouses, Quonset, Mountaineer's
Hut (`CrashMountainRegion`), Hibernia and the airfield hangar. Each base part pool
has three additional candidates; its counts and optional-region pools remain.
Existing IDs are preserved. The three farmhouse poses have corrected transforms
to clear native interior geometry; other placed finds retain their transforms.

`Spawns/placement-evidence.json` records the 20 added candidates and two farmhouse
position alternatives, source bundle and
prefab hashes, native support paths and numeric clearance checks. Only numbers
are retained; no game geometry is redistributed. The bounded audit checks nine
support points, active BoxColliders, existing grouped/fixed mod-loot collider
envelopes, overlap among new placements and 0.5 m
loot-origin separation. The farmhouse uses the stronger mesh audit described
below; other entries retain this bounded BoxCollider audit. Dynamic furniture,
physics and pickup remain game checks. Locations use measured clear support, including the
hut workbench and an upstairs Hibernia counter, rather than unverified leaning
poses. Machine-specific authoring commands live under workspace `Custom/`.

### Paradise Meadows farmhouse

`SG_Shotgun_007` in `FarmHouseB` remains one world-pool candidate. Its default
position is on clear wooden floor near the bedroom doorway, with two alternatives
on supported floor beside the bedroom bed and office desk. Each position has equal
chance after the farmhouse candidate is selected. Quotas, candidate weight,
usable/ruined/empty rolls and optional rifle replacements are unchanged.

The parser attaches named alternatives to the existing member rather than adding
lottery members. Position selection uses the separate `ShotgunPlacement:` seed
salt and ordinal alternative-name ordering. The resolved transform is cached in
the existing scene plan before instantiation, so retries use the same position.
The ledger continues to reserve and charge the original ID. Position alternatives
do not enter its roster fingerprint or require a save-format migration.

All three positions pass a version-2 audit of the full rendered shotgun envelope
and collider, nine-point floor support, active native MeshColliders/BoxColliders,
enabled furniture render meshes, structural mesh triangles and grouped/fixed mod
loot. Furniture bounds reject enclosed positions; structural triangle tests avoid
treating an entire room's wall bounds as solid. Poses require 0.1 m horizontal
obstacle clearance and 0.5 m separation from loot origins. The supporting floor
remains below the gun, with 0.01 m vertical clearance.

The earlier BoxCollider-only audit missed desk, trash-can, trunk, doorway and rug
geometry. The photographed office pose is retained as a rejection regression,
and all three old poses have been corrected. Evidence records the sealed shotgun
model template, resolved native geometry dependencies/hashes, coverage counts,
support and minimum furniture clearance. Missing relevant mesh data fails the
audit. Existing IDs, alternative names, roster fingerprint and save schema remain
unchanged; saved items are not moved or replaced.

Physical pickup, moving doors and customized safehouse furniture remain game
checks. The [location reference](https://thelongdark.fandom.com/wiki/Paradise_Meadows_Farm)
informed the bedroom/office choice; coordinates come from the installed scene.
See [spawn-format rules](RESTORATION.md#persistence) for authoring constraints.

## Container parts

`ShotgunDistribution.Parts` owns the nine table names and their part lists.
Each added part has relative weight 1, not a percentage or an extra draw. No
complete guns are added. Minimum resources are Medium; existing bans apply.

`ShotgunContainerLoot` scopes copies around `Container.PopulateWithRandomGear`.
Copies preserve original entries/weights, deduplicate parts, and own separate
mutable lists and Addressables operations. A Harmony finalizer restores table
references and destroys copies even on native failure. Original table names
remain, allowing ModComponent's existing ammunition additions to apply.

## Optional rifle replacements

**Replace Standard Rifle Finds** defaults off; **Replacement Chance** defaults
to 25%, clamped to 0–100% when used. It is independent of placed-find outcome
percentages and quotas. Usable shotguns must be enabled and resource-eligible.
Story/challenge modes are excluded.

The main-thread update waits for weapon configuration and native item startup.
Only active loose ordinary `GEAR_Rifle` instances with native GUIDs qualify.
Named/modded IDs, mission/collectible objects, inspected items, containers and
anything previously in player inventory or a container are excluded. Saved
scene contents remain authoritative. Each source GUID gets a seeded decision;
retention and replacement decisions are saved. A completed scene cannot reroll.

The initialized replacement is unloaded at the rifle's normalized condition.
Loaded rifle rounds become loose rifle ammunition. All new objects must
initialize before `GearManager` removes the rifle. Failure removes new objects
and retains the original, allowing the same decision to retry in that fresh
scene. No ammunition type is converted.

## Upgrade persistence

The existing `ShotgunSpawnStateV2` key and version remain. Optional fields
`DistributionVersion`, `PreDistributionScenes`, `RifleScenes` and
`RifleDecisions` extend the state. Distribution version 1 requires all three
collections; malformed data fails closed.

V1 history is reconstructed using historical capacities and selection rules.
Native saved scenes are reconciled before the frozen pre-expansion snapshot.
The gun allowance then increases once, retaining all spent slots, including
empty outcomes. Pending guns are reselected from the remaining unvisited pool.
Part/ammunition capacities, legacy bytes and permanent IDs are unchanged.

Old scenes, including unopened containers, keep prior distribution. An unvisited
interior in a visited region is eligible. A scene first explored after expansion
keeps its new container rules on later visits. Actual items, condition, ammunition
and positions remain owned by native serialization.

## Validation

The existing `Tests/SpawnCompatibility.csproj` harness links production
distribution, table scope and replacement code alongside parsing, migration,
ledger and policy code. Checks cover top-up, old/new scene gates, table isolation
and restoration, bans, chance boundaries, item exclusions, condition, ammunition
and rollback. Farmhouse checks also cover all three position choices, unchanged
lottery state/outcome RNG, alternative-row validation, existing reservations,
retry transforms, empty-slot consumption, reloads and save switching. Native
calls use boundary stand-ins, not game acceptance. See the
[portable command](RESTORATION.md#focused-regression-harness).

Game checks: inspect all new placements, including all three corrected farmhouse
positions in a newly explored scene; exercise doors and safehouse furniture;
pick up, save and revisit; open old and
new containers; test standard/named/dropped rifles with replacement off and at
0/25/100%; verify condition, unloaded clips and loose rifle rounds; test bans,
resource levels, absent optional regions and save-slot changes.
