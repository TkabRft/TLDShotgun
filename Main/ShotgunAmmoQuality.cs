using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;

namespace Shotgun;

internal static class ShotgunAmmoQuality
{
    // Crafting may nest callbacks. Each finalizer restores the previous scope even on failure.
    [ThreadStatic] private static string? activeResult;

    internal static string? Enter(CraftingOperation operation)
    {
        string? previous = activeResult;
        var gear = operation.Blueprint?.m_CraftedResultGear;
        activeResult = ShotgunAmmoPresentation.IsShell(gear) ? gear.m_GearItemData.name : null;
        return previous;
    }

    internal static void Exit(string? previous) => activeResult = previous;

    internal static void Apply(string? gearId, ref float normalizedCondition)
    {
        if (activeResult == null || activeResult != gearId || Settings.Instance == null) return;
        var skill = GameManager.GetSkillGunsmithing();
        if (skill == null) return;
        normalizedCondition = Settings.Instance.ShellConditionForTier(skill.GetCurrentTierNumber());
    }

    [HarmonyPatch(typeof(CraftingOperation), nameof(CraftingOperation.ApplyCraftingProgress))]
    internal static class Progress
    {
        internal static void Prefix(CraftingOperation __instance, out string? __state) => __state = Enter(__instance);
        internal static Exception? Finalizer(Exception? __exception, string? __state) { Exit(__state); return __exception; }
    }

    [HarmonyPatch(typeof(CraftingOperation), nameof(CraftingOperation.HandleSuccess))]
    internal static class Success
    {
        internal static void Prefix(CraftingOperation __instance, out string? __state) => __state = Enter(__instance);
        internal static Exception? Finalizer(Exception? __exception, string? __state) { Exit(__state); return __exception; }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateItemInPlayerInventory),
        new[] { typeof(GearItem), typeof(int), typeof(float), typeof(PlayerManager.InventoryInstantiateFlags) })]
    internal static class InventoryGear
    {
        internal static void Prefix(GearItem gearItem, ref float normalizedCondition)
        {
            if (ShotgunAmmoPresentation.IsShell(gearItem)) Apply(gearItem.m_GearItemData.name, ref normalizedCondition);
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateItemInPlayerInventory),
        new[] { typeof(AssetReferenceGearItem), typeof(int), typeof(float), typeof(PlayerManager.InventoryInstantiateFlags) })]
    internal static class InventoryReference
    {
        internal static void Prefix(AssetReferenceGearItem assetReference, ref float normalizedCondition)
        {
            if (assetReference != null) Apply(assetReference.RuntimeKey?.ToString(), ref normalizedCondition);
        }
    }
}
