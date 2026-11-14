# Wildlife damage scaling research

Research and implementation date: 2026-11-06. Status: **implemented; game acceptance pending**. The shared Shotgun wildlife policy is consumed by standalone resolution and the updated Adaptive Arsenal adapter. Pre-rebalance asset observations and damage tables are retained below as a dated baseline.

## Finding

The revolver's lower damage is represented by weapon-specific **body-region scales**, not just a smaller weapon damage value or one penalty for large animals. The relevant contract is `LocalizedDamage.GetDamageScale(WeaponSource)`, backed by `BodyDamage.m_DamageRegions`. Each `DamageRegion` has both `m_DamageScale` and `m_DamageScaleForRevolver`.

Both resolvers first obtain the generic region scale, then apply the selected load's wildlife policy after the mode-specific distance/minimum calculation. Standalone slugs retain native `vp_Bullet.Start`; scoped damage-scale and bleed-time hooks explicitly select the generic scale and half its positive timer. Their native endpoint queries use an exception-safe recursion bypass. Native game execution remains a separate acceptance check.

`Main/ShotgunWildlifePolicy.cs` owns immutable shell policy and cumulative per-target damage/bleeding/critical accounting. The adapter binds its versioned operations once, without a compile dependency or per-hit reflection. The implemented balance uses current effective pellet damage as its baseline; it does not add revolver scales on top of the existing low pellet inputs.

## Verified vanilla asset data

The following are **serialized prefab values from the installed game**, read on the research date. They are not live damage measurements or a guarantee for every game revision. The pinned reference package establishes API availability, not the installed game's asset version.

Each cell is **generic / revolver**. Region names are the game's `BodyPart` names; notably, `neck` is distinct from `torso`.

| Prefab | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Normal bear | 3 / 0.7 | 2 / 0.5 | 1 / 0.45 | 0.5 / 0.2 | 0.5 / 0.2 |
| Aurora bear | 3 / 2.25 | 2 / 1.5 | 1 / 0.75 | 0.5 / 0.375 | 0.5 / 0.375 |
| Moose | 2 / 0.5 | 3 / 0.4 | 1.1 / 0.3 | 1 / 0.25 | 1 / 0.25 |
| Normal wolf, `WILDLIFE_Wolf_grey` and `WILDLIFE_Wolf_grey_aurora` | 10 / 0.8 | 5 / 0.9 | 1 / 0.65 | 0.25 / 0.15 | 0.25 / 0.15 |
| `WILDLIFE_Wolf_aurora` | 10 / 0.9 | 5 / 0.9 | 1 / 1 | 0.25 / 0.25 | 0.25 / 0.25 |
| Stag and doe | 10 / 7.5 | 1 / 0.75 | 0.75 / 0.5625 | 0.5 / 0.375 | 0.33 / 0.2475 |
| Cougar, normal and Aurora | 1 / 0.6 | 0.5 / 0.35 | 0.75 / 0.35 | 0.5 / 0.2 | 0.25 / 0.2 |
| Rabbit and ptarmigan | 1 / 1 | 1 / 1 | 1 / 1 | 1 / 1 | 1 / 1 |

Consequences:

- Normal bear head damage uses about 23.3% of the generic scale, while its torso uses 45%. Moose neck damage uses about 13.3%, while its torso uses about 27.3%. A single species multiplier cannot reproduce both regions.
- Normal wolf head damage uses only 8% of the generic scale. The difference is not restricted to bears and moose. Deer use 75% across their recorded regions; rabbit and ptarmigan have no damage-scale reduction.
- Aurora values must be queried from the actual struck region. Do not hardcode one bear/wolf table for all variants.
- `WILDLIFE_Timberwolf_NavMeshAgent` has no `BodyDamage` in the inspected hierarchy. The later direct-reference check resolves `BaseAi.m_NavMeshAgentPrefab` on both `WILDLIFE_Wolf_grey` and `WILDLIFE_Wolf_grey_aurora` to that navigation prefab. Use the grey-wolf model prefabs' own `BodyDamage`: both have normal-wolf damage scales, with 150 and 225 prefab HP respectively. The ordinary Aurora-wolf prefab instead has its own revolver scales and 150 HP. Live spawned values remain a game check.
- Story/challenge bear prefabs in the bear bundle contain zero damage scales. They are excluded from the survival table. Preserve legitimate zero scales and native special handling rather than substituting 1 or dividing by zero.

The base assets are `Assets/Prefabs/Wildlife/WILDLIFE_Bear.prefab`, `WILDLIFE_Bear_aurora.prefab`, `WILDLIFE_Moose.prefab`, `WILDLIFE_Wolf.prefab`, `WILDLIFE_Wolf_grey.prefab`, `WILDLIFE_Wolf_grey_aurora.prefab`, `WILDLIFE_Wolf_aurora.prefab`, `WILDLIFE_Wolf_town.prefab`, `WILDLIFE_Stag.prefab`, `WILDLIFE_Doe.prefab` and `WILDLIFE_Rabbit.prefab`. DLC assets are `Assets/Prefabs/Wildlife/WILDLIFE_CougarA.prefab`, `WILDLIFE_CougarA_Aurora.prefab` and `WILDLIFE_Ptarmigan.prefab`. Inspection followed catalog entries to the selected bundles and each prefab's own component hierarchy, avoiding unrelated story-bear data in the same bundle.

## Pre-rebalance firearm damage tables (2026-11-06)

Historical baseline cached before the 2026-11-06 wildlife rebalance, from selected installed wildlife prefabs and the then-current Shotgun defaults. These tables cover the standard rifle/revolver and all four Shotgun loads against huntable survival wildlife, including Aurora variants. They do not cover arrows, flare rounds, decorative wildlife or special story/challenge actors.

Values are **ordinary damage per single projectile**, rounded to two decimals, before distance falloff, critical kills, bleeding and skill damage bonuses. They are source/asset calculations, not measured live HP changes, and do not apply the proposed revolver-scale policy. HP in each caption is serialized prefab health, not a guarantee of every spawned animal's current health.

**Buckshot, birdshot and scrapshot rows mean one pellet.** For a complete shell whose pellets all strike that same part, multiply by **9, 30 and 6** respectively. Mixed-region hits are summed individually; both barrels are two separate shells. Rifle and revolver rows refer to standard weapons, not named variants.

**Slug*** uses the generic region scale confirmed in Adaptive Arsenal's custom resolver. Standalone native slug source selection remains unverified. The pellet regional scales are the same with and without Arsenal, but Arsenal's distance curve changes actual damage even inside the standalone loads' nominal effective range. These tables deliberately omit distance loss.

Columns use the game's distinct regions: **Head, Neck, Torso, Hips, Limb**. Rifle/slug/pellet rows use the generic scale; revolver rows use the revolver-specific scale. The checked Timberwolf and Aurora Timberwolf prefabs link directly to `WILDLIFE_Timberwolf_NavMeshAgent`; both retain normal-wolf damage scales, while ordinary Aurora wolves differ. `WILDLIFE_Wolf_town` has the Aurora-wolf damage table but 100 prefab HP; it is a separate special wolf prefab, not the normal-wolf baseline.

### Deer (stag and doe) — 100 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 100 | 75 | 50 | 33 |
| Revolver round | 750 | 75 | 56.25 | 37.5 | 24.75 |
| Slug* | 1200 | 120 | 90 | 60 | 39.6 |
| Buckshot pellet | 150 | 15 | 11.25 | 7.5 | 4.95 |
| Birdshot pellet | 30 | 3 | 2.25 | 1.5 | 0.99 |
| Scrapshot pellet | 130 | 13 | 9.75 | 6.5 | 4.29 |

### Wolf — 100 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 500 | 100 | 25 | 25 |
| Revolver round | 80 | 90 | 65 | 15 | 15 |
| Slug* | 1200 | 600 | 120 | 30 | 30 |
| Buckshot pellet | 150 | 75 | 15 | 3.75 | 3.75 |
| Birdshot pellet | 30 | 15 | 3 | 0.75 | 0.75 |
| Scrapshot pellet | 130 | 65 | 13 | 3.25 | 3.25 |

### Aurora wolf — 150 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 500 | 100 | 25 | 25 |
| Revolver round | 90 | 90 | 100 | 25 | 25 |
| Slug* | 1200 | 600 | 120 | 30 | 30 |
| Buckshot pellet | 150 | 75 | 15 | 3.75 | 3.75 |
| Birdshot pellet | 30 | 15 | 3 | 0.75 | 0.75 |
| Scrapshot pellet | 130 | 65 | 13 | 3.25 | 3.25 |

### Timberwolf — 150 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 500 | 100 | 25 | 25 |
| Revolver round | 80 | 90 | 65 | 15 | 15 |
| Slug* | 1200 | 600 | 120 | 30 | 30 |
| Buckshot pellet | 150 | 75 | 15 | 3.75 | 3.75 |
| Birdshot pellet | 30 | 15 | 3 | 0.75 | 0.75 |
| Scrapshot pellet | 130 | 65 | 13 | 3.25 | 3.25 |

### Aurora Timberwolf — 225 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 500 | 100 | 25 | 25 |
| Revolver round | 80 | 90 | 65 | 15 | 15 |
| Slug* | 1200 | 600 | 120 | 30 | 30 |
| Buckshot pellet | 150 | 75 | 15 | 3.75 | 3.75 |
| Birdshot pellet | 30 | 15 | 3 | 0.75 | 0.75 |
| Scrapshot pellet | 130 | 65 | 13 | 3.25 | 3.25 |

### Bear — 500 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 300 | 200 | 100 | 50 | 50 |
| Revolver round | 70 | 50 | 45 | 20 | 20 |
| Slug* | 360 | 240 | 120 | 60 | 60 |
| Buckshot pellet | 45 | 30 | 15 | 7.5 | 7.5 |
| Birdshot pellet | 9 | 6 | 3 | 1.5 | 1.5 |
| Scrapshot pellet | 39 | 26 | 13 | 6.5 | 6.5 |

### Aurora bear — 750 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 300 | 200 | 100 | 50 | 50 |
| Revolver round | 225 | 150 | 75 | 37.5 | 37.5 |
| Slug* | 360 | 240 | 120 | 60 | 60 |
| Buckshot pellet | 45 | 30 | 15 | 7.5 | 7.5 |
| Birdshot pellet | 9 | 6 | 3 | 1.5 | 1.5 |
| Scrapshot pellet | 39 | 26 | 13 | 6.5 | 6.5 |

### Moose — 400 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 200 | 300 | 110 | 100 | 100 |
| Revolver round | 50 | 40 | 30 | 25 | 25 |
| Slug* | 240 | 360 | 132 | 120 | 120 |
| Buckshot pellet | 30 | 45 | 16.5 | 15 | 15 |
| Birdshot pellet | 6 | 9 | 3.3 | 3 | 3 |
| Scrapshot pellet | 26 | 39 | 14.3 | 13 | 13 |

### Cougar (normal and Aurora) — 300 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 100 | 50 | 75 | 50 | 25 |
| Revolver round | 60 | 35 | 35 | 20 | 20 |
| Slug* | 120 | 60 | 90 | 60 | 30 |
| Buckshot pellet | 15 | 7.5 | 11.25 | 7.5 | 3.75 |
| Birdshot pellet | 3 | 1.5 | 2.25 | 1.5 | 0.75 |
| Scrapshot pellet | 13 | 6.5 | 9.75 | 6.5 | 3.25 |

### Rabbit — 25 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 100 | 100 | 100 | 100 | 100 |
| Revolver round | 100 | 100 | 100 | 100 | 100 |
| Slug* | 120 | 120 | 120 | 120 | 120 |
| Buckshot pellet | 15 | 15 | 15 | 15 | 15 |
| Birdshot pellet | 3 | 3 | 3 | 3 | 3 |
| Scrapshot pellet | 13 | 13 | 13 | 13 | 13 |

### Ptarmigan — 25 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 100 | 100 | 100 | 100 | 100 |
| Revolver round | 100 | 100 | 100 | 100 | 100 |
| Slug* | 120 | 120 | 120 | 120 | 120 |
| Buckshot pellet | 15 | 15 | 15 | 15 | 15 |
| Birdshot pellet | 3 | 3 | 3 | 3 | 3 |
| Scrapshot pellet | 13 | 13 | 13 | 13 | 13 |

## Current rebalance damage tables (2026-11-06)

Current defaults, per single projectile, before distance loss, critical kills, bleeding or skill damage bonuses. Pellet rows mean one pellet; complete same-region shells use 9 buckshot, 30 birdshot or 6 scrapshot pellets. Small-game pellet entries assume the observed full 25 HP; runtime damage is sufficient for the living target's actual positive HP. Slug rows now reflect the explicit generic-scale policy in both resolvers; native execution still needs game acceptance. Standard rifle/revolver references are unchanged.

### Deer (stag and doe) ? 100 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 100 | 75 | 50 | 33 |
| Revolver round | 750 | 75 | 56.25 | 37.5 | 24.75 |
| Slug | 1500 | 150 | 112.5 | 75 | 49.5 |
| Buckshot pellet | 150 | 15 | 11.25 | 7.5 | 4.95 |
| Birdshot pellet | 6.75 | 0.3 | 0.23 | 0.15 | 0.1 |
| Scrapshot pellet | 130 | 13 | 9.75 | 6.5 | 4.29 |

### Wolf ? 100 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 500 | 100 | 25 | 25 |
| Revolver round | 80 | 90 | 65 | 15 | 15 |
| Slug | 1500 | 750 | 150 | 37.5 | 37.5 |
| Buckshot pellet | 150 | 75 | 15 | 3.75 | 3.75 |
| Birdshot pellet | 6.75 | 1.5 | 0.3 | 0.07 | 0.07 |
| Scrapshot pellet | 130 | 65 | 13 | 3.25 | 3.25 |

### Aurora wolf ? 150 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 500 | 100 | 25 | 25 |
| Revolver round | 90 | 90 | 100 | 25 | 25 |
| Slug | 1500 | 750 | 150 | 37.5 | 37.5 |
| Buckshot pellet | 150 | 75 | 15 | 3.75 | 3.75 |
| Birdshot pellet | 6.75 | 1.5 | 0.3 | 0.07 | 0.07 |
| Scrapshot pellet | 130 | 65 | 13 | 3.25 | 3.25 |

### Timberwolf ? 150 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 500 | 100 | 25 | 25 |
| Revolver round | 80 | 90 | 65 | 15 | 15 |
| Slug | 1500 | 750 | 150 | 37.5 | 37.5 |
| Buckshot pellet | 150 | 75 | 15 | 3.75 | 3.75 |
| Birdshot pellet | 6.75 | 1.5 | 0.3 | 0.07 | 0.07 |
| Scrapshot pellet | 130 | 65 | 13 | 3.25 | 3.25 |

### Aurora Timberwolf ? 225 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 1000 | 500 | 100 | 25 | 25 |
| Revolver round | 80 | 90 | 65 | 15 | 15 |
| Slug | 1500 | 750 | 150 | 37.5 | 37.5 |
| Buckshot pellet | 150 | 75 | 15 | 3.75 | 3.75 |
| Birdshot pellet | 6.75 | 1.5 | 0.3 | 0.07 | 0.07 |
| Scrapshot pellet | 130 | 65 | 13 | 3.25 | 3.25 |

### Bear ? 500 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 300 | 200 | 100 | 50 | 50 |
| Revolver round | 70 | 50 | 45 | 20 | 20 |
| Slug | 450 | 300 | 150 | 75 | 75 |
| Buckshot pellet | 45 | 30 | 15 | 7.5 | 7.5 |
| Birdshot pellet | 0.18 | 0.12 | 0.06 | 0.03 | 0.03 |
| Scrapshot pellet | 39 | 26 | 13 | 6.5 | 6.5 |

### Aurora bear ? 750 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 300 | 200 | 100 | 50 | 50 |
| Revolver round | 225 | 150 | 75 | 37.5 | 37.5 |
| Slug | 450 | 300 | 150 | 75 | 75 |
| Buckshot pellet | 45 | 30 | 15 | 7.5 | 7.5 |
| Birdshot pellet | 0.18 | 0.12 | 0.06 | 0.03 | 0.03 |
| Scrapshot pellet | 39 | 26 | 13 | 6.5 | 6.5 |

### Moose ? 400 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 200 | 300 | 110 | 100 | 100 |
| Revolver round | 50 | 40 | 30 | 25 | 25 |
| Slug | 300 | 450 | 165 | 150 | 150 |
| Buckshot pellet | 12 | 18 | 6.6 | 6 | 6 |
| Birdshot pellet | 0.12 | 0.18 | 0.07 | 0.06 | 0.06 |
| Scrapshot pellet | 10.4 | 15.6 | 5.72 | 5.2 | 5.2 |

### Cougar (normal and Aurora) ? 300 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 100 | 50 | 75 | 50 | 25 |
| Revolver round | 60 | 35 | 35 | 20 | 20 |
| Slug | 150 | 75 | 112.5 | 75 | 37.5 |
| Buckshot pellet | 15 | 7.5 | 11.25 | 7.5 | 3.75 |
| Birdshot pellet | 0.06 | 0.03 | 0.04 | 0.03 | 0.01 |
| Scrapshot pellet | 13 | 6.5 | 9.75 | 6.5 | 3.25 |

### Rabbit ? 25 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 100 | 100 | 100 | 100 | 100 |
| Revolver round | 100 | 100 | 100 | 100 | 100 |
| Slug | 150 | 150 | 150 | 150 | 150 |
| Buckshot pellet | 25 | 25 | 25 | 25 | 25 |
| Birdshot pellet | 25 | 25 | 25 | 25 | 25 |
| Scrapshot pellet | 25 | 25 | 25 | 25 | 25 |

### Ptarmigan ? 25 prefab HP

| Projectile | Head | Neck | Torso | Hips | Limb |
|---|---:|---:|---:|---:|---:|
| Rifle round | 100 | 100 | 100 | 100 | 100 |
| Revolver round | 100 | 100 | 100 | 100 | 100 |
| Slug | 150 | 150 | 150 | 150 | 150 |
| Buckshot pellet | 25 | 25 | 25 | 25 | 25 |
| Birdshot pellet | 25 | 25 | 25 | 25 | 25 |
| Scrapshot pellet | 25 | 25 | 25 | 25 | 25 |

### Pre-rebalance effective damage comparison

This subsection preserves the earlier baseline analysis and rejected suggestions. Its references to current tuning mean the pre-rebalance tuning, not the implemented policy below.

The standard revolver prefab was re-read alongside its bullet prefab and the selected wildlife prefabs. `GEAR_Revolver` declares `m_DamageHP = 100` and a 30 m accuracy range. `RevolverBullet.prefab` separately declares a default `Damage = 1`, falloff 0.5 and minimum 1. Do not treat a standalone bullet-prefab default as its final firing-time damage: the weapon and firing path supply projectile parameters. Arsenal's native-weapon path explicitly captures the gun's `m_DamageHP`; Shotgun's `Actions.UpdateShotgunParams` supplies the selected load's damage, and Arsenal's adapter captures that load value directly.

For comparing ordinary close-range damage, the meaningful quantities are therefore **weapon/load input multiplied by the corresponding region scale**, not the inputs 100, 15 and 13 in isolation:

```text
standard revolver round: 100 * revolver region scale
current buckshot pellet: 15 * generic region scale
current scrapshot pellet: 13 * generic region scale
```

These are source/asset-derived values before falloff, criticals and native damage application, not measured live HP changes. They describe source defaults, not a user's saved settings.

| Animal / region | One revolver round | One current buckshot pellet | One current scrapshot pellet |
|---|---:|---:|---:|
| Bear head | 70 | 45 | 39 |
| Bear torso | 45 | 15 | 13 |
| Moose head | 50 | 30 | 26 |
| Moose neck | 40 | 45 | 39 |
| Moose torso | 30 | 16.5 | 14.3 |
| Cougar head | 60 | 15 | 13 |
| Cougar torso | 35 | 11.25 | 9.75 |
| Wolf head | 80 | 150 | 130 |
| Wolf torso | 65 | 15 | 13 |
| Deer head | 750 | 150 | 130 |
| Deer torso | 56.25 | 11.25 | 9.75 |

Current buckshot/scrapshot head pellets are already approximately 64%/56% of a revolver hit against a normal bear and 60%/52% against moose. Against a moose neck they are approximately 113%/98%. Against a wolf head they exceed a revolver round because the generic head scale is 10 rather than 0.8. There is no universal pellet-to-revolver damage ratio.

Re-read prefab health is bear 500, moose 400, cougar 300, wolf 100 and stag 100. Current complete nine-pellet buckshot patterns consequently give 405 bear-head damage, 270 moose-head damage, **405 moose-neck damage**, and 135 cougar-head damage. For six-pellet scrapshot, the corresponding values are 234, 156, 234 and 78. These ordinary-damage ceilings already favor deer/wolf hunting and discriminate between the larger targets. Moose-neck scaling is a notable exception to a uniformly weak moose role: nine close-range neck hits can exceed the inspected 400 HP without a critical.

Literal **one effective revolver hit per pellet** would replace `15 * G` with `100 * R`. This is not a uniform 6.67-fold increase: it would increase bear-head damage by approximately 56%, moose-head damage by 67%, moose-torso damage by 82%, and cougar-head damage fourfold, while reducing wolf-head damage by approximately 47%. A full pattern would reach 630 bear-head, 450 moose-head and 540 cougar-head damage. Thus exact equivalence is a coherent interpretation, but its stronger moose/cougar outcomes do not match the proposed progression without further constraints.

Conversely, **retaining 15/13 and merely selecting revolver scales** applies a second, often severe reduction. The previous chat suggestion of 25-damage buckshot pellets with revolver scales is also not a neutral adjustment: it changes a complete bear-head pattern from 405 to 157.5, a moose-neck pattern from 405 to 90, and a cougar-head pattern from 135 to 135. That numerical suggestion is withdrawn as an established recommendation; it requires a deliberate choice of desired outcomes and comparative game testing.

For the requested deer/wolf > bear > moose/cougar roles, use the current effective damage as the baseline. Identify specific excessive outcomes, especially moose-neck damage and per-shell criticals, before applying a global revolver profile or raising per-pellet base values. Preserve the existing 13-versus-15 and six-versus-nine scrapshot tradeoff unless intentionally rebalancing it. Any proposed policy must be checked with Arsenal's distance curve as well as standalone falloff, rather than treating these close-range ceilings as every-range performance.

### Damage is separate from instant kills and bleeding

`DamageRegion` also stores weapon-specific critical chances and bleed timers. The differences are independent: the normal wolf head has generic/revolver critical values of 0/40 despite its much lower revolver damage scale. The bear head has 15/3. These are region data, before the caller's skill rules; they are not guaranteed final critical probabilities.

Therefore, changing the weapon identity globally is broader than a damage adjustment. It can also change bleeding, criticals and animal reactions. Preserve Shotgun's current per-shell pellet thresholds, rifle-based adjusted critical chance, critical immunity and native no-bleed handling unless those mechanics are deliberately rebalanced. A strong damage reduction alone does not prevent a qualifying critical kill or eventual bleed-out. In particular, using revolver damage values does not make birdshot harmless to large wildlife.

## Implemented policy and source ownership

- [Shared policy](../Main/ShotgunWildlifePolicy.cs) captures load, critical tier and hit thresholds per shell. A target state owns cumulative hit counts, strongest adjusted-damage region, shortest positive bleed timer and consumed probability gates. Only new batch damage is returned. Animals are classified through typed components and `AiSubType`, including the dedicated ptarmigan component; unknown types and special hunting/story controllers retain legacy policy.
- [Standalone pellets](../Main/ShotgunPellets.cs) calculate generic regional damage, subtract falloff and clamp its minimum before calling the policy. [Shot scopes](../Main/ShotgunShot.cs) preserve parent scopes and expose the deliberately new reference-type `out ShotResolutionState` contract. [Slug hooks](../Main/ShotgunSlugPolicy.cs) adjust only active native slug queries.
- [Arsenal binding](../../TLD-adaptive-arsenal/src/Compatibility/ShotgunWildlifeBinding.cs) verifies policy contract version 1 and compiles cached capture/target delegates. [Its resolver](../../TLD-adaptive-arsenal/src/Compatibility/ShotgunHitResolver.cs) applies Arsenal distance loss and the generic region scale, then delegates to the same target policy. The public Arsenal projectile API stays at version 1. Older adapters cannot bind the new out-state signature; unsupported pairings warn and retain standalone resolution, while faults after activation keep the existing firing block.

| Load | Damage policy | Bleeding | Critical policy |
|---|---|---|---|
| Slug | Default 150; explicit generic scale; range 30 m unchanged | Half the positive native generic timer, one hit | Rifle region chance ? captured tier ? 0.3125 |
| Buckshot | Existing 15 ? 9; small-game lethal hit; moose ? 0.4 on every region | Three hits; native timer | Six in-range hits; moose chance ? 0.1, cougar ? 0.25 |
| Scrapshot | Existing 13 ? 6; same animal rules as buckshot | Three hits; native timer ? 1.5 | Six in-range hits; same animal factors |
| Birdshot | Existing 3 ? 30 input; deer/wolf head ? 0.225, other regions ? 0.1; bear/moose/cougar ? 0.02; small-game lethal hit | Deer/wolves only: 15 hits, one 15% roll, max(240 minutes, native timer ? 4) | Disabled for recognized wildlife |

The pellet small-game rule supplies at least the living rabbit/ptarmigan's current positive finite HP through ordinary `ApplyDamage`; it does not require a critical or bleed, and applies after falloff. Native no-bleed/critical immunity remains protected. No policy damage reduction is followed by another minimum clamp. Chance modifiers apply to buckshot/scrapshot only; slug critical chances retain the captured skill formula. Zero ordinary-damage patterns cannot acquire critical kills.

Birdshot uses one bleeding trial per animal per shell, once its hit threshold and a positive native timer are available. A failure cannot retry. A successful trial can use a later shorter eligible timer but never a longer one. Separate animals and barrels own separate trials. At defaults, deer/wolf head pellets deal 6.75 before falloff: fifteen exceed 100 HP, twenty-three exceed 150 HP, and thirty total 202.5, below an Aurora Timberwolf's 225 HP. Against a bear or moose the maximum complete same-part birdshot pattern is about 5.4 damage; against cougar, 1.8. It cannot bleed those three groups.

Slug damage is 450 per bear head before falloff. Two landed headshots total 900, or 851.4 with Arsenal's default distance curve at 30 m, against the observed 750-HP Aurora bear. This is the authored ordinary-damage target at all skill levels, conditional on both head hits, normal configuration and the recorded animal health; it is not proof of live game behavior.

`Settings/ShotgunBalanceDefaults.cs` owns the one-time settings revision. After settings load, every prior slug-damage value, including custom values, resets to 150 once; Save/Reload updates the confirmed snapshot so Cancel cannot restore the old value. Later custom slug settings persist. Other numeric settings and game-save/item identifiers are preserved. The revision and retained legacy birdshot-critical field are hidden. The birdshot bleeding-threshold control describes its probabilistic deer/wolf role.

The previously considered generic/revolver interpolation and blanket 25-damage-plus-revolver-scale suggestion were not selected. Counts, cones, speeds, drag, falloff, wear, skill ownership and per-shell experience rules are unchanged.

## API verification and limits

Narrow metadata inspection confirmed these types/members in Shotgun's **pinned 2.51.0** reference assembly and the installed interop assembly:

- `LocalizedDamage.GetDamageScale(WeaponSource) -> float`, `GetBleedOutMinutes(WeaponSource) -> float`, `GetChanceKill(WeaponSource) -> int`, `RollChanceToKill(WeaponSource) -> bool` and `HasBodyDamage() -> bool`.
- `LocalizedDamage.m_BodyPart`, `LocalizedDamage.m_BodyDamage`, `BodyDamage.m_DamageRegions`, and the generic/revolver fields on `DamageRegion`.
- `WeaponSource.Unspecified = 0`, `Revolver = 4`, `Rifle = 5`; `BodyPart.head = 0`, `torso = 1`, `hips = 2`, `limb = 3`, `neck = 4`.

Reference/stub or interop wrappers were **not** treated as native implementation bodies. Runtime initialization, the native execution of the scoped slug hooks, special modes, other damage mods and live regional overrides remain game checks. Arsenal declares a different game-reference package; its compatibility is based here on the checked local source and common verified runtime members, not on assuming its pin is Shotgun's pin.

The [upstream Arsenal repository](https://github.com/Deaadman/adaptive-arsenal) documents its projectile replacement and damage drop-off. Its public version differs from this workspace's extended adapter; the local source above is the evidence for the current integration. Public search did not establish a reliable current body-region table, so the numerical findings above come from selected installed assets rather than community damage estimates.

## Validation and remaining game checks

The production-source Harmony adapter/runtime/policy harness passes 5,155 checks, including the captured source/tier regressions, all inspected animal/region/load combinations, typed ptarmigan handling, small-game kills after falloff, thresholds, birdshot's one-shot probability gate and shortening timer, uniform moose reductions, critical factors and immunity, zero-damage isolation, streaming batches, policy-callback fault blocking without ammo loss or hitscan replay, and native slug recursion/exception cleanup. Native/Unity objects remain stand-ins. The real settings initialization and migration harness passes 40 checks for forced custom/default slug resets, unrelated values, hidden fields, Cancel and later customization. The existing focused weapon/audio/progression regression, the 80-check production projectile-component harness and Arsenal's 189 portable tests pass.

After final input validation, normal full-build gates verify the embedded asset package, API references and fresh local Release snapshots. Build and package success do not establish game acceptance; no build installs or launches the game.

Pending game acceptance: all loads with Arsenal absent, active, integration off and real bullets off; one-pellet rabbit/ptarmigan kills; close-range birdshot head/body patterns, repeated probabilistic bleeding and large-animal resistance; buckshot/scrapshot moose neck/head/torso and cougar criticals; normal/Aurora wolf, Timberwolf and bear variants; two landed slug heads on an Aurora bear at 30 m, native generic scale and half bleed timers; both barrels, skills, ammo changes, scene teardown and corpses. Record requested damage, HP changes, hit regions, critical results, bleed timers, game/mod versions and settings. Existing saves retain their items and persistence contracts; only future hits use the new policy.

code-synced: 2026-11-06
