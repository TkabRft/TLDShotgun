using HarmonyLib;
using Il2Cpp;

namespace Shotgun;

internal static partial class ShotgunPellets
{
    private static bool OwnsSlugQuery(LocalizedDamage part)
    {
        if (CurrentAmmo != ShotgunAmmo.Slug || ShotgunWildlifePolicy.QueryingNative) return false;
        var ai = AiUtils.GetBaseAiFromObject(part.gameObject);
        return ai != null && ShotgunWildlifePolicy.HasWildlifeProfile(ai);
    }

    [HarmonyPatch(typeof(LocalizedDamage), nameof(LocalizedDamage.GetDamageScale))]
    internal static class SlugDamageScale
    {
        internal static void Postfix(LocalizedDamage __instance, ref float __result)
        {
            if (OwnsSlugQuery(__instance)) __result = ShotgunWildlifePolicy.NativeScale(__instance);
        }
    }

    [HarmonyPatch(typeof(LocalizedDamage), nameof(LocalizedDamage.GetBleedOutMinutes))]
    internal static class SlugBleedTime
    {
        internal static void Postfix(LocalizedDamage __instance, ref float __result)
        {
            if (!OwnsSlugQuery(__instance)) return;
            float native = ShotgunWildlifePolicy.NativeBleed(__instance);
            __result = native > 0f ? native * 0.5f : native;
        }
    }
}
