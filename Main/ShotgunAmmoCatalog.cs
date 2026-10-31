namespace Shotgun
{
    // Order is a save contract: append new loads, never reorder the original three.
    internal static class ShotgunAmmoCatalog
    {
        internal readonly record struct Stats(float Damage, int Range, int Pellets, float Spread,
            float Falloff, float MinimumDamage, int BleedPellets, int CriticalPellets);

        internal sealed record Definition(ShotgunPellets.ShotgunAmmo Kind, string Name, Func<Settings, Stats> Read);

        internal static readonly Definition[] Definitions =
        {
            new(ShotgunPellets.ShotgunAmmo.Slug, "Slug", s => new(s.SlugBaseDamage, s.SlugAccuracyRange, 1, 0,
                s.SlugDamageFalloff, s.SlugMinimumDamage, 0, 0)),
            new(ShotgunPellets.ShotgunAmmo.Buckshot, "Buckshot", s => new(s.BuckshotBaseDamage, s.BuckshotAccuracyRange,
                s.BuckshotPelletCount, s.BuckshotPelletSpread, s.BuckshotDamageFalloff, s.BuckshotMinimumDamage,
                s.BuckshotBleedPellets, s.BuckshotCriticalPellets)),
            new(ShotgunPellets.ShotgunAmmo.Birdshot, "Birdshot", s => new(s.BirdshotBaseDamage, s.BirdshotAccuracyRange,
                s.BirdshotPelletCount, s.BirdshotPelletSpread, s.BirdshotDamageFalloff, s.BirdshotMinimumDamage,
                s.BirdshotBleedPellets, s.BirdshotCriticalPellets)),
            new(ShotgunPellets.ShotgunAmmo.Scrapshot, "Scrapshot", s => new(s.ScrapshotBaseDamage, s.ScrapshotAccuracyRange,
                s.ScrapshotPelletCount, s.ScrapshotPelletSpread, s.ScrapshotDamageFalloff, s.ScrapshotMinimumDamage,
                s.ScrapshotBleedPellets, s.ScrapshotCriticalPellets)),
        };

        internal static Definition? Find(string name) => Definitions.FirstOrDefault(d =>
            name == $"GEAR_DoubleBarrelAmmoSingle{d.Name}" || name == $"GEAR_DoubleBarrelAmmoBox{d.Name}");

        internal static Stats Read(ShotgunPellets.ShotgunAmmo kind) =>
            Definitions.FirstOrDefault(d => d.Kind == kind)?.Read(Settings.Instance) ?? new Stats(0, 0, 1, 0, 0, 0, 0, 0);
    }
}
