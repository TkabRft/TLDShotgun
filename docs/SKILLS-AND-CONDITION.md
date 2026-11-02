# Shotgun skill progression and firing wear

code-synced: 2026-11-01

Status: implemented. Native gameplay acceptance of wear, experience, skill
benefits and new-find condition remains pending.

## Skill source

`ShotgunSkill` chooses exactly one source, in this order:

| Available integration / setting | Experience destination | Handling tier | Firing wear |
|---|---|---|---|
| Registered FiligranisSkills Shotgun skill with both delegates | Shotgun | Shotgun level | Rifle reduction table at that level |
| No separate skill; Use Rifle Skill Without Shotgun Skill enabled (default) | Rifle | Rifle level | Rifle reduction table at that level |
| No separate skill; option disabled | None | Previous fixed internal tier 3 | Full base wear |

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

## Condition

The shotgun has its own `DegradeOnUse`, linked through `GearItem.m_DegradeOnUse`.
Its base HP debit is one percent of the item's maximum HP: one condition
percentage point at the current 100 HP maximum.

The existing `vp_FPSShooter.Fire` observer confirms one shell consumed without
jam/misfire and applies wear once, whether the shot hits or misses. A shotgun-only
prefix suppresses native `GearItem.DegradeOnUse` debits so the confirmed-shot path
is the sole firing debit; other gear is unaffected. The native `GearItem.Degrade`
method still handles HP clamping and condition notifications.

For an active skill, reduction comes from the live rifle's five-entry
`m_ConditionDegradeOnUseReduction` array, indexed by the selected skill tier and
bounded to 0–50 percent. Base wear is **1%**, falling to **0.5% at maximum skill**.
Without an active source, wear is 1%, independently of the fixed animation tier.
Two successful barrels debit 2% base or 1% at maximum skill. Empty/refused/jammed/
misfired attempts debit nothing; reaching zero on the first barrel prevents the
automatic second discharge.

The mapped weapon definition now uses `InitialCondition: Random`, matching the
inspected vanilla rifle. Configuration also copies the rifle's start-condition
mode. Neither operation writes current HP or rolls condition again. The game's
normal initialization handles new finds; saved condition and explicitly assigned
condition are retained. Ruined finds keep their existing transient zero-condition
override after initial rolls; see [restoration](RESTORATION.md).

## Validation and acceptance

Native gameplay acceptance remains pending. Check one experience point per
hitting shell across all four loads, skill-source priority, fallback off and
handling changes after level-up or settings confirmation.

Sources: [progression](../Main/ShotgunSkillProgression.cs),
[shot scope and hit hooks](../Main/ShotgunShot.cs),
[condition setup](../Main/ShotgunCondition.cs), [shot observer](../Main/DoubleFire.cs).
