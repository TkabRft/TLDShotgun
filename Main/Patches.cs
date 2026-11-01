using HarmonyLib;
using ModComponent.Utils;
using Il2Cpp;
using UnityEngine;
using MelonLoader;
using UnityEngine.Playables;
using UnityEngine.AddressableAssets;
using Shotgun.Utility;

namespace Shotgun
{
    // Note for self: A lot of vanilla firearm functionality hinges on checks for GunType == 1 through 3, none of which we proc with our "custom" gun type.
    // I end up having to recreate a fair bit of vanilla firearm logic here in order to continue streamlined use of vanilla methods as triggers and so forth
    // so if it looks like we're reinventing half the wheel... we are. Wheeeeee!

    //BUG: I noticed on holding left dropping  right, spent casings in barrel doesnt reset to 0 until after reloadingis complete... that should sync iup immediately. Its messing with the visibility layer and likely causing an earlier issue with not transitionoing through reloading correctly.
    // BUG: I noticed if I have zero remaining ammo, one spent shell and one live shell, i cannot swap ammo - i have to shoot that other shell first. What gives 
    // BUG: "InsertBothMedium" goes right-left... which is is against our existing left-right format.

    internal class Patches
    {
        [HarmonyPatch(typeof(vp_FPSCamera), nameof(vp_FPSCamera.LoadAllFPSWeapons))]
        internal static class LateInitializationPatch
        {
            internal static void Postfix() => Main.Instance?.LateInitialize();
        }

        [HarmonyPatch(typeof(EquipItemPopup), nameof(EquipItemPopup.AllowedToHideAmmoPopup))]
        [HarmonyPriority(-999999)]
        internal static class EquipItemPopup_AllowedToHideAmmoPopup_ModComponentCompatibilityFix
        {
            internal static void Postfix(ref bool __result)
            {
                if (!__result) return;
                PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
                if (playerManager == null) return;
                if (playerManager.m_ItemInHands == null) return;
                if (playerManager.m_ItemInHands.m_GunItem == null) return;
                __result = false;
            }
        }


        [HarmonyPatch(typeof(GunItem), nameof(GunItem.PressSwitchAmmo))]
        internal static class GunItem_PreventSwitchAmmoUnlessIntended
        {
            internal static bool Prefix(GunItem __instance)
            {
                if (__instance.m_GunType != GunTypeCustom) return true;
                return false;
            }
        }


        [HarmonyPatch(typeof(GunItem), nameof(GunItem.PressReloadAmmo))]
        internal static class GunItem_PreventReloadAmmoUnlessIntended
        {
            private static bool mOwnerRequest = false;

            internal static void StartShotgunReload(GunItem gunItem)
            {
                mOwnerRequest = true;
                try
                {
                    gunItem.PressReloadAmmo();
                }
                finally
                {
                    mOwnerRequest = false;
                }
            }

            internal static bool Prefix(GunItem __instance)
            {
                if (__instance.m_GunType != GunTypeCustom) return true;
                return mOwnerRequest;
            }
        }


        [HarmonyPatch(typeof(GunItem), nameof(GunItem.Start))]
        internal static class GunItem_UpdateOnStart
        {
            internal static void Postfix(GunItem __instance)
            {
                if (__instance.m_GunType != GunTypeCustom) return;
                ShotgunSkill.ApplyToGunItem(__instance);
                UpdateShotgunParams(__instance, __instance.ValidAmmo[__instance.m_SelectedAmmoIndex].DisplayName);
                CustomAmmoHUD.Refresh();
            }
        }


        [HarmonyPatch(typeof(GunItem), nameof(GunItem.Update))]
        internal static class GunItem_SwitchAmmoOnT
        {
            internal static void Postfix(GunItem __instance)
            {
                if (__instance.m_GunType != GunTypeCustom) return;
                AmmoRadial.Tick(__instance);
            }
        }

        internal static bool TryBeginAmmoSwap(GunItem gunItem)
        {
            if (gunItem.GetNextValidAmmoIndex() == gunItem.m_SelectedAmmoIndex) return false; //no other ammo available
            if (!gunItem.CanReload()) return false;
            if (gunItem.m_IsJammed) return false;
            if (!TryGetSelectedAmmoReserve(gunItem, out int reserve)) return false;

            gunItem.m_PreviousAmmoIndex = gunItem.m_SelectedAmmoIndex; //indicate is ammo swap during reload
            if (reserve > 0)
            {
                GunItem_PreventReloadAmmoUnlessIntended.StartShotgunReload(gunItem);
                return true;
            }
            ReloadPastVanillaAmmoGate();
            return true;
        }


        [HarmonyPatch(typeof(GunItem), nameof(GunItem.Update))]
        internal static class GunItem_ReloadAmmoOnR
        {
            internal static void Postfix(GunItem __instance)
            {
                if (__instance.m_GunType != GunTypeCustom) return;
                if (!InputManager.HasContext(InputManager.m_CurrentContext)) return;
                if (!InputManager.GetReloadPressed(InputManager.m_CurrentContext)) return;
                if (!__instance.CanReload()) return;

                if (__instance.m_IsJammed)
                {
                    GunItem_PreventReloadAmmoUnlessIntended.StartShotgunReload(__instance);
                    return;
                }

                if (__instance.m_Clip.Count == __instance.m_ClipSize && __instance.m_SpentCasingsInClip == 0) return;
                if (!TryGetSelectedAmmoReserve(__instance, out int reserve)) return;

                if (reserve > 0)
                {
                    __instance.m_PreviousAmmoIndex = -1; //indicate is NOT ammo swap during reload
                    GunItem_PreventReloadAmmoUnlessIntended.StartShotgunReload(__instance);
                    CustomAmmoHUD.Refresh();
                    return;
                }
                if (__instance.m_SpentCasingsInClip > 0)
                {
                    __instance.m_PreviousAmmoIndex = -1; //indicate is NOT ammo swap during reload
                    ReloadPastVanillaAmmoGate();
                    CustomAmmoHUD.Refresh();
                    return;
                }
            }
        }


        [HarmonyPatch(typeof(GunItem), nameof(GunItem.RemoveNextFromClip))]
        internal static class GunItem_AddSpentShellCasingOnRemoveNextFromClip
        {
            internal static void Prefix(GunItem __instance)
            {
                if (__instance.m_GunType != GunTypeCustom) return;
                if (__instance.m_Clip.Count == 0) return;
                __instance.m_SpentCasingsInClip++;
            }
        }

        [HarmonyPatch(typeof(vp_FPSShooter), nameof(vp_FPSShooter.Reload), new Type[] { typeof(int) })]
        internal static class vp_FPSShooter_EnsureCorrectReloadCount
        {
            internal static void Prefix(ref int ammoCount, vp_FPSShooter __instance)
            {
                if (__instance == null) return;
                if (__instance.m_Weapon == null) return;
                if (__instance.m_Weapon.m_GunItem == null) return;
                if (__instance.m_Weapon.m_GunItem.m_GunType != GunTypeCustom) return;

                GunItem gunItem = __instance.m_Weapon.m_GunItem;
                if (__instance.m_Weapon.m_BulletsToReload > 0) return;
                if (__instance.IsReloadCooldownActive) return;

                if (gunItem.m_PreviousAmmoIndex >= 0 && gunItem.m_Clip.Count == 0)
                    SwitchToNextAmmoType(gunItem);

                int slotsToFill;
                if (gunItem.m_PreviousAmmoIndex >= 0)
                {
                    slotsToFill = gunItem.m_ClipSize;
                }
                else
                {
                    slotsToFill = gunItem.m_ClipSize - gunItem.m_Clip.Count;
                }

                int ammoAvailable = 0;
                GearItem weaponInHands = GameManager.GetPlayerManagerComponent()?.m_ItemInHands;
                Inventory inventory = GameManager.GetInventoryComponent();
                if (weaponInHands != null && inventory != null)
                {
                    if (gunItem.m_PreviousAmmoIndex >= 0)
                        ammoAvailable = inventory.GetAmmoAvailableForWeapon(gunItem.ValidAmmo[gunItem.GetNextValidAmmoIndex()]);
                    else
                        ammoAvailable = inventory.GetAmmoAvailableForWeapon(weaponInHands);
                }

                ammoCount = Math.Min(slotsToFill, ammoAvailable);
            }
        }


        [HarmonyPatch(typeof(GunItem), nameof(GunItem.Fired))]
        internal static class GunItem_RefreshCustomHUDOnFired
        {
            internal static void Postfix(GunItem __instance)
            {
                CustomAmmoHUD.Refresh();

                if (__instance.m_GunType != GunTypeCustom) return;
                Actions.SyncAmmoParams(__instance);
            }
        }


        [HarmonyPatch(typeof(vp_FPSShooter), nameof(vp_FPSShooter.OnBulletLoaded))]
        internal static class vp_FPSShooter_RefreshHUDOnBulletLoaded
        {
            internal static void Postfix()
            {
                CustomAmmoHUD.Refresh();
            }
        }


        [HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.Update))]
        internal static class PlayerMovement_TransmitLocomotion
        {
            internal static void Postfix(PlayerMovement __instance)
            {
                Actions.TransmitLocomotion(__instance);
            }
        }


        [HarmonyPatch(typeof(PlayerAnimation), nameof(PlayerAnimation.Trigger_Generic_Equip))]
        internal static class PlayerAnimation_ShotgunEquip
        {
            internal static void Postfix(PlayerAnimation __instance, GearItem gearItem)
            {
                if (__instance.m_State != PlayerAnimation.State.Equipping && __instance.m_State != PlayerAnimation.State.Showing) return;
                if (!MaybeLatchShotgunAnimator(__instance, "Equip")) return;

                ShotgunAim.Reset("equip");

                if (__instance.m_EquippedFirstPersonWeaponShoulder != null)
                    __instance.m_EquippedFirstPersonWeaponShoulder.gameObject.name = ShotgunFPHPrefabName;
                Animator moddedAnimator = Main.Instance.Configuration.Refs.ModdedAnimator;
                moddedAnimator.Rebind();

                GunItem equippedGun = gearItem?.m_GunItem;
                if (equippedGun != null) 
                    Actions.SyncAmmoParams(equippedGun);


                ShotgunSkill.ApplyAnimatorParams(moddedAnimator);
                moddedAnimator.SetTrigger("Trigger_Generic_Equip");
                moddedAnimator.Update(0f);
            }
        }


        [HarmonyPatch(typeof(PlayerAnimation), nameof(PlayerAnimation.Trigger_Generic_Unequip))]
        internal static class PlayerAnimation_ShotgunUnequip
        {
            internal static void Postfix(PlayerAnimation __instance, bool __result)
            {
                if (!__result) return;
                if (__instance.m_EquippedFirstPersonWeaponShoulder == null) return;
                if (!__instance.m_EquippedFirstPersonWeaponShoulder.name.StartsWith(ShotgunFPHPrefabName)) return;

                ShotgunAim.Interrupt(__instance, "unequip");

                Animator moddedAnimator = Main.Instance.Configuration.Refs.ModdedAnimator;
                if (moddedAnimator == null || !moddedAnimator.enabled)
                {
                    Animator vanillaAnimator = Main.Instance.Configuration.Refs.VanillaAnimator;
                    if (vanillaAnimator != null) vanillaAnimator.enabled = true;
                    return;
                }

                GunItem gunItem = GunItemInHands();
                SyncAmmoParams(gunItem);
                ResetShotgunTriggers(moddedAnimator);
                moddedAnimator.SetTrigger("Trigger_Generic_Unequip");
            }
        }


        [HarmonyPatch(typeof(PlayerAnimation), nameof(PlayerAnimation.Reset))]
        internal static class PlayerAnimation_ShotgunReset
        {
            internal static void Postfix(PlayerAnimation __instance)
            {
                ShotgunAim.Interrupt(__instance, "native reset");
                if (__instance.m_EquippedFirstPersonWeaponShoulder != null && __instance.m_EquippedFirstPersonWeaponShoulder.name.StartsWith(ShotgunFPHPrefabName)) return;

                Animator moddedAnimator = Main.Instance?.Configuration?.Refs?.ModdedAnimator;
                if (moddedAnimator == null || !moddedAnimator.enabled) return;

                ResetShotgunTriggers(moddedAnimator);
                moddedAnimator.enabled = false;

                Animator vanillaAnimator = Main.Instance.Configuration.Refs.VanillaAnimator;
                if (vanillaAnimator != null) vanillaAnimator.enabled = true;
            }
        }


        [HarmonyPatch(typeof(PlayerAnimation), nameof(PlayerAnimation.Trigger_Generic_Aim))]
        internal static class PlayerAnimation_ShotgunAim
        {
            internal static void Postfix(PlayerAnimation __instance)
            {
                if (__instance.m_State != PlayerAnimation.State.ToAiming) return;
                if (!MaybeLatchShotgunAnimator(__instance, "Aim")) return;
                ShotgunAim.Request(__instance, ShotgunAim.Direction.Raise);
            }
        }


        [HarmonyPatch(typeof(PlayerAnimation), nameof(PlayerAnimation.Trigger_Generic_Aim_Cancel))]
        internal static class PlayerAnimation_ShotgunAimCancel
        {
            internal static void Postfix(PlayerAnimation __instance)
            {
                if (__instance.m_State != PlayerAnimation.State.FromAiming) return;
                if (!MaybeLatchShotgunAnimator(__instance, "AimCancel")) return;
                ShotgunAim.Request(__instance, ShotgunAim.Direction.Lower);
            }
        }


        [HarmonyPatch(typeof(PlayerAnimation), nameof(PlayerAnimation.Trigger_Generic_Fire))]
        internal static class PlayerAnimation_ShotgunFire
        {
            internal static void Postfix(PlayerAnimation __instance)
            {
                if (__instance.m_State != PlayerAnimation.State.Firing) return;
                if (!MaybeLatchShotgunAnimator(__instance, "Fire")) return;
                ShotgunAim.Interrupt(__instance, "fire");
                Main.Instance.Configuration.Refs.ModdedAnimator.SetTrigger("Trigger_Generic_Fire");
            }
        }


        [HarmonyPatch(typeof(PlayerAnimation), nameof(PlayerAnimation.Trigger_Generic_Reload))]
        internal static class PlayerAnimation_ShotgunReload
        {
            internal static void Prefix(PlayerAnimation __instance, out PlayerAnimation.State __state)
            {
                __state = __instance.m_State;
            }

            internal static void Postfix(PlayerAnimation __instance, int bulletsToReload, int roundsLoaded, bool willJam, PlayerAnimation.State __state)
            {
                if (__instance.m_State != PlayerAnimation.State.Reloading) return;
                if (!MaybeLatchShotgunAnimator(__instance, "Reload")) return;
                ShotgunAim.Interrupt(__instance, "reload");

                GearItem weaponInHands = GameManager.GetPlayerManagerComponent()?.m_ItemInHands;
                GunItem gunItem = weaponInHands?.m_GunItem;
                int spentCasings = gunItem != null ? gunItem.m_SpentCasingsInClip : 0;
                bool isAmmoSwitch = gunItem != null && gunItem.m_PreviousAmmoIndex >= 0;

                Animator moddedAnimator = Main.Instance.Configuration.Refs.ModdedAnimator;
                moddedAnimator.SetInteger("Reload_Count", bulletsToReload);
                moddedAnimator.SetInteger("Rounds_Loaded", roundsLoaded);
                moddedAnimator.SetInteger("SpentCasingsInClip", spentCasings);
                moddedAnimator.SetBool("Is_Loaded", roundsLoaded > 0);
                moddedAnimator.SetBool("IsAmmoSwitch", isAmmoSwitch);
                moddedAnimator.SetBool("Is_Jammed", willJam);
                moddedAnimator.SetBool("IsAimHeld", __state == PlayerAnimation.State.Aiming);
                moddedAnimator.SetBool("AimConfirmed", false);
                moddedAnimator.SetTrigger("Trigger_Generic_Reload_Single");
            }
        }


        [HarmonyPatch(typeof(PlayerAnimation), nameof(PlayerAnimation.IsAllowedToFire))]
        internal static class PlayerAnimation_IsAllowedToFire_ShotgunOverride
        {
            internal static bool Prefix(PlayerAnimation __instance, bool allowHipFire, ref bool __result)
            {
                if (__instance.m_EquippedFirstPersonWeaponShoulder == null) return true;
                if (!__instance.m_EquippedFirstPersonWeaponShoulder.name.StartsWith(ShotgunFPHPrefabName)) return true;

                if (DoubleFire.FiringSecondBarrel)
                {
                    __result = true;
                }
                else if (allowHipFire)
                {
                    int state = (int)__instance.m_State;
                    int blocked = (1 << (int)PlayerAnimation.State.Equipping) | (1 << (int)PlayerAnimation.State.Dequipping);
                    __result = ((1 << state) & blocked) == 0;
                }
                else
                {
                    __result = __instance.m_State == PlayerAnimation.State.Aiming;
                }
                return false;
            }
        }


        [HarmonyPatch(typeof(vp_FPSWeapon), nameof(vp_FPSWeapon.PlayFireAnimation))]
        internal static class vp_FPSWeapon_PlayFireAnimation
        {
            internal static bool Prefix(vp_FPSWeapon __instance)
            {
                if (__instance.m_GunItem == null) return true;
                if (__instance.m_GunItem.m_GunType != GunTypeCustom) return true;
                ApplyShotgunRecoil();
                return true;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddGear))]
        internal static class Inventory_NoShotgunBooksWithoutSkills
        {
            private static bool Prefix(GearItem gi)
            {
                if (ShotgunSkill.IsAvailable || gi == null || !ShotgunSkill.IsShotgunBook(gi.gameObject)) return true;
                UnityEngine.Object.Destroy(gi.gameObject);
                return false;
            }
        }


        [HarmonyPatch(typeof(GearItem), nameof(GearItem.ManualStart))]
        internal static class GearItem_RemoveShotgunBooksWithoutSkills
        {
            private static void Postfix(GearItem __instance)
            {
                if (ShotgunSkill.IsAvailable || !ShotgunSkill.IsShotgunBook(__instance.gameObject)) return;
                UnityEngine.Object.Destroy(__instance.gameObject);
            }
        }


        [HarmonyPatch(typeof(ResearchItem), nameof(ResearchItem.OnResearchComplete))]
        internal static class Patch_ResearchItem_OnResearchComplete
        {
            private static bool Prefix(ResearchItem __instance)
            {
                if (__instance == null) return true;
                if (!ShotgunSkill.IsShotgunBook(__instance.gameObject)) return true;

                __instance.m_ElapsedHours = __instance.m_TimeRequirementHours;
                if (!ShotgunSkill.IsAvailable || ShotgunSkill.IncreaseSkillPoints == null) return false;

                float multiplier = 1f;
                Feat_BookSmarts bookSmarts = FeatsManager.m_Feat_BookSmarts;
                if (bookSmarts != null && bookSmarts.IsUnlockedAndEnabled()) multiplier += bookSmarts.m_PercentBenefit / 100f;

                int points = (int)Math.Round(__instance.m_SkillPoints * multiplier);
                ShotgunSkill.IncreaseSkillPoints.Invoke(ShotgunSkill.SkillName, points);
                return false;
            }
        }


        [HarmonyPatch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.OnRead))]
        internal static class Panel_Inventory_Examine_PatchOnRead
        {
            private static bool Prefix(Panel_Inventory_Examine __instance)
            {
                GearItem gi = __instance.m_GearItem;
                if (gi == null) return true;
                if (!ShotgunSkill.IsShotgunBook(gi.gameObject)) return true;

                ResearchItem ri = gi.m_ResearchItem;
                if (ri == null) return true;

                if (ri.m_ElapsedHours >= ri.GetTimeRequirementHoursPrecise())
                {
                    GameAudioManager.PlayGUIError();
                    return false;
                }

                int currentTier = ShotgunSkill.GetCurrentTier();
                if (currentTier >= ri.m_NoBenefitAtSkillLevel - 1)
                {
                    GameAudioManager.PlayGUIError();
                    ri.m_ElapsedHours = ri.m_TimeRequirementHours;
                    return false;
                }

                if (__instance.MaybeAbortReadingWithHUDMessage())
                {
                    GameAudioManager.PlayGUIError();
                    return false;
                }

                GameAudioManager.PlayGuiConfirm();
                __instance.SelectWindow(__instance.m_ActionInProgressWindow);
                __instance.SetReadInProgress(true);

                UISlider slider = __instance.m_Slider_ActionProgress;
                if (slider == null)
                    return false;

                slider.value = 0f;
                __instance.m_ElapsedProgressBarSeconds = 0f;
                __instance.m_ProgressBarTimeSeconds = __instance.m_ReadTimeSeconds;

                int minutesRemaining = 0;
                if (ri.m_ElapsedHours < ri.GetTimeRequirementHoursPrecise())
                    minutesRemaining = Mathf.RoundToInt((ri.GetTimeRequirementHours() - ri.m_ElapsedHours) * 60f);

                int minutesToAdvance = Math.Min(__instance.m_HoursToRead * 60, minutesRemaining);
                __instance.AccelerateTimeOfDay(minutesToAdvance, false);

                GameObject emitter = InterfaceManager.GetSoundEmitter();
                __instance.m_ProgressBarAudio = GameAudioManager.PlaySound(ri.m_ReadAudio, emitter);
                __instance.SetInventoryAlpha(0f);

                return false;
            }
        }


        [HarmonyPatch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.RefreshReadPanel))]
        internal static class Panel_Inventory_Examine_PatchRefreshReadPanel
        {
            private static void Postfix(Panel_Inventory_Examine __instance)
            {
                ShotgunSkill.ApplyIconOverlay(__instance.m_ResearchIcon);
                GearItem gi = __instance.m_GearItem;
                if (gi == null) return;
                if (!ShotgunSkill.IsShotgunBook(gi.gameObject)) return;

                ResearchItem ri = gi.m_ResearchItem;
                if (ri == null) return;
                if (!ShotgunSkill.IsAvailable) return;

                (int points, int tier) s = ShotgunSkill.GetPointsAndTiers(ShotgunSkill.SkillName);

                if (__instance.m_SkillProgressBarLabel != null)
                    __instance.m_SkillProgressBarLabel.text = Localization.Get("GAMEPLAY_DoubleBarrel_SkillName");

                if (__instance.m_SkillLevelLabel != null)
                    __instance.m_SkillLevelLabel.text = (s.tier + 1).ToString();

                Skill skill = ShotgunSkill.GetSkillObject();
                if (__instance.m_SkillProgressBar != null)
                    __instance.m_SkillProgressBar.value = skill != null ? skill.GetProgressToNextLevelAsNormalizedValue(0) : 0f;

                if (__instance.m_SkillProgressBarSprite != null)
                    __instance.m_SkillProgressBarSprite.fillAmount = skill != null ? Mathf.Clamp01(skill.GetProgressToNextLevelAsNormalizedValue(ri.m_SkillPoints)) : 0f;

                if (__instance.m_ResearchIcon != null)
                {
                    __instance.m_ResearchIcon.spriteName = ShotgunSkill.IconId;
                    ShotgunSkill.ApplyIconOverlay(__instance.m_ResearchIcon);
                }
            }
        }


        [HarmonyPatch(typeof(SprainedWrist), nameof(SprainedWrist.CanEquipItem))]
        internal static class SprainedWrist_BlockShotgunLikeRifle
        {
            private static void Postfix(SprainedWrist __instance, GearItem gi, ref SprainedWrist.CanEquipItemResult __result)
            {
                if (__result != SprainedWrist.CanEquipItemResult.Success) return;
                if (gi == null || gi.m_GunItem == null) return;
                if (gi.m_GunItem.m_GunType != GunTypeCustom) return;
                if (__instance.m_CausesLocIDs == null || __instance.m_CausesLocIDs.Count < 1) return;
                __result = SprainedWrist.CanEquipItemResult.CannotEquip2HandedWeapon;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsGearItemGunOfType))]
        internal static class Inventory_IsGearItemGunOfType_ShotgunCyclesWithRifles
        {
            private static void Postfix(GearItem gi, GunType targetType, ref bool __result)
            {
                if (__result) return;
                if (targetType != GunType.Rifle) return;
                if (gi == null || gi.m_GunItem == null) return;
                if (gi.m_GunItem.m_GunType != GunTypeCustom) return;
                if (gi.m_GunItem.m_ClipSize == 0) return;
                __result = true;
            }
        }

        [HarmonyPatch(typeof(ConsoleManager), nameof(ConsoleManager.Initialize))]
        internal class ConsoleManagerPatches_Initialize
        {
            private static bool mRegistered;

            private static void Postfix()
            {
                if (mRegistered) return;
                mRegistered = true;
                uConsole.RegisterCommand("shotgun_levelup", new Action(() =>
                {
                    ShotgunSkill.IncreaseSkillPoints.Invoke(ShotgunSkill.SkillName, 25);
                }));
            }
        }
    }
}
