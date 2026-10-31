using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections;
using System.Reflection;

namespace Shotgun
{
    internal static class ShotgunRestoration
    {
        private static readonly Dictionary<IntPtr, GearItem> PendingRuined = new();

        internal static void Configure(ConfigurationReferences refs)
        {
            Millable source = refs.Rifle.Gun.Prefab.GetComponent<Millable>();
            if (source == null) throw new InvalidOperationException("GEAR_Rifle has no milling configuration");
            GameObject prefab = refs.Shotgun.Gun.Prefab;
            Millable target = prefab.GetComponent<Millable>() ?? prefab.AddComponent<Millable>();
            target.m_RepairRequiredGear = source.m_RepairRequiredGear;
            target.m_RepairRequiredGearUnits = source.m_RepairRequiredGearUnits;
            target.m_RepairDurationMinutes = source.m_RepairDurationMinutes;
            target.m_CanRestoreFromWornOut = true;
            target.m_RecoveryDurationMinutes = 180;
            target.m_Skill = SkillType.Gunsmithing;
            GearItem scrap = Addressables.LoadAssetAsync<GameObject>("GEAR_ScrapMetal").WaitForCompletion().GetComponent<GearItem>();
            GearItem fir = Addressables.LoadAssetAsync<GameObject>("GEAR_FirewoodFir").WaitForCompletion().GetComponent<GearItem>();
            target.m_RestoreRequiredGear = new Il2CppReferenceArray<GearItem>(new[] { scrap, fir });
            target.m_RestoreRequiredGearUnits = new Il2CppStructArray<int>(new[] { 4, 1 });
            refs.Shotgun.Gun.GearItem.m_Millable = target;
        }

        internal static void MarkRuined(GameObject item)
        {
            GearItem gear = item.GetComponent<GearItem>();
            if (gear == null) throw new InvalidOperationException("Spawned shotgun has no GearItem");
            PendingRuined[gear.Pointer] = gear;
            ApplyRuined(gear);
            MelonCoroutines.Start(ClearAfterInitialization(gear.Pointer, gear));
        }

        private static IEnumerator ClearAfterInitialization(IntPtr pointer, GearItem gear)
        {
            yield return new WaitForEndOfFrame();
            if (PendingRuined.TryGetValue(pointer, out var expected) && ReferenceEquals(expected, gear))
                PendingRuined.Remove(pointer);
        }

        private static void ApplyRuined(GearItem gear)
        {
            gear.SetNormalizedHP(0f, true);
            GunItem gun = gear.GetComponent<GunItem>();
            if (gun == null) return;
            gun.m_Clip?.Clear();
            gun.m_SpentCasingsInClip = 0;
        }

        internal static void Reset() => PendingRuined.Clear();

        // Intercept the verified condition-roll APIs only during the spawn frame.
        // Loaded/previously repaired guns never receive this transient marker.
        [HarmonyPatch]
        private static class FinishInitialCondition
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(GearItem), nameof(GearItem.RollSeededGearConditionAndWeight));
                yield return AccessTools.Method(typeof(GearItem), nameof(GearItem.RollGearConditionAndWeight));
            }

            private static void Postfix(GearItem __instance)
            {
                if (!PendingRuined.TryGetValue(__instance.Pointer, out var expected) || expected == null || expected != __instance) return;
                ApplyRuined(__instance);
            }
        }
    }
}
