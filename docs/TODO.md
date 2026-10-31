# Shotgun to-do list

Recorded: 2026-10-31. This backlog includes future ideas and pending acceptance of source changes. Entries name implementation gaps; none establish compatibility guarantees. Checkbox numbers follow the requested order, not priority.

1. [ ] **Optional replacement of vanilla rifle spawns.** Add an option for a shotgun to spawn in place of a standard vanilla rifle. Exclude every named rifle variant. Decide the replacement chance and interaction with existing shotgun spawns and availability settings; preserve deterministic selection and existing-save behavior.

2. [ ] **Optional empty-casing crafting.** Add an option to disable crafting empty shotgun shell casings, following vanilla's noncraftable firearm-casing approach. Preserve a way to enable it for established saves where all locations have already been visited and new casing supplies may be unavailable. Decide the default and whether the choice belongs to a save or global settings. Current recipe: 1 scrap metal produces 6 casings at a lit forge in 30 base minutes.

3. [ ] **Validate the projectile-material pipeline.** Runtime/recipe source now uses vanilla Bullet for slugs and distinct lead buckshot/birdshot portions. Yields, station options, and source-authoring handoff are recorded in [AMMUNITION.md](AMMUNITION.md). Remaining gap: Unity prefab authoring/import, package export, compilation, and game acceptance.

4. [ ] **Validate scrapshot.** Source integration and artwork are prepared: six 13-damage pellets, 2.2-degree spread, and separate workbenchable scrap-portion preparation and shell assembly. See [SCRAPSHOT-DESIGN.md](SCRAPSHOT-DESIGN.md). No wad or small-game scrap load. Remaining gap: asset import/export, compilation, and game checks.

5. [ ] **Bleeding and weapon-mod compatibility work.** Investigate a patch, rework, or integration with [BleedingRevamped](https://github.com/RomainDeschampsFR/BleedingRevamped) and [Adaptive Arsenal](https://github.com/Deaadman/adaptive-arsenal). Planning note from the user: **BleedingRevamped itself needs a deep overhaul**, so this may require substantial work beyond a shotgun compatibility patch. Audit current versions before deciding the approach. Check pellet aggregation, bleed thresholds and timers, critical rolls, damage/falloff, and patch ordering; keep integrations optional. No compatibility status is established by this backlog entry.

6. [ ] **Consider more shotgun variants.** Explore additional shotgun types or named variants with distinct roles. Decide what should differ in handling, capacity, weight, condition loss, ammunition support, and availability before adding assets or identifiers.

7. [ ] **Test birdshot against different small animals.** Include rabbits and ptarmigans; identify any other relevant supported small wildlife. Test damage, pellet coverage, practical range, single- versus double-barrel use, and ammunition efficiency. Review bleed and critical settings where applicable, with and without optional combat integrations.

8. [ ] **Test slug balance against large predators and moose.** Test cougar, bear, and moose, comparing the shotgun with the standard rifle and every rifle variant available in the supported game version. Cover hit regions, distance/falloff, skill levels, critical kills, bleed behavior where applicable, follow-up shots, and both-barrel fire. Record the tested game and mod versions.

9. [ ] **Validate ammunition weight and balance weapon weight.** Source shell definitions now use **40 g per round**, including scrapshot, twice the vanilla rifle round. Verify loaded/stacked weights after the deferred asset build and runtime compilation. Weapon/casing weights remain unchanged; retain the [ammunition reference](AMMUNITION.md) and the historical baseline below for comparisons.

10. [ ] **Load different ammunition into each barrel.** Design independent barrel contents, firing order, HUD, ammo switching, and save migration while preserving existing uniformly loaded guns.

11. [ ] **Restrained beachcombing support.** Add shotgun casings and ammunition at availability and quantity comparable to vanilla ammunition. Respect shotgun/firearm bans; avoid abundant renewable loaded shells.

## Historical shotgun weight baseline

Checked against the Unity item definitions and the matching entries in the embedded `DoubleBarrel.modcomponent` on 2026-10-31. These are the earlier 20 g shell values, not an in-game weighing test. Source shell weights were subsequently changed to 40 g; see AMMUNITION.md. The embedded package has not been rebuilt.

| Item | Configured unit weight | Stack size / calculated weight |
|---|---:|---|
| Shotgun | 3.00 kg | Weapon item; verify loaded-ammunition accounting in game |
| Slug shell | 0.020 kg (20 g) | 6 shells = 0.120 kg; 100 shells = 2.00 kg |
| Buckshot shell | 0.020 kg (20 g) | 6 shells = 0.120 kg; 100 shells = 2.00 kg |
| Birdshot shell | 0.020 kg (20 g) | 6 shells = 0.120 kg; 100 shells = 2.00 kg |
| Empty shotgun casing | 0.010 kg (10 g) | 6 casings = 0.060 kg; 100 casings = 1.00 kg |

Each ammo-box definition has `WeightKG: 0.02` and `UnitsPerItem: 6`. The 0.120 kg box figures above are calculated as six ammunition units; confirm stack behavior and the displayed inventory total in game. No separate packaging weight is declared in these definitions.

Sources: [weapon](../unity/Assets/_ModComponent/shotgun/auto-mapped/DoubleBarrel.json), [casing](../unity/Assets/_ModComponent/shotgun/auto-mapped/DoubleBarrelAmmoCasing.json), [slug](../unity/Assets/_ModComponent/shotgun/auto-mapped/DoubleBarrelAmmoSingleSlug.json), [buckshot](../unity/Assets/_ModComponent/shotgun/auto-mapped/DoubleBarrelAmmoSingleBuckshot.json), [birdshot](../unity/Assets/_ModComponent/shotgun/auto-mapped/DoubleBarrelAmmoSingleBirdshot.json), and box definitions in the same directory.

For any mechanic implemented from this list, validate it first, then update the owning player and developer documentation. Update compatibility notes only when testing establishes a support or conflict status.
