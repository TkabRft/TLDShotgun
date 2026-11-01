using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace ModBuild;

// Shotgun-specific PCM path. The workspace's generic WAV/OGG adapter is separate.
// Resources stay in the DLL; neither files nor FSB decoding are used here.
internal static class EmbeddedAudio
{
    internal sealed class Sound
    {
        public string Id { get; set; } = "";
        public string File { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string Resource { get; set; } = "";
    }

    internal sealed class Manifest
    {
        public string Owner { get; set; } = "";
        public Sound[] Sounds { get; set; } = Array.Empty<Sound>();
    }

    private static Manifest? manifest;
    private static readonly Dictionary<string, AudioClip> clips = new();

    internal static AudioClip LoadPcm(string id)
    {
        Assembly assembly = typeof(EmbeddedAudio).Assembly;
        if (manifest == null)
        {
            using var source = assembly.GetManifestResourceStream("ModBuild.Audio.Manifest.json")
                ?? throw new InvalidDataException("PCM manifest: missing embedded audio manifest");
            var loaded = JsonSerializer.Deserialize<Manifest>(source)
                ?? throw new InvalidDataException("PCM manifest: invalid embedded audio manifest");
            if (loaded.Sounds == null || loaded.Sounds.Any(s => s == null || string.IsNullOrWhiteSpace(s.Id)
                    || string.IsNullOrWhiteSpace(s.Resource) || string.IsNullOrWhiteSpace(s.File) || string.IsNullOrWhiteSpace(s.Sha256))
                || loaded.Sounds.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != loaded.Sounds.Length
                || loaded.Sounds.Select(s => s.Resource).Distinct(StringComparer.Ordinal).Count() != loaded.Sounds.Length)
                throw new InvalidDataException("PCM manifest: missing or duplicate sound identities/resources");
            manifest = loaded;
        }
        Sound sound = manifest.Sounds.SingleOrDefault(item => item.Id == id)
            ?? throw new InvalidDataException("PCM manifest: missing required sound " + id);
        if (!sound.Resource.StartsWith("ModBuild.Audio.", StringComparison.Ordinal)
            || !sound.File.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("PCM manifest: expected embedded WAV for " + id);
        using var stream = assembly.GetManifestResourceStream(sound.Resource)
            ?? throw new InvalidDataException("PCM resource: missing " + sound.Resource);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return LoadVerifiedPcm(id, buffer.ToArray(), sound.Sha256);
    }

    // Production entry point also used by the isolated corrupt-data/native-boundary tests.
    internal static AudioClip LoadVerifiedPcm(string id, byte[] bytes, string expectedHash)
    {
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("PCM hash: SHA-256 mismatch for " + id);
        string cacheKey = id + "/" + hash;
        if (clips.TryGetValue(cacheKey, out var cached) && cached != null && cached.loadState == AudioDataLoadState.Loaded)
            return cached;

        string stage = "RIFF decode";
        AudioClip? clip = null;
        try
        {
            var wave = Pcm16Wave.Decode(bytes);
            stage = "AudioClip.Create";
            clip = AudioClip.Create(id, wave.Frames, wave.Channels, wave.SampleRate, false);
            if (clip == null) throw new InvalidOperationException("returned null");
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            stage = "SetData";
            var samples = new Il2CppStructArray<float>(wave.Samples);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("returned false");
            stage = "clip metadata";
            double expectedDuration = (double)wave.Frames / wave.SampleRate;
            // The native backend can report milliseconds even when frame/sample counts are exact.
            double durationTolerance = Math.Max(0.001, 1.0 / wave.SampleRate) + 0.000001;
            double durationDifference = Math.Abs(clip.length - expectedDuration);
            if (clip.loadState != AudioDataLoadState.Loaded || clip.samples != wave.Frames
                || clip.channels != wave.Channels || clip.frequency != wave.SampleRate
                || durationDifference > durationTolerance)
                throw new InvalidOperationException($"unexpected state={clip.loadState}, frames={clip.samples}, channels={clip.channels}, rate={clip.frequency}, duration={clip.length:R}, expectedDuration={expectedDuration:R}, durationDifference={durationDifference:R}, durationTolerance={durationTolerance:R}");
            stage = "GetData readback";
            var readback = new Il2CppStructArray<float>(wave.Samples.Length);
            if (!clip.GetData(readback, 0)) throw new InvalidOperationException("returned false");
            if (!wave.Samples.AsSpan().SequenceEqual(readback.ToArray()))
                throw new InvalidOperationException("samples differ from original PCM16");
            clips[cacheKey] = clip;
            MelonLogger.Msg($"[Shotgun audio] PCM ready {id}: {wave.Frames} frames/channel, {wave.Channels} channels, {wave.SampleRate} Hz; native samples verified");
            return clip;
        }
        catch (Exception e)
        {
            if (clip != null) UnityEngine.Object.Destroy(clip);
            throw new InvalidOperationException($"PCM {id} / {stage}: {e.Message}", e);
        }
    }
}
