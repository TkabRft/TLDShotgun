# Double-Barreled Shotgun

*Double Barrel All The Way*

- Hold **Left Alt** while firing to fire both barrels at once
- Tap **T** to cycle to the next shell type, or hold it for the ammo wheel:
  - Slugs
  - Buckshot
  - Birdshot
  - Scrapshot
- Make it (barrel, stock, forend, trigger) or find it on Voyager or Pilgrim
- Find or make your own ammo, just like vanilla
- Level up a dedicated skill with optional integration with Filigrani's Skills
- (Open Beta Only) Many aspects can be changed in Mod Settings; experiment and enjoy!

![ammo wheel](media/ammo_wheel.jpg)

## Ammunition and crafting

Slug shells use the vanilla Bullet component. Buckshot and birdshot use their own lead-shot portions, while scrapshot uses prepared scrap buckshot. Each portion supplies one shell, regardless of pellet-count settings. All finished shotgun shells weigh 40 g; a six-shell box weighs 240 g.

| Preparation | Output | Location | Base time |
|---|---|---|---:|
| 1 scrap lead | 6 lead buckshot portions | Lit ammunition forge | 60 minutes |
| 1 scrap lead | 12 lead birdshot portions | Lit ammunition forge | 60 minutes |
| 1 scrap metal | 6 scrap buckshot portions | Workbench, simple tools | 30 minutes |

Combine one matching portion (or one Bullet for a slug), one recovered shotgun casing, and 10 g gunpowder to make one shell in five base minutes. Lead shell assembly uses an ammunition workbench. Scrapshot assembly works at an ordinary workbench without an additional tool. Workbench actions require light. Recover spent casings from the ground after reloading; they can be reused.

Three options, off by default, add ordinary-forge bullet casting, ordinary-forge lead-shot casting, and ordinary-workbench lead shotgun shell assembly. They take effect on the next game load and preserve the default recipes. Vanilla rifle and revolver cartridge recipes are unchanged. Scrapshot is crafting-only and uses six slightly weaker pellets with a slightly wider pattern than lead buckshot.

## Finding and restoring shotguns

Usable and ruined shotguns share the existing complete-gun locations. Mod Settings independently control each type's availability, chance, and minimum resource level. Defaults are 50% usable at High or above, and 50% ruined at Medium or above. Blocked chances remain empty: on Medium the default is 50% ruined and 50% empty. Previously visited areas retain their saved finds.

Restore a ruined shotgun at a milling machine with **4 scrap metal and 1 fir firewood**, taking **three base hours**, subject to normal milling-machine requirements and skill rules.

<details>
<summary><b>Spoilers: Geoguessing with a Double Barrel</b></summary>

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

The new ammunition and restoration changes are prepared in source, and runtime C# compilation has passed. Unity prefab authoring/import, asset-package rebuilding, and in-game validation remain pending; the currently embedded package still contains the older content. The current compilation artifact is not a playable build of the new ammunition. See the [ammunition source handoff](unity/SourceArt/README.md) before producing a playable build of these changes.

Start with the [developer router](docs/FEATURES.md) for source locations and validation guidance. [Storage notes](docs/STORAGE-NOTES.md) describe the local partial-clone setup and build-output guard.
