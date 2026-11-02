# Skill-based casing recovery research

Reviewed: 2026-11-01. Status: deferred design; this update changes neither extraction
behavior nor animation assets. Inspection covered C# handlers, controller branches,
source `.anim` events and the corresponding clips in the embedded template bundle.
It did not include visual playback of a newly adapted catch animation.

## Current behavior and available clips

`AnimationEventProxy.OnEjectCasing` spawns spent casings at the player's feet,
plays native casing audio and decreases `m_SpentCasingsInClip`.
`OnReturnRound` returns live shells of the selected ammunition type with their
stored condition, then removes those entries from `m_Clip`. Live shells currently
return to inventory at every skill level, regardless of the hand motion.

| Contents / operation | Existing clip or path | Transfer events (seconds) | Current result |
|---|---|---|---|
| Two spent, or one spent with no live shell | `Reload_SlideOutBoth` | `OnEjectCasing(2)` at 0.367; limited by actual spent count | Spent casings on ground |
| One spent, one live; normal reload | `Reload_SlideOutRight` | `OnEjectCasing(1)` at 0.333 | Casing on ground; live shell stays loaded |
| One spent, one live; ammo switch, catch gate met | `Switch_SlideOutRightCatchLeft` | Eject 1 at 0.367; return 1 at 0.467; change type at 0.500 | Casing dropped, live shell returned |
| Same ammo switch, catch gate not met | Slide-out-right followed by live-shell removal | Separate eject and return events | Same destinations as above |
| One live shell to unload | `Switch_TakeOutLeft` | Return 1 at 0.367; change type at 0.380 | Live shell returned |
| Two live shells; slow removal | `Switch_TakeOutRight`, then `Switch_TakeOutLeft` | Return 1 at 0.467, then return 1 in the left clip | Both returned |
| Two live shells; medium removal | `Switch_TakeOutBothMedium` | Return 2 at 1.000; change type at 1.400 | Both returned |
| Two live shells; fast removal | `Switch_TakeOutBothFast` | Return 2 at 0.500; change type at 0.767 | Both returned |

`CanCatchLeftOnAmmoSwitch` currently unlocks at internal tier 3 (level 4).
It selects a mixed-content animation, not a general casing-recovery mechanic:
the spent right casing still drops while the live left shell is caught.

## Feasibility and proposed setting

The mechanic is feasible. A future **Recover shells from skill level** setting
would accept levels **1–5**, default **4**, using the same active Shotgun/Rifle
source as [skill progression](SKILLS-AND-CONDITION.md). Below threshold, extracted
spent and live shells would fall to the ground; at or above threshold they would
be removed/caught into inventory. An ordinary reload must leave any unspent shell
in its barrel. With no active skill source, retain current legacy destinations.

Existing hand-removal motions are candidates for high-level collection of one or
two spent casings. They require adapted explicit event tables: transfer spent
casings rather than live ammunition, omit ammo-switch events during ordinary
reloads and suppress drop sounds. Existing slide-out motions are candidates for
low-level dropping of live shells, with the correct live-shell event and a retained
ammo-switch completion. These adaptations could occupy existing clip slots via
an Animator override rather than generating a new controller graph.

No existing clip is a verified complete solution for catching a mixed spent/live
pair. `Switch_SlideOutRightCatchLeft` explicitly throws the spent casing away.
That case needs a new/adapted mixed extraction motion if the existing two-item
hand-removal motion cannot preserve visibility and hand contact. One- and two-
casing collection also require pose/visibility review; names and events alone do
not prove that a clip visually handles spent shells correctly. Author new motion
where that review fails, using the existing skeleton and Blender workflow.

Simply routing a slide-out event to inventory would leave visibly falling shells
that disappear into the backpack; it does not meet the animation requirement.

## Transfer and audio requirements for future implementation

- Snapshot gun ownership, threshold result and ammunition type for the reload.
  Transfer only at the appropriate animation event, with duplicate-event guards.
- Preserve each live shell's selected Gear ID and stored condition. Spent casings
  must remain casings; no powder, projectile or loaded shell is generated.
- Decrement the corresponding loaded/spent slot only after successful transfer.
  Interrupted/repeated events, reload completion, save/load and weapon changes
  must not duplicate or discard ammunition.
- Update counts and visible barrel contents together. A caught casing has no
  world spawn or falling-shell sound. A dropped live shell uses its proper world
  item and condition.
- Gate both current audio paths: native `PlayCasingAudio` in the handler and
  `GAMEPLAY_DoubleBarrel_ShellDrop_Hard` animation events. Preserve opening,
  extraction, insertion and closing sounds.

Future acceptance must cover 0/1/2 live and spent contents, normal reload versus
ammo switch, all four ammunition types and shell conditions, threshold boundaries,
both skill sources, interruption, duplicate events, save/load and audible/visible
agreement. Implementation and animation authoring are intentionally deferred.
