# Weapon animation and audio

code-synced: 2026-11-01

## Symptoms and evidence

After weapon initialization was repaired, the user confirmed equipping and
reported occasional aiming delay, a rare loss of aiming/firing/weapon cycling
after lowering, and silence for both discharge and reload sounds. The available
log confirms successful initialization; it does not capture the failing aim
sequence. A missed or stale completion event remains a hypothesis for that rare
sequence, not a demonstrated runtime cause.

The original aim patches and clips matched upstream before this change. The
generated package preserves the original controller, clips and audio objects.
Its 479,296-byte audio resource bank matches the template byte-for-byte. Earlier
"Registered audio clip" messages only proved dictionary insertion, not loaded
audio data or audible playback.

The next user test confirmed that all six clips stayed `Unloaded` after an
explicit `LoadAudioData` request and the 30-second deadline. The installed DLL
matched Release. This establishes the clip-data blocker, not the underlying
engine/FSB decoder cause. The current build bypasses that path using the original
PCM16 WAV sources. That structural change alone did not establish audible playback.

The later 2026-11-01 test separated two blockers. The inspected AudioCore 3.1.0
omitted `AssetFileInfo.SetNewData` before writing `globalgamemanagers`; the on-disk
`m_DisableAudio` remained true despite its success message. The user installed
AudioCore 3.1.5, which includes that call, and the flag became false. No additional
AudioCore API calls or compile reference are needed in Shotgun.

After that update, all six PCM clips accepted `SetData`. The falling-shell clip
passed native readback and was audibly confirmed by the user. The other five
were rejected by Shotgun's one-sample duration tolerance: for example, 267638
frames at 48000 Hz calculate to 5.575791667 seconds, while native `clip.length`
reported 5.575 seconds. Their later `state=missing` messages were consequences
of rejection and cleanup, not missing embedded WAVs. Updating AudioCore alone
therefore did not restore every sound. The duration correction is implemented; native game acceptance remains separate from structural checks.

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

## Clip readiness and playback

The six original WAV files are embedded as `ModBuild.Audio.*`, with a logical-ID
and SHA-256 manifest. `EmbeddedAudio.LoadPcm` reads the DLL stream directly and
verifies the hash. The shotgun-specific `Pcm16Wave` parser checks RIFF boundaries,
word padding, fmt/data chunks, PCM16 format, mono/stereo channels, sample/byte rates,
block alignment and complete channel frames. It accepts extra chunks and data
before fmt; it does not assume a 44-byte header. Samples are converted from signed
PCM16 to float by division by 32768, without normalization.

On the main thread, the loader creates a nonstreaming `AudioClip` using frames per
channel, calls `SetData`, and verifies Loaded state, metadata/duration and exact
`GetData` readback. Only then can `ShotgunAudio` register the clip with AudioManager.
Frame count, channels, sample rate and all PCM samples must match exactly.
Duration alone allows `max(0.001, 1.0 / sampleRate) + 0.000001` seconds for native
millisecond reporting and float precision. Metadata failures include actual and
calculated duration, absolute difference and allowed tolerance. This does not
change the decoded waveform, source volume or sample-count checks.
Successful creation logs `PCM ready ... native samples verified`. Clips are cached
by logical ID and content hash and kept across configuration refreshes. Corrupt
input or native failure reports its exact stage, releases a partially created
clip and leaves weapon registration intact.

There is no FSB/Addressables audio fallback, `LoadClipFromFile` call, filesystem
extraction, UnityWebRequest or waiting loop in this PCM path. The original seven
bundle clips/bank are preserved for package compatibility but are not the six
runtime sound sources. The generic workspace WAV/OGG adapter for other profiles
is unchanged. `ShotgunAudio` retains defensive readiness checks/polling, but the
normal PCM path supplies an already loaded and verified clip.

Only this mod's player-shot AudioSource is made two-dimensional:
`spatialBlend=0`, `spatialize=false`. AudioManager continues to control its SFX/master
volume. The shared AudioManager parent/listener and other sources/settings are
not changed. The own source is reused across configuration refreshes and can be
recreated if destroyed. Playback checks clip readiness, source/listener activity
and mute/volume state. Diagnostics do not bypass legitimate mute or pause.

The existing `vp_FPSShooter.Fire` observer plays one discharge for a successful
one-round decrease without jam/misfire, including each successful barrel of a
double shot. It plays no discharge for empty, rejected or misfired shots and does
not depend on pellet count. The original discharge animation events remain in
the preserved clips but are ignored by the proxy to avoid double playback.
Reload sounds retain their animation events, gated to the current shotgun
animator and weapon. `PlayOneShot` preserves overlapping discharge tails.

## HUD artwork

Scrapshot's loaded HUD is a white/alpha 183 x 436 shell silhouette with three
negative-space cut-metal shapes, using the buckshot Sprite parameters. Inventory
renders remain 256 x 256. The BC3 texture is 184 x 436 with one transparent column
on the right; the explicit Sprite rect remains 183 x 436. The first revision's
183-pixel BC3 texture caused Unity's RGBA32 fallback warning in the user log.
The new writer checks block alignment and validates logical Sprite rect bounds.
GUIDs, keys, visible geometry, UV transform and UI layout code are unchanged. The
[source-art guide](../unity/SourceArt/README.md) owns regeneration and validation.

## Validation and game acceptance

The existing isolated harness compiles the production aim, audio and double-fire
implementations with stand-ins only at the native Unity/game boundary. Its 41
assertions cover repeated requests, rapid reversals, omitted/opposite/duplicate
events, blends, owner/shoulder replacement, menu reset, reload shortcut,
fire/reload interruptions, clip loading/failure/deadline, listener/volume checks,
source reuse, bounded diagnostics and single/double/rejected/misfired shots.

The same harness adds 104 PCM checks against the production resource loader/parser:
all six real embedded WAVs and their exact decoded samples/durations, mono/stereo,
extra/odd-sized RIFF chunks and reordered data, damaged files/hashes, Create/SetData/
GetData/readback/metadata failures, partial-clip cleanup, same-version content
updates, reuse and no sound-file extraction. Unity/IL2CPP allocation/data access
are stand-ins; successful readback in this harness is not a native game result.

The duration regression uses all six actual embedded WAVs with native-style
millisecond lengths. The old one-sample predicate rejects five; the corrected
production loader accepts and registers all six after exact PCM readback.
Tests also retain full-precision durations, check the larger one-sample allowance
at low sample rates, reject excessive duration error with detailed diagnostics,
and retain the corrupted-data, metadata and sample-mismatch negative controls.

All 17 pipeline tests pass, including the existing maintenance and real
ModComponent JSON/streaming-loader harnesses. Package checks validate the new HUD
Sprite/alpha, preserved audio bank and event keys, references, recipes and CRC.
The rebuilt DLL's managed audit resolves 212 external types, 687 members, 260
assembly references and 55 Harmony targets against the installed dependencies.
Pre-publication checks resolve 23 actual startup addresses and independently
verify all six PCM resources/manifest hashes against the authored WAVs.
These are compilation and structural checks; they do not execute a native
Animator, establish audibility or demonstrate that the rare input lock is gone.

## Pending game acceptance

Check six PCM-ready messages, audible single/double shots and each reload
sound. Retain checks of volume, pause, scene changes and save/load. The
duration correction preserves aiming, radial selection and input. Gear IDs,
ammo indices and save formats are unchanged; no migration is required.
