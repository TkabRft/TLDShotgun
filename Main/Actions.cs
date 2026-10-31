global using static Shotgun.Actions;
using Il2Cpp;
using MelonLoader;
using Shotgun.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Shotgun
{
    internal class Actions
    {

        internal static void SyncAmmoParams(GunItem gunItem)
        {
            if (gunItem == null || gunItem.m_Clip == null) return;
            Animator moddedAnimator = Main.Instance?.Configuration?.Refs?.ModdedAnimator;
            if (moddedAnimator == null) return;
            moddedAnimator.SetInteger("Rounds_Loaded", gunItem.m_Clip.Count);
            moddedAnimator.SetInteger("SpentCasingsInClip", gunItem.m_SpentCasingsInClip);
        }

        internal static void UpdateShotgunParams(GunItem gun, string ammoName)
        {
            if (gun == null) return;
            if (gun.m_GunType != GunTypeCustom) return;
            vp_FPSShooter shooter = Main.Instance.Configuration.Refs.Shotgun.WeaponView.FPSShooter;
            shooter.ProjectileCount = 1;
            shooter.ProjectileSpread = 0f;
            var definition = ShotgunAmmoCatalog.Find(ammoName);
            if (definition == null) return;
            var stats = definition.Read(Settings.Instance);
            gun.m_DamageHP = stats.Damage;
            gun.m_AccuracyRange = stats.Range;
        }


        internal static void ResetShotgunTriggers(Animator moddedAnimator)
        {
            if (moddedAnimator == null) return;
            moddedAnimator.ResetTrigger("Trigger_Generic_Equip");
            moddedAnimator.ResetTrigger("Trigger_Generic_Unequip");
            moddedAnimator.ResetTrigger("Trigger_Generic_Aim");
            moddedAnimator.ResetTrigger("Trigger_Generic_Aim_Cancel");
            moddedAnimator.ResetTrigger("Trigger_Generic_Fire");
            moddedAnimator.ResetTrigger("Trigger_Generic_Reload_Single");
        }

        internal static bool TryGetSelectedAmmoReserve(GunItem gunItem, out int reserve)
        {
            reserve = 0;
            Inventory inventory = GameManager.GetInventoryComponent();
            if (inventory == null)
            {
                MelonLogger.Error("TryGetSelectedAmmoReserve: no Inventory while the shotgun is equipped; reload not started");
                return false;
            }
            GearItem gearItem = gunItem.GetComponent<GearItem>();
            if (gearItem == null)
            {
                MelonLogger.Error("TryGetSelectedAmmoReserve: shotgun GunItem has no GearItem; reload not started");
                return false;
            }
            reserve = inventory.GetAmmoAvailableForWeapon(gearItem);
            return true;
        }

        internal static void ReloadPastVanillaAmmoGate()
        {
            vp_FPSShooter shooter = GameManager.GetVpFPSCamera()?.m_CurrentShooter;
            if (shooter == null)
            {
                MelonLogger.Error("ReloadPastVanillaAmmoGate: no current vp_FPSShooter while the shotgun is equipped; reload not started");
                return;
            }
            vp_FPSPlayer player = GameManager.GetVpFPSPlayer();
            if (player == null)
            {
                MelonLogger.Error("ReloadPastVanillaAmmoGate: no vp_FPSPlayer while the shotgun is equipped; reload not started");
                return;
            }

            bool started = shooter.Reload(0);
            if (player.m_ReenableWeaponStatesTimer != null) player.m_ReenableWeaponStatesTimer.Cancel();

            if (!started)
            {
                MelonLogger.Error("ReloadPastVanillaAmmoGate: vp_FPSShooter.Reload refused although GunItem.CanReload held");
                return;
            }

            player.MaybeCancelZoomInternal();
            player.SetState("Reload", true);
        }

        internal static void SwitchToNextAmmoType(GunItem gunItem)
        {
            if (gunItem == null) return;
            gunItem.m_SelectedAmmoIndex = gunItem.GetNextValidAmmoIndex();
            gunItem.m_PreviousAmmoIndex = -1;
            AmmoRadial.ClearPending();
            UpdateShotgunParams(gunItem, gunItem.ValidAmmo[gunItem.m_SelectedAmmoIndex].DisplayName);
            CustomAmmoHUD.Refresh();
        }

        internal static void TransmitLocomotion(PlayerMovement __instance)
        {
            Animator moddedAnimator = Main.Instance?.Configuration?.Refs?.ModdedAnimator;
            if (moddedAnimator == null || !moddedAnimator.enabled) return;
            ShotgunSkill.ReapplyAnimatorParamsIfTierChanged(moddedAnimator);

            bool sprint = InputManager.GetSprintDown(__instance);
            Vector2 move = InputManager.GetPlayerMovement(__instance);
            bool hasInput = move.sqrMagnitude > 0.0001f;
            Transform playerTransform = GameManager.GetPlayerTransform();
            float speed = 0f;

            if (playerTransform != null && Time.deltaTime > 0f)
            {
                if (Main.Instance.LastPosition != Vector3.zero)
                {
                    Vector3 d = playerTransform.position - Main.Instance.LastPosition;
                    d.y = 0f;
                    speed = d.magnitude / Time.deltaTime;
                }
                Main.Instance.LastPosition = playerTransform.position;
            }

            moddedAnimator.SetFloat("Movement_Speed", speed, 0.10f, Time.deltaTime);
            moddedAnimator.SetBool("Movement_Input", hasInput);
            moddedAnimator.SetBool("Sprint_Input", sprint);
            moddedAnimator.SetFloat("Idle_Speed_Multiplier", Settings.Instance.IdleSpeedMulitplier);
            moddedAnimator.SetFloat("Walk_Speed_Multiplier", Settings.Instance.BaseWalkingSpeedMulitplier * Mathf.Lerp(1.0f, Settings.Instance.MaxWalkingSpeedMulitplier, 1.0f - ((Settings.Instance.MaxWalkingSpeed - speed) / (Settings.Instance.MaxWalkingSpeed))));
            moddedAnimator.SetFloat("Sprint_Speed_Multiplier", Settings.Instance.BaseSprintingSpeedMulitplier * Mathf.Lerp(1.0f, Settings.Instance.MaxSprintingSpeedMulitplier, 1.0f - ((Settings.Instance.MaxSprintingSpeed - speed) / (Settings.Instance.MaxSprintingSpeed - Settings.Instance.MaxWalkingSpeed))));
            moddedAnimator.SetBool("Fast_Enough_For_Sprinting", speed >= Settings.Instance.MaxWalkingSpeed);

            // move to settings post-set, this is spamming here
            Main.Instance.IdleVariation.TimeScale = Settings.Instance.IdleVariationSpeed;
            Main.Instance.IdleVariation.MinSegmentDuration = Settings.Instance.IdleVariationMinInterval;
            Main.Instance.IdleVariation.MaxSegmentDuration = Settings.Instance.IdleVariationMaxInterval;
            Main.Instance.IdleVariation.MinTargetDistance = Settings.Instance.IdleVariationMinTargetDistance;
            Main.Instance.IdleVariation.MaxTargetDistance = Settings.Instance.IdleVariationMaxTargetDistance;
            Main.Instance.IdleVariation.OutputMin = Settings.Instance.IdleVariationOutputMin;
            Main.Instance.IdleVariation.OutputMax = Settings.Instance.IdleVariationOutputMax;
            Main.Instance.IdleVariation.Tick();

            moddedAnimator.SetFloat("Idle_Variation", Main.Instance.IdleVariation.Value);
            TickRareIdle(moddedAnimator, hasInput);
        }

        private static readonly int IdleStateHash = Animator.StringToHash("Idle");
        private static float mIdleSeconds;
        private static float mNextRareIdleSeconds = -1f;

        private static float NextRareIdleDelay()
        {
            float min = Settings.Instance.RareIdleMinSeconds;
            return UnityEngine.Random.Range(min, Mathf.Max(min, Settings.Instance.RareIdleMaxSeconds));
        }

        private static void TickRareIdle(Animator moddedAnimator, bool hasInput)
        {
            PlayerAnimation playerAnimation = GameManager.GetPlayerAnimationComponent();
            bool resting = !hasInput
                && playerAnimation != null && playerAnimation.m_State == PlayerAnimation.State.Showing
                && !moddedAnimator.IsInTransition(0)
                && moddedAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == IdleStateHash;
            if (!resting)
            {
                mIdleSeconds = 0f;
                return;
            }
            if (mNextRareIdleSeconds < 0f) mNextRareIdleSeconds = NextRareIdleDelay();
            mIdleSeconds += Time.deltaTime;
            if (mIdleSeconds < mNextRareIdleSeconds) return;

            moddedAnimator.SetTrigger("Idle_Rare");
            mIdleSeconds = 0f;
            mNextRareIdleSeconds = NextRareIdleDelay();
        }


        internal static bool MaybeLatchShotgunAnimator(PlayerAnimation instance, string callerName = "")
        {
            Animator vanillaAnimator = Main.Instance.Configuration.Refs.VanillaAnimator;
            if (vanillaAnimator == null)
            {
                return false;
            }

            Animator moddedAnimator = Main.Instance.Configuration.Refs.ModdedAnimator;
            if (moddedAnimator == null)
            {
                vanillaAnimator.enabled = true;
                return false;
            }

            if (instance.m_EquippedFirstPersonWeaponShoulder == null)
            {
                moddedAnimator.enabled = false;
                vanillaAnimator.enabled = true;
                return false;
            }
            Animator shotgunAnimator = instance.m_EquippedFirstPersonWeaponShoulder.m_Animator;

            if (shotgunAnimator == null)
            {
                moddedAnimator.enabled = false;
                vanillaAnimator.enabled = true;
                return false;
            }

            bool isShotgun = instance.m_EquippedFirstPersonWeaponShoulder.name.StartsWith(ShotgunFPHPrefabName);
            bool wasModdedEnabled = moddedAnimator.enabled;

            bool renamed = false;
            if (isShotgun && instance.m_EquippedFirstPersonWeaponShoulder.gameObject.name != ShotgunFPHPrefabName)
            {
                instance.m_EquippedFirstPersonWeaponShoulder.gameObject.name = ShotgunFPHPrefabName;
                renamed = true;
            }

            moddedAnimator.enabled = isShotgun;
            vanillaAnimator.enabled = !isShotgun;
            shotgunAnimator.enabled = !isShotgun;

            if (!isShotgun) return false;
            if (wasModdedEnabled && !renamed) return true;

            if (wasModdedEnabled) moddedAnimator.Rebind();
            SyncAmmoParams(GunItemInHands());
            ShotgunSkill.ApplyAnimatorParams(moddedAnimator);
            return true;
        }

        internal static void ApplyShotgunRecoil()
        {
            float pitchMax = Settings.Instance.RecoilMaxPitchKick;
            float yawMax = Settings.Instance.RecoilMaxYawKick;
            if (pitchMax <= 0f && yawMax <= 0f) return;

            int tier = ShotgunSkill.GetCurrentTier();
            float factor = Mathf.Clamp01(1f - (tier / 4f));
            if (factor <= 0f) return;

            MelonCoroutines.Start(RecoilRoutine(pitchMax * factor, UnityEngine.Random.Range(-yawMax, yawMax) * factor));
        }

        private static System.Collections.IEnumerator RecoilRoutine(float pitchTotal, float yawTotal)
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null) yield break;

            float riseDur = Mathf.Max(0.001f, Settings.Instance.RecoilRiseTime);
            float recFrac = Mathf.Clamp01(Settings.Instance.RecoilRecoveryFraction);
            float recDur = recFrac > 0f ? Mathf.Max(0.001f, Settings.Instance.RecoilRecoveryTime) : 0f;
            float total = riseDur + recDur;

            float elapsed = 0f;
            float pitchApplied = 0f;
            float yawApplied = 0f;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;

                float shape;
                if (elapsed <= riseDur)
                {
                    float u = Mathf.Clamp01(elapsed / riseDur);
                    shape = 1f - (1f - u) * (1f - u);
                }
                else if (recDur > 0f)
                {
                    float v = Mathf.Clamp01((elapsed - riseDur) / recDur);
                    float s = v * v * (3f - 2f * v);
                    shape = 1f - recFrac * s;
                }
                else
                {
                    shape = 1f;
                }

                float pitchTarget = pitchTotal * shape;
                cam.m_Pitch += pitchTarget - pitchApplied;
                pitchApplied = pitchTarget;

                float yawTarget = yawTotal * shape;
                cam.AddToYaw(yawTarget - yawApplied);
                yawApplied = yawTarget;

                yield return null;
            }
        }
    }
}
