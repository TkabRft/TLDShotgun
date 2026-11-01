# Weapon animation and audio

code-synced: 2026-11-01

## Aim ownership and completion

`ShotgunAim` coordinates existing accepted native aim requests. It owns one
direction at a time, together with the player animation component, gun, shoulder
instance and custom animator. Repeated requests do not resend the same trigger;
a reversal resets the opposite pending trigger. Native aim callbacks still own
all player state/control-mode changes. There is no replacement key-2 handler,
direct state write, fixed aim timeout or animation-speed adjustment.

`AnimationEventProxy` forwards completion through the coordinator. Events require
the current owner/proxy, expected native state and the corresponding raising or
lowering clip. Duplicate, opposite-direction and previous-owner events are
ignored. Completion bookkeeping is cleared before calling native code to allow
safe reentry. The existing `CloseBarrelToAim`/`AimConfirmed` shortcut is retained.

The Melon late-update callback samples the Animator. Only after observing the
expected transition clip can it recover a missed event at a stable `Aim_Idle`
or one of the actual Locomotion states. Blends are not treated as settled poses.
An unobserved transition is not fabricated. Fire/reload, accepted unequip, native
reset, equip/rebind, changed ownership and menu entry clear the old bookkeeping.

**Aim Diagnostics**, off by default, logs requests, accepted/ignored completions,
resets and recovery with native state, control mode and Animator state hashes/time.
It does not log every frame. Both hold and toggle input modes use the game's
existing accepted requests rather than a new mouse-input implementation.

## Validation and game acceptance

The isolated weapon harness exercises the production transition coordinator at
the native Unity/game boundary. It covers repeated/reversed requests, omitted,
opposite and duplicate events, blends, owner replacement, reset, reload-to-aim
and fire/reload interruptions. It does not execute a native Animator.

Game acceptance covers repeated aiming, lowering and weapon cycling. Native
state callbacks remain authoritative; controls, gear IDs and save formats remain.
