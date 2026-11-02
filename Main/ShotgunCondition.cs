using HarmonyLib;
using Il2Cpp;

namespace Shotgun;

internal static class ShotgunCondition
{
    internal static void ConfigureWear(GearItem gear)
    {
        if (gear == null) throw new InvalidOperationException("Shotgun GunItem.m_GearItem is missing");
        var wear = gear.GetComponent<DegradeOnUse>() ?? gear.gameObject.AddComponent<DegradeOnUse>();
        wear.m_DegradeHP = gear.GearItemData.m_MaxHP * 0.01f;
        gear.m_DegradeOnUse = wear;
    }

    internal static void SuccessfulShot(GunItem gun)
    {
        GearItem gear = gun.m_GearItem;
        ConfigureWear(gear);
        gear.Degrade(gear.m_DegradeOnUse.m_DegradeHP * ShotgunSkill.ConditionDegradeScale());
    }

    // The confirmed one-round debit in DoubleFire owns firing wear for this gun.
    // Native gun paths must not debit the same shell a second time.
    [HarmonyPatch(typeof(GearItem), nameof(GearItem.DegradeOnUse))]
    internal static class SuppressNativeShotgunWear
    {
        internal static bool Prefix(GearItem __instance) => __instance.m_GunItem == null
            || __instance.m_GunItem.m_GunType != GunTypeCustom;
    }
}
