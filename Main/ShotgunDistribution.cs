namespace Shotgun;

// Weight 1 is relative to the native table, never a percentage or an extra draw.
internal static class ShotgunDistribution
{
    internal const string Barrel = "GEAR_DoubleBarrelPartBarrel";
    internal const string Stock = "GEAR_DoubleBarrelPartStock";
    internal const string Forend = "GEAR_DoubleBarrelPartForend";
    internal const string Trigger = "GEAR_DoubleBarrelPartTrigger";
    internal static readonly IReadOnlyDictionary<string, string[]> Parts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["LootCargoRifle"] = new[] { Barrel, Stock, Forend, Trigger },
        ["LootTableLockerLocked"] = new[] { Barrel, Stock, Forend, Trigger },
        ["LootTableToolChest"] = new[] { Forend, Trigger },
        ["LootTableToolChestDrawer"] = new[] { Trigger },
        ["LootTableMetalBox"] = new[] { Trigger },
        ["LootTableVehicleTrunk"] = new[] { Stock, Forend },
        ["LootTableVehicleTrunkCoastal"] = new[] { Stock, Forend },
        ["LootTableVehicleTrunkLocked"] = new[] { Stock, Forend },
        ["LootTableVehicleTrunkLockedCoastal"] = new[] { Stock, Forend }
    };

    internal static bool AllowsScene(SpawnLedger.State state, string scene) => state.DistributionVersion == 1
        && !string.IsNullOrWhiteSpace(scene) && scene is not ("Boot" or "MainMenu" or "Empty")
        && !state.PreDistributionScenes.Contains(ShotgunSceneNames.Canonical(scene));

    internal static bool ChooseRifle(SpawnLedger.State state, string scene, string id, bool enabled, int chance, Func<int> roll)
    {
        string key = ShotgunSceneNames.Canonical(scene) + "\n" + id;
        if (!state.RifleDecisions.TryGetValue(key, out bool replace))
            state.RifleDecisions.Add(key, replace = enabled && roll() < Math.Clamp(chance, 0, 100));
        return replace;
    }
}
