using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using System.Diagnostics.CodeAnalysis;
using static Shotgun.Utility.Utility;

namespace Shotgun;

internal static class ShotgunAmmoPresentation
{
    internal static void Validate(GearItem gear, string expectedId)
    {
        var data = gear.m_GearItemData;
        string expectedKey = "GAMEPLAY_DoubleBarrel_" + expectedId["GEAR_DoubleBarrel".Length..] + "Name";
        if (ShotgunAmmoCatalog.Find(expectedId) == null
            || (gear.name != expectedId && gear.name != expectedId + "(Clone)")
            || data == null || data.name != expectedId || data.DisplayNameLocID != expectedKey
            || data.ConditionType != ConditionTableType.Ammunition
            || gear.m_AmmoItem == null || gear.m_AmmoItem.m_AmmoForGunType != GunTypeCustom)
            throw new InvalidOperationException($"Incorrect shotgun ammunition registration: {expectedId}");
    }

    internal static bool IsShell([NotNullWhen(true)] GearItem? gear) => gear != null && gear.m_AmmoItem != null
        && gear.m_AmmoItem.m_AmmoForGunType == GunTypeCustom && gear.m_GearItemData != null
        && ShotgunAmmoCatalog.Find(gear.m_GearItemData.name) != null
        && gear.m_GearItemData.ConditionType == ConditionTableType.Ammunition;

    internal static void Apply(GearItem gear, ref string result)
    {
        if (!IsShell(gear)) return;
        var data = gear.m_GearItemData;
        var table = GameManager.GetConditionTable();
        if (table == null) return;
        var ranges = table.GetTableEntry(ConditionTableType.Ammunition)?.m_NameForConditionRangeList;
        if (ranges == null) return;
        string baseKey = string.IsNullOrEmpty(gear.m_DisplayNameOverrideLocID)
            ? data.DisplayNameLocID : gear.m_DisplayNameOverrideLocID;
        float percent = Math.Clamp(gear.GetNormalizedCondition(), 0f, 1f) * 100f;
        for (int i = 0; i < ranges.Count; i++)
        {
            if (percent < ranges[i].m_MinConditionForName) continue;
            // Explicit full-name variants avoid assuming vanilla builds adjectives for new loc IDs.
            string key = baseKey + "_" + ranges[i].m_Suffix;
            if (!Localization.Exists(key)) return;
            string localized = Localization.Get(key);
            if (!string.IsNullOrEmpty(localized) && localized != key) result = localized;
            return;
        }
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.GetDisplayNameForInventoryInterfaces))]
    internal static class InventoryName
    {
        internal static void Postfix(GearItem __instance, ref string __result) => Apply(__instance, ref __result);
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.GetTitleToDisplayWhenSelected))]
    internal static class SelectedTitle
    {
        internal static void Postfix(GearItem __instance, ref string __result) => Apply(__instance, ref __result);
    }

    [HarmonyPatch(typeof(GearItem), "get_DisplayNameWithCondition")]
    internal static class ConditionName
    {
        internal static void Postfix(GearItem __instance, ref string __result) => Apply(__instance, ref __result);
    }
}
