namespace Shotgun;

internal static class ShotgunBalanceDefaults
{
    internal const int Revision = 1;
    internal static bool Upgrade(ref int slugDamage, ref int revision)
    {
        if (revision >= Revision) return false;
        slugDamage = 150; // Requested one-time reset includes custom saved values.
        revision = Revision;
        return true;
    }
}
