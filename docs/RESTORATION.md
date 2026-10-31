# Shotgun finds and restoration

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

## Restoration

Ruined finds retain `GEAR_DoubleBarrel` and start unloaded at zero condition. A transient, instance-checked marker reapplies zero condition after the verified `RollSeededGearConditionAndWeight` / `RollGearConditionAndWeight` APIs during the spawn frame. It expires at frame end and is not a save field; loaded or later repaired guns are not marked.

The shotgun receives a `Millable` component configured for Gunsmithing. Recover a ruined gun at a milling machine using **4 scrap metal + 1 fir firewood**, taking **180 base minutes**. Milling-machine power, skill, success, and recovery behavior follow the game. Ordinary repairs copy the standard rifle's milling materials and duration. Existing gun identity and serialized ammunition fields are unchanged.

## Persistence

`ShotgunSpawnPolicy` snapshots the current save's existing scene keys using `SaveGameSlots.GetSceneKeysForCurrentSaveSlot`. Previously saved areas keep the legacy group-selection path and their serialized gear. New areas record their selected gun locations and empty/usable/ruined outcomes. Settings changes apply only to subsequently unvisited areas.

The existing ModData namespace remains `Shotgun`. The new suffix is `ShotgunSpawnOutcomesV1`, with version 1 state containing `LegacyScenes`, `Selections`, and `Outcomes`. Selection/outcome records are saved with the normal slot save. Seeded outcome rolls use a separate key from group selection, leaving other groups' RNG unchanged. Cached records and scene-attempt guards reset on save restoration/menu entry; scene attempts clear on unload. Repeated quality-setting callbacks cannot duplicate the same successful spawn in a loaded scene.

Unavailable or malformed scene/state information falls back to legacy spawning for that load and logs the reason. It does not rewrite or delete saves.

## Pending game checks

Check old visited areas, unvisited areas, all resource levels, chance endpoints, disabled finds, bans, optional-region absence, save switching, repeated quality-setting calls, and pickup/revisit behavior. Verify zero-condition items remain present and unfireable, retain their state through save/load, appear in the milling UI, consume the selected recovery materials, and become usable afterward. These are acceptance requirements, not completed in-game tests.

Sources: [spawn policy](../Main/ShotgunSpawnPolicy.cs), [group spawner](../Main/GroupSpawner.cs), [restoration](../Main/ShotgunRestoration.cs), and [settings](../Settings/Settings.cs).
