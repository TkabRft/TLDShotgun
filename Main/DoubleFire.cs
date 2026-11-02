using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace Shotgun
{
    internal static class DoubleFire
    {
        internal static bool FiringSecondBarrel { get; private set; }


        [HarmonyPatch(typeof(vp_FPSShooter), nameof(vp_FPSShooter.Fire))]
        internal static class vp_FPSShooter_FireBothBarrels
        {
            internal static void Prefix(vp_FPSShooter __instance, out int __state)
            {
                GunItem? gunItem = __instance.m_Weapon?.m_GunItem;
                __state = gunItem != null && gunItem.m_GunType == GunTypeCustom && gunItem.m_Clip != null ? gunItem.m_Clip.Count : -1;
            }

            internal static void Postfix(vp_FPSShooter __instance, int __state)
            {
                if (__state < 0) return;
                GunItem? gunItem = __instance.m_Weapon?.m_GunItem;
                if (gunItem == null || gunItem.m_Clip == null
                    || !ShotgunAudio.SuccessfulShot(__state, gunItem.m_Clip.Count, gunItem.m_IsJammed, gunItem.m_HasMisfired)) return;
                ShotgunCondition.SuccessfulShot(gunItem);
                ShotgunAudio.Play(ShotgunAudio.Discharge);
                if (FiringSecondBarrel || __state < 2) return;
                if (gunItem.m_GearItem.m_CurrentHP <= 0f) return;
                if (!Input.GetKey(Settings.Instance.DoubleFireKeyCode)) return;

                FiringSecondBarrel = true;
                try
                {
                    __instance.m_NextAllowedFireTime = 0f;
                    __instance.Fire();
                }
                finally
                {
                    FiringSecondBarrel = false;
                }
            }
        }
    }
}
