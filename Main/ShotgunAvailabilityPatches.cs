using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using UnityEngine;

namespace Shotgun
{
    internal static class ShotgunAvailabilityPatches
    {
        [HarmonyPatch(typeof(Panel_CustomXPSetup), nameof(Panel_CustomXPSetup.Initialize))]
        internal static class PanelCustomXPSetup_AddShotgunsRow
        {
            internal static void Postfix(Panel_CustomXPSetup __instance) => ShotgunAvailability.AddRow(__instance);
        }


        [HarmonyPatch(typeof(Panel_CustomXPSetup), nameof(Panel_CustomXPSetup.SetUIFromCurrentCustomXP))]
        internal static class PanelCustomXPSetup_ShowShotgunsRow
        {
            internal static void Postfix(Panel_CustomXPSetup __instance) => ShotgunAvailability.ShowInRow(__instance, ShotgunAvailability.ReadRow(__instance));
        }


        [HarmonyPatch(typeof(Panel_CustomXPSetup), nameof(Panel_CustomXPSetup.OnEnterCodePopupConfirmed))]
        internal static class PanelCustomXPSetup_CodeResetsShotguns
        {
            internal static void Prefix() => ShotgunAvailability.ResetToDefault();


            internal static void Postfix(Panel_CustomXPSetup __instance) => ShotgunAvailability.ShowInRow(__instance, true);
        }


        [HarmonyPatch(typeof(Panel_CustomXPSetup), nameof(Panel_CustomXPSetup.OnContinue))]
        internal static class PanelCustomXPSetup_CarryShotgunsChoiceToNewGame
        {
            internal static void Prefix(Panel_CustomXPSetup __instance) => ShotgunAvailability.BeginContinue(__instance);


            internal static void Finalizer() => ShotgunAvailability.EndContinue();
        }


        [HarmonyPatch(typeof(CustomExperienceMode), nameof(CustomExperienceMode.CreateStringFromCustomMode))]
        internal static class CustomExperienceMode_RememberNewGameCode
        {
            internal static void Postfix(string __result) => ShotgunAvailability.RememberNewGameCode(__result);
        }


        [HarmonyPatch(typeof(CustomExperienceMode), nameof(CustomExperienceMode.CreateCustomModeFromString))]
        internal static class CustomExperienceMode_ApplyShotgunsChoiceToNewGame
        {
            internal static void Postfix(string inputString, bool __result) => ShotgunAvailability.ApplyToNewGame(inputString, __result);
        }


        [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.WriteSlotToDisk), new[] { typeof(SlotData), typeof(SaveGameSlots.Timestamp) })]
        internal static class SaveGameSlots_SaveShotguns
        {
            internal static void Prefix() => ShotgunAvailability.Save();
        }


        [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGame))]
        internal static class SaveGameSystem_ForgetShotgunsSave
        {
            internal static void Prefix() => ShotgunAvailability.ForgetLoadedSave();
        }


        [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.LoadSceneData))]
        internal static class SaveGameSystem_LoadShotguns
        {
            internal static void Postfix(bool __result)
            {
                if (__result) ShotgunAvailability.LoadOncePerSave();
                ShotgunAvailability.ApplyToCurrentMode();
            }
        }


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
