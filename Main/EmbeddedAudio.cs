using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AudioMgr;
using MelonLoader.Utils;

namespace ModBuild;

// Shared source adapter, compiled into the selected mod, never a new runtime DLL.
// The mod's audio registration calls TryRegister before its bundle fallback.
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

    internal static bool TryRegister(ClipManager manager, string id)
    {
        Assembly assembly = typeof(EmbeddedAudio).Assembly;
        if (manifest == null)
        {
            using var source = assembly.GetManifestResourceStream("ModBuild.Audio.Manifest.json");
            manifest = source == null ? new Manifest() : JsonSerializer.Deserialize<Manifest>(source)
                ?? throw new InvalidDataException("Invalid embedded audio manifest");
        }
        Sound? sound = manifest.Sounds.FirstOrDefault(item => item.Id == id);
        if (sound == null) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(manifest.Owner, "^[A-Za-z0-9_-]+$")
            || Path.GetFileName(sound.File) != sound.File || string.IsNullOrWhiteSpace(sound.File)
            || sound.File is "." or "..")
            throw new InvalidDataException("Unsafe embedded audio path");
        string relative = manifest.Owner + "/Audio.Generated/" + sound.File;
        string path = Path.Combine(MelonEnvironment.ModsDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bool matches = false;
        if (File.Exists(path))
        {
            using var existing = File.OpenRead(path);
            using var hash = SHA256.Create();
            matches = Convert.ToHexString(hash.ComputeHash(existing)).Equals(sound.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        if (!matches)
        {
            using var source = assembly.GetManifestResourceStream(sound.Resource)
                ?? throw new InvalidDataException("Missing audio resource: " + sound.Resource);
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            byte[] data = buffer.ToArray();
            if (!Convert.ToHexString(SHA256.HashData(data)).Equals(sound.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Embedded audio hash mismatch");
            string temporary = path + ".tmp";
            try { File.WriteAllBytes(temporary, data); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        manager.LoadClipFromFile(id, relative, ClipManager.LoadType.Compressed);
        var loaded = manager.GetClip(id)?.audioClip;
        if (loaded != null && loaded.loadState == UnityEngine.AudioDataLoadState.Unloaded)
            loaded.LoadAudioData();
        if (loaded == null || loaded.loadState != UnityEngine.AudioDataLoadState.Loaded)
            throw new InvalidOperationException("AudioManager did not load embedded sound: " + id);
        return true;
    }
}
