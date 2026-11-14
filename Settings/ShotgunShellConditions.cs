namespace Shotgun;

internal static class ShotgunShellConditions
{
    internal static float ForTier(int tier, int level1, int level2, int level3, int level4, int level5) =>
        Math.Clamp(Math.Clamp(tier, 0, 4) switch
        {
            0 => level1, 1 => level2, 2 => level3, 3 => level4, _ => level5
        }, 0, 100) / 100f;
}
