using AudioMgr;
using MelonLoader;
using UnityEngine;

namespace Shotgun;

internal static class ShotgunAudio
{
    internal const string Discharge = "GAMEPLAY_DoubleBarrel_Discharge";
    private sealed class PendingClip
    {
        internal AudioClip Clip = null!;
        internal float Started;
        internal bool Finished;
        internal bool Ready;
    }
    private static readonly Dictionary<string, PendingClip> clips = new();
    private static readonly HashSet<string> warnings = new();
    private static ConfigurationReferences? references;

    internal static void Begin(ConfigurationReferences refs)
    {
        // A player-shot component lives on AudioManager's shared persistent parent.
        // Reuse our component across configuration refreshes; never destroy that parent.
        var previousShot = references?.AudioShot;
        Reset();
        references = refs;
        try { refs.ClipManager = AudioMaster.NewClipManager(); }
        catch (Exception e) { Failure("manager", e.Message); }
        if (previousShot != null) refs.AudioShot = previousShot;
        EnsureSource();
    }

    internal static void Reset()
    {
        clips.Clear(); warnings.Clear();
        // Keep the reference for reuse at the next Begin, but perform no polling in menus.
    }

    internal static void Register(string key, AudioClip? clip)
    {
        if (clip == null) { Failure(key, "missing AudioClip"); return; }
        var pending = new PendingClip { Clip = clip, Started = Time.realtimeSinceStartup };
        clips[key] = pending;
        try
        {
            if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
            {
                pending.Finished = true;
                Failure(key, "LoadAudioData refused; state=" + clip.loadState);
            }
            Poll(key, pending);
        }
        catch (Exception e) { pending.Finished = true; Failure(key, e.Message); }
    }

    internal static void Tick()
    {
        foreach (var pair in clips)
        {
            if (pair.Value.Finished) continue;
            try { Poll(pair.Key, pair.Value); }
            catch (Exception e) { pair.Value.Finished = true; Failure(pair.Key, e.Message); }
        }
    }

    private static void Poll(string key, PendingClip pending)
    {
        if (pending.Finished) return;
        var clip = pending.Clip;
        if (clip == null || clip.loadState == AudioDataLoadState.Failed || Time.realtimeSinceStartup - pending.Started > 30f)
        {
            pending.Finished = true;
            Failure(key, clip == null ? "destroyed clip" : "audio data unavailable; state=" + clip.loadState);
            return;
        }
        if (clip.loadState != AudioDataLoadState.Loaded) return;
        pending.Finished = true;
        if (clip.samples <= 0 || clip.frequency <= 0 || clip.channels <= 0 || clip.length <= 0f)
        {
            Failure(key, "loaded clip has no valid samples/duration");
            return;
        }
        if (references?.ClipManager == null) { Failure(key, "missing ClipManager"); return; }
        references.ClipManager.LoadAudioclip(key, clip);
        pending.Ready = true;
        MelonLogger.Msg($"[Shotgun audio] Ready {key}: {clip.samples} samples, {clip.frequency} Hz, {clip.length:F3}s");
    }

    private static AudioSource? EnsureSource()
    {
        if (references == null) return null;
        try
        {
            if (references.AudioShot == null || references.AudioShot._audioSource == null)
                references.AudioShot = AudioMaster.CreatePlayerShot(AudioMaster.SourceType.SFX);
            var source = references.AudioShot._audioSource;
            // First-person mechanical sounds must not require a Unity spatializer plug-in.
            // Volume remains owned by AudioManager's SFX/master settings.
            source.spatialBlend = 0f;
            source.spatialize = false;
            return source;
        }
        catch (Exception e) { Failure("source", e.Message); return null; }
    }

    internal static bool SuccessfulShot(int before, int after, bool jammed, bool misfired)
        => before > 0 && after == before - 1 && !jammed && !misfired;

    internal static void AnimationSound(GameObject emitter, string key)
    {
        // Clip-time-zero discharge events are intentionally superseded by the successful-shot observer.
        if (key == Discharge || emitter != Main.Instance?.Configuration?.Refs?.ModdedAnimator?.gameObject || ShotgunInHands() == null) return;
        Play(key);
    }

    internal static void Play(string key)
    {
        try
        {
            if (!clips.TryGetValue(key, out var pending) || !pending.Ready || pending.Clip == null
                || pending.Clip.loadState != AudioDataLoadState.Loaded)
            {
                Failure(key, "play requested before audio ready; state=" + (pending?.Clip == null ? "missing" : pending.Clip.loadState.ToString()));
                return;
            }
            var source = EnsureSource();
            var listener = AudioMaster.audioListener;
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy
                || listener == null || !listener.enabled || !listener.gameObject.activeInHierarchy)
            {
                Failure(key, $"inactive playback: source={source != null}/{source?.enabled}, listener={listener != null}/{listener?.enabled}");
                return;
            }
            if (source.mute || source.volume <= 0f || AudioListener.pause || AudioListener.volume <= 0f)
                Failure(key, $"muted playback: sourceMute={source.mute}, SFX={source.volume}, listenerPause={AudioListener.pause}, master={AudioListener.volume}");
            source.PlayOneShot(pending.Clip); // Preserve overlapping discharge tails, including both barrels.
        }
        catch (Exception e) { Failure(key, e.Message); }
    }

    internal static void Failure(string key, string reason)
    {
        // One report per resource/reason category rather than one per frame/shot.
        string category = reason.Split(':', ';')[0];
        if (warnings.Add(key + ":" + category)) MelonLogger.Warning($"[Shotgun audio] {key}: {reason}");
    }
}
