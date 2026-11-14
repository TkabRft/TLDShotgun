namespace Shotgun;

// Only the three scene corrections shipped upstream in 0.13.7/0.14.0.
internal static class ShotgunSceneNames
{
    internal static string Canonical(string scene)
    {
        if (scene.Equals("WoodCabinC_IronRoad", StringComparison.OrdinalIgnoreCase)) return "WoodCabinCIronRoad";
        if (scene.Equals("CoastalHouseH1_IronRoad", StringComparison.OrdinalIgnoreCase)) return "CoastalHouseH1IronRoad";
        if (scene.Equals("LakeCabinD_IronRoad", StringComparison.OrdinalIgnoreCase)) return "LakeCabinDIronRoad";
        return scene;
    }

    internal static GroupSpawner.Group Current(GroupSpawner.Group historical) => new()
    {
        Name = historical.Name,
        Scene = historical.Scene == null ? null : Canonical(historical.Scene),
        Picks = historical.Picks,
        Members = historical.Members.Select(m => m with { Scene = Canonical(m.Scene) }).ToList(),
        Alternatives = historical.Alternatives
    };

    internal static Dictionary<string, T> MergeKeys<T>(IEnumerable<KeyValuePair<string, T>> source,
        Func<string, string> key, IEqualityComparer<string> comparer)
    {
        Dictionary<string, T> result = new(comparer);
        foreach (var pair in source)
        {
            string name = key(pair.Key);
            if (result.TryGetValue(name, out var previous) && !EqualityComparer<T>.Default.Equals(previous, pair.Value))
                throw new InvalidDataException("Conflicting historical scene aliases");
            result[name] = pair.Value;
        }
        return result;
    }

    internal static void Normalize(SpawnLedger.State state)
    {
        state.VisitedScenes = new(state.VisitedScenes.Select(Canonical), StringComparer.OrdinalIgnoreCase);
        state.PreDistributionScenes = new(state.PreDistributionScenes.Select(Canonical), StringComparer.OrdinalIgnoreCase);
        state.RifleScenes = new(state.RifleScenes.Select(Canonical), StringComparer.OrdinalIgnoreCase);
        foreach (var group in state.Groups.Values)
        {
            foreach (var decision in group.Processed.Values) decision.Scene = Canonical(decision.Scene);
            foreach (var pending in group.Pending) pending.Scene = Canonical(pending.Scene);
        }
        state.RifleDecisions = MergeKeys(state.RifleDecisions, key =>
        {
            int separator = key.IndexOf('\n');
            return separator < 0 ? key : Canonical(key[..separator]) + key[separator..];
        }, StringComparer.Ordinal);
    }
}
