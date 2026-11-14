# Double-Barreled Shotgun

*Double Barrel All The Way*

- Equip from inventory with **Use**, or press **2** to cycle between carried rifles and shotguns.
- Hold **Left Alt** while firing to fire both barrels at once
- Tap **T** to cycle to the next shell type, or hold it for the ammo wheel:
  - Slugs
  - Buckshot
  - Birdshot
  - Scrapshot
- Make it (barrel, stock, forend, trigger) or find it on Voyager or Pilgrim
- Find or make your own ammo, just like vanilla
- Train Rifle with shotgun hits, or a dedicated Shotgun skill with optional Filigrani's Skills integration
- (Open Beta Only) Many aspects can be changed in Mod Settings; experiment and enjoy!

![ammo wheel](media/ammo_wheel.jpg)

Aiming uses the game's hold/toggle preference. Shotgun sounds follow the SFX and master volume settings; all six sounds ship inside DoubleBarrel.dll, with no separate sound download. The loaded Scrapshot HUD uses the same white shell silhouette as the other ammunition, with cut-metal shapes inside.

Spent shells land more quietly on snow, dirt, straw, rugs, cloth and flesh. Other weapon sounds retain their usual volume.

If aiming will not start while walking, stop briefly to aim, then resume movement. If touchpad taps or its left button do not select radial entries, use a physical mouse's left button. Both behaviors were also reported with vanilla items/UI; see the [input observations](docs/VANILLA-INPUT-ISSUES.md).

If aiming or weapon switching stops responding, enable **Aim Diagnostics** in Mod Settings and include the resulting MelonLoader log with your report. It is off by default.

## Ammunition and crafting

Slug shells use the vanilla Bullet component. Buckshot and birdshot use their own lead-shot portions, while scrapshot uses prepared scrap buckshot. Each portion supplies one shell, regardless of pellet-count settings. All finished shotgun shells weigh 40 g; a six-shell box weighs 240 g.

In inventory, loose rounds and boxes display condition labels such as **Perfect Buckshot Ammunition** at 100%. Slug, birdshot and scrapshot ammunition follow the same naming rule. Crafting recipes use **Slug Shell**, **Buckshot Shell**, **Birdshot Shell**, and **Scrapshot Shell**. A single round in the world uses its shell name when highlighted or inspected; multi-round boxes keep their ammunition names. Picking up a loose shell restores the usual inventory quality name.

Crafting shells improves **Gunsmithing**. By default, Gunsmithing levels 1–5 produce shells at **40%, 60%, 80%, 100% and 100% condition**. Under **Crafted Shell Condition** in Mod Options, change **Gunsmithing Condition Controls** from **Collapsed** to **Expanded** to reveal five condition sliders. Confirmed values affect subsequent completed shells, including resumed crafting; existing ammunition keeps its condition. Harvesting ammunition gives no Gunsmithing XP, matching vanilla.

Shells and their boxes use matching colors: **red buckshot, dark blue slugs, green birdshot, and grey scrapshot**. Harvesting live ammunition recovers its casing, matching projectile material, and **10 g of gunpowder per round**. A full six-round box returns 60 g; partial stacks return powder for the rounds harvested. Empty casings contain no recoverable gunpowder.

| Preparation | Output | Location | Base time |
|---|---|---|---:|
| 1 scrap lead | 6 lead buckshot portions | Lit ammunition forge | 60 minutes |
| 1 scrap lead | 12 lead birdshot portions | Lit ammunition forge | 60 minutes |
| 1 scrap metal | 6 scrap buckshot portions | Workbench, simple tools | 30 minutes |

Combine one matching portion (or one Bullet for a slug), one recovered shotgun casing, and 10 g gunpowder to make one shell in five base minutes. Lead shell assembly uses an ammunition workbench. Scrapshot assembly works at an ordinary workbench without an additional tool. Workbench actions require light. Recover spent casings from the ground after reloading; they can be reused.

Three options, off by default, add ordinary-forge bullet casting, ordinary-forge lead-shot casting, and ordinary-workbench lead shotgun shell assembly. They take effect on the next game load and preserve the default recipes. Vanilla rifle and revolver cartridge recipes are unchanged. Scrapshot is crafting-only and uses six slightly weaker pellets with a slightly wider pattern than lead buckshot.

### Hunting with different loads

Slugs are the strongest large-game load. At default settings, two landed headshots within 30 metres are designed to kill a full-health Aurora bear without a lucky critical. Slug wounds bleed out twice as quickly as equivalent buckshot wounds on animals that can bleed.

One birdshot, buckshot or scrapshot pellet kills a rabbit or ptarmigan, including after distance loss. Birdshot is otherwise a small-game load: a concentrated head pattern can kill a deer or wolf, but body hits are weak and it does almost nothing to bears, moose or cougars. It cannot cause critical kills. Fifteen hits on one deer or wolf allow one 15% bleeding chance per shell; a successful wound takes at least four game hours to bleed out. Birdshot never makes bears bleed.

Buckshot retains nine strong pellets for deer, wolves and close-range bear hunting. Scrapshot retains six weaker pellets and its wider pattern; its bleeding wounds take 50% longer. Both need three hits on one animal for bleeding and six hits within their effective range for one critical chance. Moose take substantially less damage from either load across every body part and have much lower critical chances; cougar critical chances are also reduced. Moose and cougars do not bleed. Each barrel resolves its own shell and thresholds.

These rules also apply with the updated Adaptive Arsenal integration; its projectile flight and distance loss remain in effect. Combat acceptance in the game is still pending. On the first start of this update, **Slug Base Damage resets to 150 even if customized**. You can change it again afterward; later starts preserve your choice. Other saved settings and existing items are retained.

## Maintenance

Each successfully fired barrel costs **1% condition**, reduced by skill to **0.5% at maximum level**. A double shot costs twice as much. Misses still wear the weapon; empty triggers, jams and misfires do not.

Use the inventory **Actions** menu to clean the shotgun with a firearm cleaning kit. Condition recovery, cleaning time, tool wear and eligibility follow the hunting rifle.

Harvesting a shotgun returns **2 scrap metal and 1 fir firewood**. It takes **60 base minutes**, with no tool required, matching the rifle's harvesting time and tool requirements.

## Skills

Without a separate Shotgun skill, **Use Rifle Skill Without Shotgun Skill** is on by default: Rifle level controls shotgun handling, recoil, critical chance and wear, and hitting a live animal grants **one Rifle point per shell**, regardless of pellet or target count. Both hitting barrels can earn two points. Turn the option off to retain fixed handling without Rifle experience or Rifle-level bonuses. When the separate Shotgun skill is available, it takes priority for both experience and benefits.

## Finding and restoring shotguns

Usable and ruined shotguns share the existing complete-gun locations. Mod Settings independently control each type's availability, chance, and minimum resource level. Defaults are 50% usable at High or above, and 50% ruined at Medium or above. Blocked chances remain empty: on Medium the default is 50% ruined and 50% empty. Previously visited areas retain their saved finds.

The world selects **0 / 2 / 3 / 4 complete-gun locations** at Low / Medium / High / Very High base resources. These are selected locations, not guaranteed usable guns. The wider pool includes hunting cabins, farmhouses, workshops and remote shelters. Existing saves gain only their remaining allowance in unexplored scenes; empty results still spend slots.

From Medium resources upward, rare shotgun parts can also appear in rifle cargo, locked lockers, tool storage and vehicle trunks. Container tables add no complete shotguns.

**Replace Standard Rifle Finds** is an optional Mod Settings switch, **off by default**. Its independent **Replacement Chance** defaults to **25%**. Eligible ordinary rifle finds in newly explored scenes can become usable, unloaded shotguns at the rifle's condition. Named rifles, mission items, container contents and player-carried or dropped weapons are preserved. Loaded rifle rounds are left as loose rifle ammunition. Usable shotguns must be enabled and permitted at that resource level. Replacements are additional to the world allowance.

Previously visited exteriors and interiors, including unopened containers, keep their prior distribution. An unexplored building in a visited region can use the new rules. Settings changes never reroll a processed rifle find.

Iron Road interior finds use the corrected scene names from upstream 0.14.0. Existing spawn history is retained across the name corrections, so revisiting an explored interior does not refill its grouped finds.

Updates can change future finds in unexplored areas within each group's remaining allowance. This applies to grouped shotguns, parts and ammunition. A processed location spends its allowance even when its result is empty; picking up, moving or dismantling its loot does not create a replacement. Chance and eligibility settings affect newly explored areas. Existing fixed finds retain their usual placement rules.

When upgrading from a version without complete spawn history, remaining allowances are reconstructed from the earlier placement rules. A different region-mod list or earlier settings can make that estimate differ from the actual past finds. Saved items keep the game's normal condition, ammunition and placement persistence.

New usable finds use the rifle's random starting-condition behavior. Existing saved shotguns retain their condition; ruined finds remain unloaded at 0%.

Restore a ruined shotgun at a milling machine with **4 scrap metal and 1 fir firewood**, taking **three base hours**, subject to normal milling-machine requirements and skill rules.

<details>
<summary><b>Spoilers: Geoguessing with a Double Barrel</b></summary>

Complete guns share one world-wide pool:

| Region | Possible location |
|---|---|
| Blackrock | Prison interior |
| Zone of Contamination | Langston Mine |
| Mystery Lake | Trapper's Homestead, by the gun-storage area |
| Broken Railroad | Hunting Lodge basement |
| Pleasant Valley | Farmstead bedroom |
| Mountain Town | Paradise Meadows farmhouse: clear wooden floor near the bedroom doorway, beside the bedroom bed, or on the office floor beside the desk |
| Coastal Highway | Quonset rear living/storage area |
| Timberwolf Mountain | Mountaineer's Hut workbench |
| Desolation Point | Hibernia upstairs counter |
| Forsaken Airfield | Hangar equipment storage |
| Optional mod region | Shattered Bunker |

Paradise Meadows farmhouse is one candidate in this pool. If selected, its find
uses one of the three indoor positions with equal chance; additional positions
do not increase its selection chance or the world's shotgun allowance.
Placement corrections apply to future finds; already saved shotguns keep their
positions.

Additional part candidates include the cannery workshop, Hibernia, hangar,
mine concentrator, Trapper's, both farmhouses, Mountaineer's Hut, Lonely
Lighthouse and the Pleasant Valley barn. These locations are candidates, not
guaranteed finds.

<img src="media/spot_Dam_Barrel.jpg" alt="Barrel: Upper Dam, Mystery Lake" width="49%">
<img src="media/spot_CampOffice_Forend.jpg" alt="Forend: Camp Office, Mystery Lake" width="49%">
<img src="media/spot_MiningRegionMine_Gun.jpg" alt="The gun: Langston Mine, Zone of Contamination" width="49%">
<img src="media/spot_MountainPassBasement_Stock.jpg" alt="Stock: Basement, Sundered Pass" width="49%">
<img src="media/spot_BlackrockPrison_Trigger.jpg" alt="Trigger: Blackrock Prison Buildings" width="49%">

</details>

## Installation

- MelonLoader 0.7.2
- [ModComponent](https://github.com/dommrogers/ModComponent/releases/latest/download/ModComponent.dll)
- [ModSettings](https://github.com/DigitalzombieTLD/ModSettings/releases/latest/download/ModSettings.dll)
- [ModData](https://github.com/dommrogers/ModData/releases/latest/download/ModData.dll)
- [MehToolBox](https://github.com/monsieurmeh/MehToolBox/releases/latest/download/MehToolBox.dll)
- [AudioCore](https://github.com/DigitalzombieTLD/AudioCore/releases/latest/download/AudioCore.dll) (goes in /plugins/)
- [AudioManager](https://github.com/DigitalzombieTLD/AudioManager/releases/latest/download/AudioManager.dll)
- [Filigrani's Skills](https://github.com/No3371/TLD-FiligranisSkills/releases/latest/download/FiligranisSkills.dll) (optional)
- Pop [DoubleBarrel.dll](https://github.com/monsieurmeh/Shotgun/releases/latest/download/DoubleBarrel.dll) in /mods/ and enjoy the show

## Team Shotgun
- [Kai](https://www.patreon.com/cw/Kaiusername1) - 2D & 3D Artwork for Shotgun & Ammo
- [Waltz](https://www.artstation.com/fardyhernandez) - 3D Animation
- [Flower Field](https://ko-fi.com/flowerfield123) - 2D & 3D Artwork for custom skill content
- Mako - Sound Design
- [Lithar](https://www.youtube.com/@Lithar-TheLongDark) - Marketing like a boss
- [MonsieurMeh](https://github.com/monsieurmeh) - Programming

Special shoutout to everyone that helped me test in 2026!
- Red (red6785)
- Viola (romiebee2077)
- System Ether (systeme_ether)
- TKG (thekillergreece)
- Gavin (gavinrod)

If you don't see your name here tell me and I will fix! You know how forgetful I am.

## Development

The rebuilt runtime package contains the three projectile materials, scrapshot shells and open cardboard box, their inventory/HUD icons, updated recipes and 40 g shells. The package reads directly from the DLL through ModComponent; it no longer creates a separate `DoubleBarrel.modcomponent`. ModComponent's standard working cache remains. After successful loading, known archives extracted by earlier builds are removed; modified or unknown files are preserved. The Unity-free build and consumer lookup/lifecycle checks pass; in-game testing remains pending. See the [ammunition source handoff](unity/SourceArt/README.md) for asset inputs and validation boundaries.

Start with the [developer router](docs/FEATURES.md) for source locations and validation guidance. [Storage notes](docs/STORAGE-NOTES.md) describe the local partial-clone setup and build-output guard.
