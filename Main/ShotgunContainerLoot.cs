using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using MelonLoader;
using UnityEngine;

namespace Shotgun;

[HarmonyPatch(typeof(Container), nameof(Container.PopulateWithRandomGear))]
internal static class ShotgunContainerLoot
{
    internal sealed class Scope
    {
        internal readonly Container Container;
        internal readonly LootTableData[] Original;
        internal readonly Dictionary<IntPtr, LootTableData> Copies = new();
        internal Scope(Container container)
        { Container = container; Original = new[] { container.m_LootTable, container.m_LootTableData, container.m_LockedLootTableData }; }

        internal LootTableData Copy(LootTableData original)
        {
            if (original == null) return null!;
            if (!ShotgunDistribution.Parts.TryGetValue(original.name, out var parts)) return original;
            if (Copies.TryGetValue(original.Pointer, out var existing)) return existing;
            LootTableData copy = UnityEngine.Object.Instantiate(original);
            Copies.Add(original.Pointer, copy);
            copy.name = original.name;
            // Never share mutable lists or Addressables ownership with the original.
            copy.m_BaseEntries = new();
            copy.m_FilteredExtendedItems = new();
            copy.m_ExistingOperations = new();
            HashSet<string> present = new(StringComparer.Ordinal);
            foreach (var entry in original.m_BaseEntries)
            {
                string id = entry.m_Item.AssetGUID;
                // AssetReference caches its own load handle. Sharing it would also
                // share ownership even with a separate operations dictionary.
                copy.m_BaseEntries.Add(new RandomTableDataEntry<AssetReferenceGearItem>
                    { m_Item = new AssetReferenceGearItem(id), m_Weight = entry.m_Weight });
                present.Add(id);
            }
            foreach (string part in parts)
                if (present.Add(part)) copy.m_BaseEntries.Add(new RandomTableDataEntry<AssetReferenceGearItem>
                    { m_Item = new AssetReferenceGearItem(part), m_Weight = 1 });
            copy.MaybeInitializeTable();
            copy.FilterEntries();
            return copy;
        }

        internal void Restore()
        {
            if (Container != null)
            { Container.m_LootTable = Original[0]; Container.m_LootTableData = Original[1]; Container.m_LockedLootTableData = Original[2]; }
            foreach (var copy in Copies.Values) if (copy != null) UnityEngine.Object.Destroy(copy);
            Copies.Clear();
        }
    }

    private static void Prefix(Container __instance, out Scope? __state)
    {
        __state = null;
        try
        {
            string? level = GroupSpawner.CurrentLevel();
            if (level == null || ShotgunSpawnPolicy.ResourceRank(level) < 1 || !ShotgunAvailability.AllowsWorldItems()) return;
            var state = ShotgunSpawnPolicy.DistributionState();
            if (state == null || !ShotgunDistribution.AllowsScene(state, GameManager.m_ActiveScene)) return;
            __state = new Scope(__instance);
            __instance.m_LootTable = __state.Copy(__state.Original[0]);
            __instance.m_LootTableData = __state.Copy(__state.Original[1]);
            __instance.m_LockedLootTableData = __state.Copy(__state.Original[2]);
        }
        catch (Exception e)
        {
            __state?.Restore(); __state = null;
            MelonLogger.Warning($"Shotgun container parts deferred: {e.Message}");
        }
    }

    // Finalizer restores even when native population or another patch throws.
    private static void Finalizer(Scope? __state) => __state?.Restore();
}
