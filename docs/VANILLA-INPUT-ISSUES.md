# Input observations also reproduced with vanilla items

Reported: 2026-11-01.

## Test context and scope

The user reported these behaviors in The Long Dark 2.59, Unity 6000.0.60f1,
MelonLoader 0.7.3, with the current modded installation. They reproduced them
using the vanilla rifle or standard quick-selection wheel as well as Shotgun.
A separate clean, unmodded-game test has not been established. These are reports
of shared behavior in that setup, not proof of an engine or touchpad-driver cause.

No further Shotgun changes to aiming gates, radial selection or touchpad input
are planned for these observations. Existing aim-transition safeguards remain.

## Starting aim while walking

1. Equip the vanilla hunting rifle and start walking.
2. Attempt to begin aiming while still walking: aiming does not start.
3. Stop, begin aiming while stationary, then start walking: aiming can continue.
4. Repeat with the shotgun: the user reports identical behavior.

Workaround: stop briefly to begin aiming, then resume movement. Further changes
to this behavior are intentionally out of scope at the user's request. This
observation is separate from earlier reports of a complete weapon-control lock.

## Touchpad selection in radial menus

1. Open the standard quick-selection wheel and select an entry with a physical
   mouse's left button: selection works.
2. Repeat with a touchpad tap or the touchpad's physical left button: selection
   is not accepted in the reported setup.
3. Hold **T** (the default ammunition-wheel binding) and repeat on a shotgun ammo
   icon: mouse selection works; the same touchpad actions do not.

Workaround: use the physical mouse's left button. Touchpad model, driver and
operating-system gesture settings were not isolated. Do not add a Shotgun-only
input workaround or remap buttons on the strength of this report.
