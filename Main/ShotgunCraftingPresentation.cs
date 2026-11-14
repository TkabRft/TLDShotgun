using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Shotgun;

// Presentation belongs to the consuming UI. Never rename gear data or temporarily
// change localization overrides: inventory quality names use those same objects.
internal static class ShotgunCraftingPresentation
{
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static readonly HashSet<string> Reported = new();

    internal static string? LoadName(GearItem? gear) => gear == null || gear.m_GearItemData == null
        ? null : ShotgunAmmoCatalog.Find(gear.m_GearItemData.name)?.Name;

    internal static string? ShellName(GearItem? gear)
    {
        if (gear != null && !string.IsNullOrEmpty(gear.m_DisplayNameOverrideLocID)) return null;
        string? load = LoadName(gear);
        if (load == null) return null;
        string key = "GAMEPLAY_DoubleBarrel_" + load + "ShellName";
        if (!Localization.Exists(key)) return null;
        string value = Localization.Get(key);
        return string.IsNullOrEmpty(value) || value == key ? null : value;
    }

    internal static bool IsLooseWorldShell(GearItem? gear) => LoadName(gear) != null
        && !gear!.m_InPlayerInventory && gear.m_StackableItem != null && gear.m_StackableItem.m_Units == 1;

    private static string MatchCase(string value, string original) => !string.IsNullOrEmpty(original)
        && original == original.ToUpperInvariant() ? value.ToUpperInvariant() : value;

    internal static string? CraftIcon(GearItem? gear)
    {
        if (gear == null || gear.m_GearItemData == null) return null;
        string id = gear.m_GearItemData.name;
        return LoadName(gear) != null || id is "GEAR_DoubleBarrelAmmoCasing"
            or "GEAR_DoubleBarrelLeadBuckshot" or "GEAR_DoubleBarrelLeadBirdshot" or "GEAR_DoubleBarrelScrapBuckshot"
            ? id.Replace("GEAR_", "ico_CraftItem__") : null;
    }

    private static Texture2D? Texture(string address)
    {
        if (Textures.TryGetValue(address, out var cached) && cached != null) return cached;
        try
        {
            var texture = Addressables.LoadAssetAsync<Texture2D>(address).WaitForCompletion();
            if (texture != null) { Textures[address] = texture; return texture; }
        }
        catch (Exception e)
        {
            if (Reported.Add(address)) MelonLogger.Warning($"Could not load crafting artwork {address}: {e.Message}");
        }
        return null; // Retain native presentation if registration is not ready.
    }

    [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.GetDisplayedNameWithCount))]
    internal static class RecipeName
    {
        internal static void Postfix(BlueprintData __instance, ref string __result)
        {
            string? name = ShellName(__instance.m_CraftedResultGear);
            if (name != null) __result = __instance.m_CraftedResultCount > 1
                ? $"{name} ({__instance.m_CraftedResultCount})" : name;
        }
    }

    [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.RefreshSelectedBlueprint))]
    internal static class SelectedRecipe
    {
        internal static void Postfix(Panel_Crafting __instance)
        {
            var blueprint = __instance.SelectedBPI;
            if (blueprint == null) return;
            var gear = blueprint.m_CraftedResultGear;
            string? name = ShellName(gear);
            if (name == null) return;
            if (__instance.m_SelectedName != null)
                __instance.m_SelectedName.text = MatchCase(blueprint.GetDisplayedNameWithCount(), __instance.m_SelectedName.text);
            var texture = Texture("ico_CraftingPreview__DoubleBarrelAmmoSingle" + LoadName(gear));
            if (texture != null && __instance.m_SelectedImage != null) __instance.m_SelectedImage.mainTexture = texture;
        }
    }

    [HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.Enable))]
    internal static class RequirementIcon
    {
        internal static void Postfix(CraftingRequirementMaterial __instance, BlueprintData bp, int requiredIndex)
        {
            if (bp == null || CraftIcon(bp.m_CraftedResultGear) == null || bp.m_RequiredGear == null
                || requiredIndex < 0 || requiredIndex >= bp.m_RequiredGear.Length || __instance.m_Icon == null) return;
            string? address = CraftIcon(bp.m_RequiredGear[requiredIndex].m_Item);
            if (address == null) return;
            var texture = Texture(address);
            if (texture != null) __instance.m_Icon.mainTexture = texture;
            // Native ApplyTints retains available/unavailable colors and quantity handling.
        }
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.GetHoverText))]
    internal static class WorldHover
    {
        internal static void Postfix(GearItem __instance, ref string __result)
        {
            if (!IsLooseWorldShell(__instance) || string.IsNullOrEmpty(__result)) return;
            string? name = ShellName(__instance);
            if (name == null) return;
            // Preserve native markup/status text surrounding the name, if present.
            string qualityName = __instance.GetDisplayNameForInventoryInterfaces();
            if (!string.IsNullOrEmpty(qualityName))
            {
                __result = __result.Replace(qualityName.ToUpperInvariant(), name.ToUpperInvariant());
                __result = __result.Replace(qualityName, name);
            }
            string baseName = __instance.DisplayName;
            if (!string.IsNullOrEmpty(baseName))
            {
                __result = __result.Replace(baseName.ToUpperInvariant(), name.ToUpperInvariant());
                __result = __result.Replace(baseName, name);
            }
        }
    }

    // Interactive-object callers can use the property rather than GetHoverText.
    // Applying the same replacement twice is harmless and preserves native markup.
    [HarmonyPatch(typeof(GearItem), "get_HoverText")]
    internal static class WorldHoverProperty
    {
        internal static void Postfix(GearItem __instance, ref string __result) => WorldHover.Postfix(__instance, ref __result);
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UpdateInspectGear))]
    internal static class WorldInspection
    {
        internal static void Postfix(PlayerManager __instance, Panel_HUD __0)
        {
            var gear = __instance.GearItemBeingInspected();
            if (!IsLooseWorldShell(gear) || __0 == null || __0.m_InspectMode_Title == null) return;
            string? name = ShellName(gear);
            if (name != null) __0.m_InspectMode_Title.text = MatchCase(name, __0.m_InspectMode_Title.text);
        }
    }
}
