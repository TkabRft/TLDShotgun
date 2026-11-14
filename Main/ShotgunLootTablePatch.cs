using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using static Shotgun.Utility.Utility;

namespace Shotgun
{
    internal static partial class ShotgunAvailabilityPatches
    {
        [HarmonyPatch("GearSpawner.LootTableManager", "ConfigureLootTableData")]
        internal static class LootTableManager_RefilterWithModes
        {
            internal static void Prefix(LootTableData lootTableData, out int __state) => __state = lootTableData?.m_BaseEntries?.Count ?? 0;


            internal static void Postfix(LootTableData lootTableData, int __state)
            {
                if (lootTableData?.m_BaseEntries == null || !AddedShotgunEntry(lootTableData, __state)) return;
                lootTableData.FilterEntries();
            }


            private static bool AddedShotgunEntry(LootTableData lootTableData, int firstAdded)
            {
                for (int i = firstAdded; i < lootTableData.m_BaseEntries.Count; i++)
                {
                    if (lootTableData.m_BaseEntries[i].m_Item?.AssetGUID?.StartsWith(ShotgunItemPrefabName, StringComparison.Ordinal) == true) return true;
                }
                return false;
            }
        }
    }
}
