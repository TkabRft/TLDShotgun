using Il2Cpp;
using Shotgun;
using UnityEngine;

internal static class UpstreamAudioTests
{
    internal static int Checks;
    internal static void Run()
    {
        var refs = Shotgun.Main.Instance.Configuration.Refs;
        ShotgunAudio.Begin(refs);
        var source = refs.AudioShot!._audioSource;
        var clip = new AudioClip { samples = 4800, frequency = 48000, channels = 1, length = .1f, loadState = AudioDataLoadState.Loaded };
        ShotgunAudio.Register(ShotgunAudio.ShellDrop, clip);
        ShotgunAudio.Register("reload", clip);
        int checks = 0;
        foreach (string surface in new[] { "Snow", "Dirt", "Straw", "Rug", "Cloth", "Flesh", "Stone", "Wood", "" })
        {
            Utils.Surface = surface;
            int before = source.Plays;
            ShotgunAudio.Play(ShotgunAudio.ShellDrop);
            float expected = surface is "Stone" or "Wood" or "" ? 1f : .25f;
            if (source.Plays != before + 1 || source.LastScale != expected || source.volume != .6f)
                throw new Exception("Shell drop volume/overlap/SFX changed on " + surface);
            checks++;
            int queries = Utils.Queries;
            ShotgunAudio.Play("reload");
            if (source.LastScale != 1f || Utils.Queries != queries) throw new Exception("Other mechanical audio attenuated");
            checks++;
        }
        GameManager.SurfacePlayer = null;
        ShotgunAudio.Play(ShotgunAudio.ShellDrop);
        if (source.LastScale != 1f) throw new Exception("Missing player did not preserve normal volume");
        GameManager.SurfacePlayer = new Transform();
        Checks = checks + 1;
    }
}
