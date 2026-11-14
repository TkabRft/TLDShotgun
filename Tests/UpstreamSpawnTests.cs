using System.Text.Json;
using Il2Cpp;
using Shotgun;
using UnityEngine;
using Ledger = Shotgun.SpawnLedger;

internal static class UpstreamSpawnTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }

    internal static void Run()
    {
        foreach (string old in new[] { "WoodCabinC_IronRoad", "CoastalHouseH1_IronRoad", "LakeCabinD_IronRoad" })
        {
            string current = ShotgunSceneNames.Canonical(old);
            Check(current != old && ShotgunSceneNames.Canonical(current) == current, "Bounded scene correction");
            var historical = new GroupSpawner.Group { Name = "Parts", Picks = new() { ["High"] = 1 },
                Members = new() { new("Part_001", old, "GEAR_DoubleBarrelPartStock", new(1, 2, 3), Quaternion.identity) } };
            bool Available(string scene) => scene == current;
            Random Rng(string _) => new(42);
            var migrated = Ledger.Migrate(null, new[] { current }, new[] { historical }, "High", Available, Rng, 1);
            Check(migrated.Groups["Parts"].Processed.ContainsKey("Part_001") && migrated.Groups["Parts"].Pending.Count == 0,
                "Native corrected saved scene consumes historical selected part");
            var active = ShotgunSceneNames.Current(historical);
            Ledger.Update(migrated, new[] { active }, "High", Available, Rng, 1);
            Check(Ledger.Plan(migrated, new[] { active }, current, (_, _) => Ledger.Outcome.Usable).Count == 0,
                "Renamed visited scene never refills");
            Check(historical.Members[0].Scene == old, "Frozen baseline object is not mutated");

            var pending = Ledger.Migrate(null, Array.Empty<string>(), new[] { historical }, "High", Available, Rng, 1);
            Check(pending.Groups["Parts"].Pending.Single().Scene == current, "Unvisited reservation uses corrected scene");
            pending.Groups["Parts"].Pending[0].Scene = old;
            var loaded = Ledger.Read(JsonSerializer.Serialize(pending));
            Check(loaded.Groups["Parts"].Pending[0].Scene == current && loaded.Groups["Parts"].Quota == 1,
                "V2 pending alias preserves quota and ID");
            Ledger.ReconcileSavedScenes(loaded, new[] { old });
            Check(loaded.Groups["Parts"].Pending.Count == 0 && loaded.Groups["Parts"].Processed["Part_001"].Outcome == Ledger.Outcome.Empty,
                "Old native scene key consumes corrected reservation");

            loaded.VisitedScenes = new() { old, current };
            loaded.Groups["Parts"].Processed["Part_001"].Scene = old;
            loaded.DistributionVersion = 1;
            loaded.PreDistributionScenes.Add(old); loaded.RifleScenes.Add(old);
            loaded.RifleDecisions[old + "\nrifle"] = true; loaded.RifleDecisions[current + "\nrifle"] = true;
            loaded = Ledger.Read(JsonSerializer.Serialize(loaded));
            Check(loaded.VisitedScenes.SetEquals(new[] { current }) && loaded.Groups["Parts"].Processed["Part_001"].Scene == current,
                "V2 visited aliases and processed records normalize");
            Check(!ShotgunDistribution.AllowsScene(loaded, current) && loaded.PreDistributionScenes.Contains(current)
                && loaded.RifleScenes.Contains(current) && loaded.RifleDecisions.Count == 1, "Distribution history normalizes");
            Check(ShotgunDistribution.ChooseRifle(loaded, old, "rifle", false, 0, () => throw new Exception("reroll")),
                "Rifle alias retains prior decision");
            loaded.RifleDecisions[old + "\nrifle"] = false;
            bool rejected = false;
            try { Ledger.Read(JsonSerializer.Serialize(loaded)); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Contradictory rifle aliases fail closed");

            var gun = new GroupSpawner.Group { Name = "SG_Shotgun", Members = new() {
                new("Gun_001", old, "GEAR_DoubleBarrel", new(1, 2, 3), Quaternion.identity) } };
            foreach (string selectedScene in new[] { old, current })
            {
                string key = Ledger.LegacyMemberKey(gun.Name, gun.Members[0] with { Scene = selectedScene });
                var v1 = new Ledger.LegacyState { Version = 1, LegacyScenes = new() { [selectedScene] = false },
                    Selections = new() { [selectedScene] = new() { key } }, Outcomes = new() { [key] = Ledger.Outcome.Ruined } };
                var state = Ledger.Migrate(JsonSerializer.Serialize(v1), new[] { current }, new[] { gun }, "High", Available, Rng, 1);
                Check(state.Groups[gun.Name].Processed["Gun_001"].Outcome == Ledger.Outcome.Ruined
                    && state.Groups[gun.Name].Pending.Count == 0 && state.VisitedScenes.Contains(current), "V1 old/new keys retain outcome and identity");
            }
        }
        Check(ShotgunSceneNames.Canonical("Other_IronRoad") == "Other_IronRoad", "Unrelated scenes unchanged");
        LootTableData table = new();
        void Add(string id) => table.m_BaseEntries.Add(new() { m_Item = new AssetReferenceGearItem(id) });
        Add("GEAR_DoubleBarrel");
        ShotgunAvailabilityPatches.LootTableManager_RefilterWithModes.Prefix(table, out int before);
        ShotgunAvailabilityPatches.LootTableManager_RefilterWithModes.Postfix(table, before);
        Check(table.m_FilteredExtendedItems.Count == 0, "Unchanged table not refiltered");
        Add("GEAR_FishingTackle");
        ShotgunAvailabilityPatches.LootTableManager_RefilterWithModes.Postfix(table, before);
        Check(table.m_FilteredExtendedItems.Count == 0, "Existing shotgun plus unrelated additions do not refilter");
        Add("GEAR_DoubleBarrelAmmoSingleSlug");
        ShotgunAvailabilityPatches.LootTableManager_RefilterWithModes.Postfix(table, before);
        Check(table.m_FilteredExtendedItems.Count == 3, "New shotgun entry refilters table");
        Console.WriteLine($"PASS: {checks} upstream scene/loot compatibility checks.");
    }
}

namespace Shotgun.Utility
{
    internal static class Utility { internal const string ShotgunItemPrefabName = "GEAR_DoubleBarrel"; }
}
