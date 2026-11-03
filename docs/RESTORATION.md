# Shotgun maintenance, finds and restoration

Status: implemented in the rebuilt runtime DLL. The complete ammunition asset package is embedded and structurally verified. Game acceptance of spawn persistence and successful ruined-gun milling recovery remains pending.

## Find controls

The existing `SG_Shotgun` pool remains shared by usable and ruined finds. Its scene names and positions are unchanged; optional scenes absent from the installed game are still excluded.

| Option | Default |
|---|---|
| Spawn usable shotguns | On |
| Usable chance per selected location | 50% |
| Minimum resources for usable shotguns | High |
| Spawn ruined shotguns | On |
| Ruined chance per selected location | 50% |
| Minimum resources for ruined shotguns | Medium |

Minimum resources can be Low, Medium, High, or Very High. An eligible Low/Medium/High game selects one candidate; Very High selects two. Low selects none with the defaults. A disabled or ineligible type contributes zero chance, with no redistribution: default Medium gives 50% ruined and 50% empty. High/Very High give 50% usable and 50% ruined at each selected location.

Changing one percentage reduces the other if their sum would exceed 100. Invalid configuration loading clamps usable to 0вЂ“100, then ruined to the remaining percentage. No-firearm and custom no-shotgun bans still apply. These options do not alter part/ammo group counts or replace vanilla rifles.

## Inventory maintenance

`ShotgunMaintenance.Resolve` obtains the standard rifle's `Harvest`, `Cleanable`
and `Millable` components, validates their material/tool references and array
lengths, and resolves scrap metal and fir firewood before any shotgun weapon
components are created. Fir firewood is `GEAR_Hardwood`, whose game data uses
`GAMEPLAY_FirFirewood`; `GEAR_FirewoodFir` is not a vanilla address.

The shotgun's inventory Actions use the game's native components:

| Action | Configuration |
|---|---|
| Harvest | 2 scrap metal + 1 fir firewood; rifle duration, tools, skill and audio |
| Clean | Complete rifle `Cleanable` configuration, including its firearm cleaning kit |
| Ordinary milling repair | Rifle materials, quantities and duration |

In the inspected 2.59 rifle prefab, harvesting takes 60 minutes, requires no tool
and applies no skill. Cleaning uses `GEAR_RifleCleaningKit`, with serialized
condition-increase endpoints 2 and 5 and duration endpoints 15 and 5 minutes.
Ordinary milling repair takes 30 minutes and one scrap metal. Timing and other
copied fields remain governed by the game's normal behavior.

`ShotgunMaintenance.Configure` creates/configures `Harvest`, `Cleanable` and
`Millable`, then connects `GearItem.m_Harvest`, `m_Cleanable` and `m_Millable`.
Array containers are copied independently; resource/tool prefab references retain
their canonical identities. Changing shotgun yields or cleaning choices cannot
modify the rifle's arrays. No condition increase or cleaning-kit wear is applied
by this setup code: native cleaning handles both, including normal full-condition
and ruined-item eligibility. Ruined recovery remains a milling operation.

## Restoration

New usable finds now use `InitialCondition: Random`, matching the inspected
vanilla rifle, instead of the old High range. Runtime configuration copies the
rifle's start-condition mode without writing HP or invoking another condition
roll. Existing saved guns are not rerolled. Firing wear and skill rules are owned
by [skills and condition](SKILLS-AND-CONDITION.md).

Ruined finds retain `GEAR_DoubleBarrel` and start unloaded at zero condition. A transient, instance-checked marker reapplies zero condition after the verified `RollSeededGearConditionAndWeight` / `RollGearConditionAndWeight` APIs during the spawn frame. It expires at frame end and is not a save field; loaded or later repaired guns are not marked.

The shotgun receives a `Millable` component configured for Gunsmithing. Recover a ruined gun at a milling machine using **4 scrap metal + 1 fir firewood**, taking **180 base minutes**. Milling-machine power, skill, success, and recovery behavior follow the game. Ordinary repairs copy the standard rifle's milling materials and duration. Existing gun identity and serialized ammunition fields are unchanged.

## Persistence

code-synced: 2026-11-03

`SpawnLedger` records placement decisions for all groups: complete shotguns, parts
and grouped ammunition. Native scene/inventory serialization owns actual gear,
including its GUID, position, condition and loaded ammunition. The mod does not
generate deterministic object GUIDs or replace native save/load methods. Scenes
already present in `SaveGameSlots.GetSceneKeysForCurrentSaveSlot`, or processed in
the ledger/current session, receive no additional group filling. Fixed ModComponent
spawns remain under that framework's existing rules.

Each member in `Spawns/groupspawns.txt` has a mandatory permanent `id=`. Keep group
names and IDs unchanged when correcting coordinates or rotation. Never reuse a
retired ID for another prefab/group. Parsing rejects missing/duplicate IDs,
repeated groups, unknown lines/fields and malformed vectors/counts. Persisted
identity records also detect prefab/group reuse and ID case changes.

### Quotas and pool updates

Each group records its initial resource-level quota, processed IDs/outcomes,
pending selections and a roster fingerprint. Complete-gun capacity is one slot,
or two at Very High, with current usable/ruined eligibility controlling whether
unprocessed slots are selected. Other groups use their original count table.
Their capacity is fixed for that save; future count-table edits do not silently
increase an existing group's quota. New independent groups receive their own
capacity and can fill only unvisited scenes.

Empty outcomes spend a selected slot. Removing a processed point, picking up its
gear or changing chance settings never refunds it. Unchanged pools retain their
pending choices. Changed membership, scene assignment or region availability
reselects only the remaining capacity from unvisited, unprocessed candidates.
Canonical ordinal ID ordering removes dependence on text-line order; coordinates
and rotation are excluded from the fingerprint. Removed groups retain spent
slots and identity tombstones; reintroduced groups exclude scenes visited while
they were absent. Outcome chances are sampled when the new scene is planned.

The original FNV seed and seeded `System.Random` selection algorithm are retained.
Gun outcome RNG uses a separate stable-ID salt. The immutable migration snapshot's
IDs follow its original order, preserving its initial selection sequence.

### V1 migration and failure handling

The ModData namespace remains `Shotgun`. `ShotgunSpawnStateV2` stores version 2
with `VisitedScenes`, `Groups` and retained `Identities`. Group entries contain
`Quota`, `Fingerprint`, `Processed` and `Pending`; reservations retain their scene
as well as ID. `ShotgunSpawnOutcomesV1` and `ShotgunsInWorld` retain their names.
The spawn migration never writes or removes V1. Existing V2 takes priority and
does not import V1 again, including if an obsolete V1 record is malformed.

The frozen `Spawns/legacy-v1-groupspawns.txt` contains all 110 groups/283 original
points, their order/counts/transforms and permanent IDs. Its normalized UTF-8
SHA-256 is checked before use. It defines the exact old coordinate-key-to-ID map;
do not update it when editing current spawns. Historical alias collisions fail
validation rather than choosing an arbitrary member.

V1 selections and outcomes are authoritative, including empty selections and
empty outcomes. Unrecognized old selected keys become retained historical
tombstones and spend quota. For missing history, replay the frozen pool with the
save's seed/resource level and currently available regions, charging selected
points in previously saved scenes. Reconstruction cannot recover a past region
list or earlier settings; it may differ from actual historical finds. It never
edits already saved gear or attempts to infer origin from the current inventory.

Writes occur only on the existing normal slot-save hook. Failed ModData writes
are reported, retain in-memory decisions and retry on the next normal save. A
native saved scene ahead of the ledger consumes its pending reservations without
refilling them. Native saves and ModData writes are not an atomic transaction;
complete loss of unwritten history still has the reconstruction limitation above.

Malformed, incomplete or unsupported state disables group injection for that
save load and preserves stored data. There is no legacy-spawn fallback. Missing
slot/scene information, an unloaded ModData cache, an unready catalog or missing
prefab defers work without committing an empty scene. Because ModData 1.5.5 returns
null both for an unloaded cache and an absent key, a narrow read-only readiness
check inspects its existing `cacheLoaded` field before reading keys. Unsupported
readiness metadata fails closed. Custom-game availability is loaded before rolls.

Deferred work runs from the existing main-thread update, scoped to the active
scene and save. Reset/menu entry, slot switches and unload cancel stale requests.
Plans and successful-attempt guards prevent repeated quality callbacks or a
partial native failure from rolling replacements in the loaded scene. Prefabs are
preflighted before committing a scene; one Addressables ownership handle per
successful prefab remains for process lifetime to support manual instances.

### Focused regression harness

From the repository root:

```text
dotnet run --project Tests/SpawnCompatibility.csproj -c Release -- .
```

The dependency-free .NET 6 harness links production parsing, ledger, migration and
runtime-policy code. Only Unity/game/ModData boundaries are stand-ins. It seals
the legacy snapshot, checks current IDs/counts/transforms and ammo order, and
exercises pool edits, quotas, V1/V2 state, bans, readiness, retries and slot changes.
It does not execute native scene deserialization or establish in-game acceptance.

## Pending game checks

Check old visited areas, unvisited areas, all resource levels, chance endpoints, disabled finds, bans, optional-region absence, save switching, repeated quality-setting calls, and pickup/revisit behavior. Verify zero-condition items remain present and unfireable, retain their state through save/load, appear in the milling UI, consume the selected recovery materials, and become usable afterward.

Across an actual version upgrade, repeat with picked-up, left, moved and dismantled
group loot; loaded and restored shotguns; an already consumed empty slot; and a
changed optional-region list. Confirm existing gear is restored by the game,
visited scenes receive no group refill, and only remaining quotas can populate
unvisited scenes. Check V1-to-V2 and a subsequent V2-to-V2 update separately.

Check inventory Use/Actions, radial and rifle-cycle selection, cleaning with and
without a kit, kit wear, full-condition/ruined eligibility, and harvesting loaded
and unloaded weapons without ammunition duplication or unintended loss. Repeat
on newly spawned and saved shotguns. The production maintenance harness checks
configuration, missing inputs and independent arrays with native boundaries
replaced; it does not execute Unity or establish in-game acceptance.

Sources: [maintenance](../Main/ShotgunMaintenance.cs), [spawn policy](../Main/ShotgunSpawnPolicy.cs), [group spawner](../Main/GroupSpawner.cs), [ruined-condition handling](../Main/ShotgunRestoration.cs), and [settings](../Settings/Settings.cs).
