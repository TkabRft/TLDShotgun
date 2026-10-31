using System.Security.Cryptography;
using MelonLoader;

namespace Shotgun;

internal static class EmbeddedAssetPackage
{
    // Package content changes during local testing without changing the mod version.
    // Refresh only our own package, atomically; do not alter saves or other mods.
    internal static void Refresh(string modsDirectory)
    {
        const string resource = "Shotgun.DoubleBarrel.modcomponent";
        using var stream = typeof(Main).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Missing embedded asset package: " + resource);
        using var hash = SHA256.Create();
        byte[] expected = hash.ComputeHash(stream);
        string path = Path.Combine(modsDirectory, Utility.Utility.ModComponentFileName);
        if (File.Exists(path))
        {
            using var existing = File.OpenRead(path);
            if (expected.SequenceEqual(hash.ComputeHash(existing))) return;
        }
        stream.Position = 0;
        string temporary = path + ".DoubleBarrel.tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                stream.CopyTo(output);
            File.Move(temporary, path, true);
            MelonLogger.Msg("Updated embedded shotgun assets: " + Convert.ToHexString(expected));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
