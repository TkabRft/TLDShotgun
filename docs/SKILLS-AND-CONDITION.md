# Shotgun skill progression

code-synced: 2026-11-01

Status: implemented. Native gameplay acceptance of experience and skill benefits
remains pending.

## Skill source

`ShotgunSkill` chooses exactly one source, in this order:

| Available integration / setting | Experience destination | Handling tier |
|---|---|---|
| Registered FiligranisSkills Shotgun skill with both delegates | Shotgun | Shotgun level |
| No separate skill; Use Rifle Skill Without Shotgun Skill enabled (default) | Rifle | Rifle level |
| No separate skill; option disabled | None | Previous fixed internal tier 3 |

An integration is available only after successful registration and binding of both
progression delegates. Rifle fallback does not create a new skill, redirect books,
or enable the separate Shotgun research books. Both skill implementations retain
their own existing thresholds and save data. Internal tiers are zero-based 0–4;
player-visible levels are 1–5.

The selected tier feeds the existing shotgun animation speeds and gates, camera
recoil and critical-hit formula. With default speed settings, reload/aim/equip
multipliers are `1 + 0.1 * tier`. Camera kick scales by `1 - tier / 4`.
Critical chance remains the hit region's rifle baseline times `0.3125 * tier`;
pellet eligibility thresholds still apply. This change does not add another rifle
damage/range multiplier on top of ammunition balance.

Parameters refresh on equip/start, an awarded hit, confirmed settings and detected
tier changes during weapon locomotion updates. A replacement Animator receives
parameters even if its tier matches the previous Animator. Disabling Rifle fallback
removes dependence on Rifle level, rather than merely stopping experience gains.

## One experience point per shell

Each shotgun projectile owns a scoped `ShotgunPellets.Shot` during its `vp_Bullet.Start`
resolution. Pellet rays resolve within that scope; slugs retain native resolution.
Both native `BaseAi.ApplyDamage` overloads record positive player damage to an
animal that was alive before the call. Querying a region's bleed timer no longer
counts as a hit. A killing hit counts; a corpse, miss, zero/negative damage or
non-player damage does not.

The scope awards at most one point after successful resolution, even if many
pellets or several animals are hit, or one damage overload calls the other.
Separate barrels are separate shells and may earn two points. There is no
wall-clock throttle. Rifle points use `IncrementPointsAndNotify` with
`AssignOnlyInSandbox`; a separate Shotgun skill uses its guarded delegate.

Postfix and finalizer share idempotent completion. Failed pellet resolution and
native exceptions clear the scope without awarding experience. Nested scopes
restore their parent, and unrelated bullets cannot close a shotgun scope.

## Validation and acceptance

Native gameplay acceptance remains pending. Check one experience point per
hitting shell across all four loads, skill-source priority, fallback off and
handling changes after level-up or settings confirmation.

Sources: [progression](../Main/ShotgunSkillProgression.cs),
[shot scope and hit hooks](../Main/ShotgunShot.cs).
