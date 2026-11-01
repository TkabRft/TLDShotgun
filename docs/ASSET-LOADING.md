# Embedded asset loading

code-synced: 2026-11-01

Status: implemented and tested in an isolated .NET 6 lifecycle harness, including
the real JsonHandler source, Harmony 2.10.2 and API binding to the official
ModComponent 7.0.0 DLL. Full Unity/game acceptance remains pending.

## Package data

The authoring data root owns the complete `auto-mapped`, `blueprints`,
`localizations`, `recipes` and `gear-spawns` sections. The builder removes their
template entries before writing current sources with lowercase filenames. An
absent section directory means an empty section. Gear IDs, blueprint names,
localization keys and Addressables keys inside the data retain their identities.

The earlier generated package retained old entries and added case-distinct new
ones. ModComponent registers JSON by literal filename stem but looks it up with
`ToLowerInvariant()`. This hid all five new item definitions and selected stale
20 g/Bullet-based definitions for existing ammunition. Five old default blueprints
also survived under different filenames. The previous direct-file checks missed
the consumer lookup error; they did not establish successful game registration.

Validation now resolves all 18 addressable gear definitions through the consumer
registry, checks the effective 20 g portions, 40 g shells and six-unit boxes,
disassembly projectiles, and exactly nine current default blueprints. The isolated
C# harness compiles the unmodified ModComponent JsonHandler source and reproduces
the old failure before checking the corrected package.

## Lifecycle

`EmbeddedAssetPackage` installs three scoped managed Harmony patches during early
initialization, before ModComponent initializes its assets. `ModComponentPackageBridge`
checks the required 7.0 loader signatures before installation; it does not require
a patched ModComponent DLL or add a public API.

1. The standalone-file prefix skips only the exact `Mods/DoubleBarrel.modcomponent`
   path. Other packs keep their normal loading path.
2. After `ZipFileLoader.Initialize`, the bridge reads
   `Shotgun.DoubleBarrel.modcomponent` directly with ZipArchive over the DLL resource
   stream. It delegates each supported entry to ModComponent's existing
   `GetFileType`/`TryHandleFile`, before standard bundle preloading/catalog loading.
3. ModComponent still creates its standard `_ModComponentTemp` working bundles and
   catalogs. The bridge never writes a complete archive, including to a temp file.
4. The bridge registers the package hash and its buildinfo dependency/DLC rules.
   The dependency-reader postfix adds the virtual `doublebarrel` identity without
   adding a nonexistent archive to ModComponent's file paths.
5. The own catalog's self-test target becomes its existing `GEAR_DoubleBarrel`
   prefab. No Addressables table, resource ID, bundle bytes or CRC check is changed.

Registration occurs once per process. An entry handler returning false fails the
load and prevents a partial retry, success marking or legacy cleanup. No standalone
fallback is used. A changed DLL resource is read on the next process launch even
when the mod version remains unchanged. FiligranisSkills lookup uses exact assembly
and type names; absence disables only that optional skill and does not scan every
interop type or suppress unrelated warnings.

## Weapon initialization and inventory actions

Package registration precedes weapon setup. After `vp_FPSCamera.LoadAllFPSWeapons`,
`Main.LateInitialize` calls `Configuration.PrepareShotgun` to obtain the vanilla
templates, add and configure the shotgun's `GunItem`, `FirstPersonItem` and
`Cleanable`, and register its first-person weapon. A failure while obtaining
templates leaves the shotgun as a generic inventory item, without weapon actions.
The existing rifle-cycling patch also requires a configured `GunItem`.

Local commit `0fb3def` incorrectly made `Harvestable` mandatory on vanilla rifle
ammunition. A game log confirmed that `Null GEAR_RifleAmmoSingle.Harvestable`
aborted setup before weapon components were created. The first rollback restored
optional vanilla lookups but was incomplete: the next game test stopped at
`Null GEAR_DoubleBarrelAmmoSingleSlug.Harvestable`.

ModComponent 7.0.0's `HarvestableMapper` maps `ModHarvestableBehaviour` to the game
component **`Harvest`**, which derives from `HarvestBase`. `Harvestable` is a
different component. `AmmoPrefabRefs` and all four single/box lookups now use
`Harvest`. Vanilla ammunition's component remains optional and is not consumed by
shotgun configuration; the shotgun's own ammunition requires the actual `Harvest`
component. Both the pinned 2.51 API and the tested game's 2.59 interop assemblies
expose this type. No API package change is needed.

Validation compares the compiled getter/delegate component queries and reference
type with the component created by the installed ModComponent mapper. The previous
DLL is rejected as a negative control; the corrected DLL matches all four lookups,
including optional vanilla and required shotgun predicates. All eight embedded
single/box definitions retain their matching disassembly projectiles, 40 g units
and six-unit boxes. The corrected runtime snapshot was published only after these
checks passed.
That initialization repair left weapon controls, cycling and save contracts
unchanged; no inventory reset or save migration was introduced.

## Legacy archive migration

After embedded registration succeeds and `ModComponent.Public.IsReady()` confirms
prefab mapping finished, the next update checks only the exact legacy archive path.
It removes a regular file only when SHA-256 matches the current embedded package,
the original template, or the previously shipped case-broken package. Unknown or
modified files and links are preserved with a diagnostic. Cleanup is non-recursive;
the settings/metadata directory, saves and other mods are untouched. An I/O failure
leaves the file in place and is reported once.

The build never performs this migration in the installed game. It occurs only
when the user launches the updated DLL. Acceptance should confirm that no archive
is recreated, all items and recipes load, the standard working cache still loads,
and a known old archive disappears after readiness. Also verify crafting,
disassembly, weights, icons, firing/reload, saves and shotgun restoration in game.
