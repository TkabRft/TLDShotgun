# TLDShotgun — developer router

Read this file first, then open only the owner documentation and source relevant to the task. Also follow the applicable workspace and repository agent instructions.

## Baseline

- The player overview, controls, installation requirements, and credits are in [`../README.md`](../README.md).
- Runtime C# lives in `Main/`, `Settings/`, `Util/`, and `ToolBox/`. Spawn source data is in `Spawns/`.
- [`../Shotgun.csproj`](../Shotgun.csproj) targets `net6.0`, declares `STBlade.Modding.TLD.Il2CppAssemblies.Windows` `2.51.0`, and produces `DoubleBarrel.dll`. The C# namespace remains `Shotgun`.
- The DLL embeds `DoubleBarrel.modcomponent` (resource name `Shotgun.DoubleBarrel.modcomponent`) and `Spawns/groupspawns.txt` (resource name `Shotgun.groupspawns.txt`). Runtime initialization atomically refreshes its own embedded package by SHA-256, including when the mod version is unchanged.
- `unity/` is the source asset project. The editor version is recorded in `unity/ProjectSettings/ProjectVersion.txt`; package inputs are in `unity/Packages/`.
- Existing generated packages and bundles are still tracked. They are required inputs or existing upstream content, not permission to commit every rebuild. The current clone uses partial fetching and a local artifact guard; see [storage notes](STORAGE-NOTES.md).

## Where a change lives

Paths below are relative to the mod root.

| Task | Start with |
|---|---|
| Planned work, balance tests, and recorded weight baseline | [To-do list](TODO.md) |
| All ammunition, vanilla references, recipes, and selected shell-weight target | [Ammunition reference](AMMUNITION.md) |
| Ruined/usable finds, milling restoration, and spawn persistence | [Restoration](RESTORATION.md), `Main/ShotgunSpawnPolicy.cs`, `Main/ShotgunRestoration.cs` |
| Projectile materials, optional recipe registration, and asset preparation | `Main/ShotgunCrafting.cs`, [source authoring handoff](../unity/SourceArt/README.md) |
| Proposed scrapshot stats, crafting recipe, and balance rationale | [Scrapshot design](SCRAPSHOT-DESIGN.md) |
| Initialization, embedded assets, resource registration, scene lifecycle | `Main/Main.cs`, `Util/General.cs` |
| Weapon and item configuration | `Main/Configuration.cs`, then the specific patch in `Main/Patches.cs` |
| Firing both barrels | `Main/DoubleFire.cs` |
| Pellets, damage, and associated effects | `Main/ShotgunPellets.cs` |
| Ammo wheel and selection | `Main/AmmoRadial.cs`, `Main/AmmoSwapPrompt.cs` |
| Ammo HUD | `Main/CustomAmmoHUD.cs` |
| Weapon actions and animation callbacks | `Main/Actions.cs`, `Main/AnimationEventProxy.cs` |
| Availability, custom difficulty, persistence | `Main/ShotgunAvailability.cs`, `Main/ShotgunAvailabilityPatches.cs` |
| Spawn parsing and group selection | `Main/GroupSpawner.cs`, `Spawns/groupspawns.txt` |
| Shotgun skill and FiligranisSkills integration | `Main/ShotgunSkill.cs` |
| Mod settings | `Settings/Settings.cs` |
| Mod identity and assembly metadata | `Settings/AssemblyInfo.cs`, `Main/Main.cs`, `Shotgun.csproj` |
| Shared references and UI/transform helpers | `Main/Refs.cs`, `Util/`, `ToolBox/` |
| Models, textures, prefabs, animation, audio, and Addressables | the relevant asset under `unity/Assets/`, with its `.meta` file |
| Disk use, partial fetching, generated-output guard | [STORAGE-NOTES.md](STORAGE-NOTES.md) |

The ammunition and restoration pages own their mechanic details and pending acceptance checks. Keep this table as a routing map. Player-visible changes should also update the relevant README section, or the owning wiki page if a wiki is introduced.

## Invariants

- Preserve namespace, prefab/gear IDs, Addressables keys, Unity GUIDs, animation event names, save/settings/localization keys, and embedded-resource names unless the task explicitly includes their migration.
- Read both initialization and scene-lifecycle paths before changing when assets or runtime patches become available.
- Keep FiligranisSkills optional at runtime. A project compile reference is separate from runtime availability.
- Keep vanilla behavior for other weapons and items. Follow the existing Harmony guards rather than applying behavior globally.
- Check spawn data against `GroupSpawner`'s parser and scene/prefab references; do not assume all scene names exist in every game installation.
- Read current C# and assets before claiming a mechanic exists or is missing. This router does not replace source verification.

## Validation

Use the workspace build rules. From the mod root, the runtime build command is:

```powershell
dotnet build ./Shotgun.csproj -c Release -p:NoDeploy=true -p:ModBuildCodeOnly=true --nologo --verbosity quiet
```

This is an explicit diagnostic C# build; it does not refresh a playable Release. Full builds use the workspace's ModBuild entry point, which converts assets first and passes `AssetPackagePath` plus the validated hash. A pre-compilation guard rejects an unvalidated/missing/changed package. `NoDeploy=true` is required because the default post-build target copies files into the game installation. Supply dependency paths as MSBuild properties when needed; commands, profiles and log conventions belong to the workspace instructions.

`ModReferencesPath` optionally selects a separate directory containing the managed mod dependency DLLs; it defaults to the game's `Mods` folder. This supports compilation without installing those dependencies. A missing FiligranisSkills DLL is omitted from compile references; the existing reflection-based runtime integration remains optional.

C# compilation alone does not regenerate bundles or the `.modcomponent`. The full template-based UnityPy pipeline consumes `unity/SourceArt/no-unity-build.json` and the authored GLB/PNG inputs, validates the catalog/CRC and all bundle references, and verifies the exact embedded package after compilation. Existing shaders, controllers, clips and sound banks remain in the compatible template. Own audio resources use the hash-based `Main/EmbeddedAudio.cs` adapter before the Addressables fallback. Game acceptance remains separate; name the focused game checks. Documentation-only edits need neither compilation nor a local release.

code-synced: 2026-10-31
