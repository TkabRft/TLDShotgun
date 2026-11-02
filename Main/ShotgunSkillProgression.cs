using Il2Cpp;
using UnityEngine;

namespace Shotgun;

internal static partial class ShotgunSkill
{
    internal enum Source { None, Shotgun, Rifle }
    internal const int DefaultAnimationTier = 3;

    internal static Source ActiveSource => IsAvailable ? Source.Shotgun
        : Settings.Instance.UseRifleSkillWithoutShotgunSkill && RifleSkill != null ? Source.Rifle : Source.None;

    private static Skill_Rifle? RifleSkill => GameManager.GetSkillsManager()?.m_Skill_Rifle;

    internal static int GetCurrentTier() => ActiveSource switch
    {
        Source.Shotgun => Math.Clamp(GetPointsAndTiers(SkillName).tier, 0, 4),
        Source.Rifle => Math.Clamp(RifleSkill!.GetCurrentTierNumber(), 0, 4),
        _ => DefaultAnimationTier
    };

    internal static float ConditionDegradeScale()
    {
        if (ActiveSource == Source.None) return 1f;
        var reductions = RifleSkill?.m_ConditionDegradeOnUseReduction;
        if (reductions == null || reductions.Length != 5)
            throw new InvalidOperationException("Missing five-tier Skill_Rifle.m_ConditionDegradeOnUseReduction");
        return 1f - Math.Clamp(reductions[GetCurrentTier()], 0, 50) / 100f;
    }

    internal static void AwardHit()
    {
        switch (ActiveSource)
        {
            case Source.Shotgun:
                IncreaseSkillPoints(SkillName, 1);
                break;
            case Source.Rifle:
                GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Rifle, 1,
                    SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
                break;
            default:
                return;
        }
        RefreshCurrentWeapon();
    }

    internal static void ApplyToGunItem(GunItem? gunItem)
    {
        if (gunItem == null || gunItem.m_GunType != GunTypeCustom) return;
        ShotgunCondition.ConfigureWear(gunItem.m_GearItem);
        if (gunItem == ShotgunInHands())
            ApplyAnimatorParams(Main.Instance?.Configuration?.Refs?.ModdedAnimator);
    }

    internal static void RefreshCurrentWeapon() => ApplyToGunItem(ShotgunInHands());

    internal static int AnimationTier => GetCurrentTier();
    internal static int CatchLeftMinTier = 3;
    internal static int RemoveBothMediumMinTier = 2;
    internal static int RemoveBothFastMinTier = 3;
    internal static int InsertBothMediumMinTier = 2;
    internal static int InsertBothFastMinTier = 4;
    internal static int CloseToAimMinTier = 3;
    internal static int RecoilMedMinTier = 2;
    internal static int RecoilLowMinTier = 4;

    internal static bool CanCatchLeftOnAmmoSwitch => AnimationTier >= CatchLeftMinTier;
    internal static bool CanCloseBarrelToAim => AnimationTier >= CloseToAimMinTier;
    internal static int ManualRemoveSpeed => AnimationTier >= RemoveBothFastMinTier ? 2 : (AnimationTier >= RemoveBothMediumMinTier ? 1 : 0);
    internal static int InsertSpeed => AnimationTier >= InsertBothFastMinTier ? 2 : (AnimationTier >= InsertBothMediumMinTier ? 1 : 0);
    internal static int RecoilHandlingLevel => AnimationTier >= RecoilLowMinTier ? 2 : (AnimationTier >= RecoilMedMinTier ? 1 : 0);

    private static int mAppliedTier = -1;
    private static Animator? mAppliedAnimator;

    internal static void ReapplyAnimatorParamsIfTierChanged(Animator? animator)
    {
        if (AnimationTier == mAppliedTier && animator == mAppliedAnimator) return;
        ApplyAnimatorParams(animator);
    }

    internal static void ApplyAnimatorParams(Animator? animator)
    {
        if (animator == null) return;
        mAppliedTier = AnimationTier;
        mAppliedAnimator = animator;
        animator.SetBool("CanCatchLeftOnAmmoSwitch", CanCatchLeftOnAmmoSwitch);
        animator.SetBool("CanCloseBarrelToAim", CanCloseBarrelToAim);
        animator.SetInteger("ManualRemoveSpeed", ManualRemoveSpeed);
        animator.SetInteger("InsertSpeed", InsertSpeed);
        animator.SetInteger("RecoilHandlingLevel", RecoilHandlingLevel);
        animator.SetFloat("Reload_Speed_Multiplier", Settings.Instance.BaseReloadSpeedMultiplier + ((AnimationTier * Settings.Instance.AdditionalReloadSpeedMultiplier) * 0.01f));
        animator.SetFloat("Aim_Speed_Multiplier", Settings.Instance.BaseAimSpeedMultiplier + ((AnimationTier * Settings.Instance.AdditionalAimSpeedMultiplier) * 0.01f));
        animator.SetFloat("Equip_Speed_Multiplier", Settings.Instance.BaseEquipSpeedMultiplier + ((AnimationTier * Settings.Instance.AdditionalEquipSpeedMultiplier) * 0.01f));
    }
}
