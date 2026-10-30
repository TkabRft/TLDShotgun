using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Shotgun
{
    internal static class ShotgunSkill
    {
        internal const string SkillName = "Shotgun";
        internal static int[] TierTresholds = new int[] { 0, 25, 50, 75, 100 };

        #region FiligranisSkills Compat

        private static Assembly mFSAssembly;
        private static Type mFSkillType;

        internal delegate (int points, int tier) GetPointsAndTiersDelegate(string skillName);
        internal static GetPointsAndTiersDelegate GetPointsAndTiers { get; private set; }

        internal delegate void IncreaseSkillPointsDelegate(string skillName, int points);
        internal static IncreaseSkillPointsDelegate IncreaseSkillPoints { get; private set; }

        internal static bool IsAvailable => mFSkillType != null && GetPointsAndTiers != null;

        internal static void Init()
        {
            mFSAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FiligranisSkills");
            Type fskillType = mFSAssembly?.GetType("FiligranisSkills.FiligranisSkills");
            if (fskillType == null)
            {
                MelonLogger.Warning("FiligranisSkills not found, Shotgun skill will not be available");
                return;
            }

            mFSkillType = fskillType;

            MethodInfo methodInfo = AccessTools.FirstMethod(mFSkillType, mi => mi.Name == nameof(GetPointsAndTiers));
            if (methodInfo == null) MelonLogger.Error($"GetPointsAndTiers method not found in {mFSkillType.Name}");
            else GetPointsAndTiers = methodInfo.CreateDelegate<GetPointsAndTiersDelegate>();

            methodInfo = AccessTools.FirstMethod(mFSkillType, mi => mi.Name == nameof(IncreaseSkillPoints));
            if (methodInfo == null) MelonLogger.Error($"IncreaseSkillPoints method not found in {mFSkillType.Name}");
            else IncreaseSkillPoints = methodInfo.CreateDelegate<IncreaseSkillPointsDelegate>();

            TryRegisterSkill();
        }

        /// <summary>
        /// OK, she IS removing the ammo properly at high level - it seems to be an issue of maybe not having the reload itself trigger properly? WTF
        /// Somehow the post-switch reload is not working. This is actually a very good sign, it means our previous fix hasn't changed the bug manifestation itself. We're just chasing a tail end of it :D
        /// </summary>
        private static void TryRegisterSkill()
        {
            if (mFSkillType == null) return;

            MethodInfo registerMethod = AccessTools.Method(mFSkillType, "RegisterSkill");
            if (registerMethod == null)
            {
                MelonLogger.Error("RegisterSkill method not found");
                return;
            }

            Type skillDefType = mFSAssembly.GetType("FiligranisSkills.SkillDefinition");
            ConstructorInfo constructor = AccessTools.Constructor(skillDefType, new Type[]
            {
                typeof(string),
                typeof(LocalizedString),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(LocalizedString[]),
                typeof(LocalizedString[]),
                typeof(int[]),
                typeof(UnityEngine.Texture2D)
            });

            object skillDef = constructor.Invoke(new object[]
            {
                SkillName,
                new LocalizedString { m_LocalizationID = "GAMEPLAY_DoubleBarrel_SkillName" },
                IconId,
                "",
                "DoubleBarrel_Skill_Graphic",
                TierStrings("Description"),
                TierStrings("Benefits"),
                new int[] { 0, 25, 50, 75, 100 },
                null
            });

            registerMethod.Invoke(null, new[] { skillDef });
            MelonLogger.Msg("Shotgun skill registered with FiligranisSkills");
        }

        private static LocalizedString[] TierStrings(string suffix) =>
            Enumerable.Range(1, TierTresholds.Length).Select(i => new LocalizedString { m_LocalizationID = $"GAMEPLAY_DoubleBarrel_SkillTier{i}{suffix}" }).ToArray();

        internal const string IconId = "ico_DoubleBarrel_Skill";
        private const string IconOverlayName = "ShotgunSkillIconTexture";
        private static Texture2D mIconTexture;

        internal static void ApplyIconOverlay(UISprite sprite)
        {
            if (sprite == null) return;
            bool show = sprite.spriteName == IconId;
            Transform overlay = sprite.transform.Find(IconOverlayName);
            if (overlay != null)
            {
                overlay.gameObject.SetActive(show);
                return;
            }
            if (!show) return;
            if (mIconTexture == null) mIconTexture = Addressables.LoadAssetAsync<Texture2D>(IconId).WaitForCompletion();
            if (mIconTexture == null) return;

            GameObject go = new(IconOverlayName) { layer = sprite.gameObject.layer };
            go.transform.SetParent(sprite.transform, false);
            UITexture texture = go.AddComponent<UITexture>();
            texture.mainTexture = mIconTexture;
            texture.rawPivot = sprite.pivot;
            texture.color = sprite.color;
            texture.depth = sprite.depth + 1;
            texture.height = sprite.height;
            texture.width = Mathf.Max(1, Mathf.RoundToInt(sprite.height * (float)mIconTexture.width / mIconTexture.height));
            go.transform.localPosition = Vector3.zero;
        }

        [HarmonyPatch(typeof(Panel_Log), nameof(Panel_Log.RefreshSkillsList))]
        internal static class Panel_Log_ShotgunIcon
        {
            private static void Postfix(Panel_Log __instance)
            {
                if (__instance.m_SkillsDisplayList == null) return;
                for (int i = 0; i < __instance.m_SkillsDisplayList.Count; i++)
                    ApplyIconOverlay(__instance.m_SkillsDisplayList[i]?.m_SkillIcon);
            }
        }

        #endregion

        #region Stat Calculation

        internal static int DefaultAnimationTier = 3;

        internal static int GetCurrentTier()
        {
            if (!IsAvailable) return DefaultAnimationTier;
            (int points, int tier) skill = GetPointsAndTiers(SkillName);
            return skill.tier;
        }

        internal static int TierForPoints(int points)
        {
            int t = 0;
            for (int i = 0; i < TierTresholds.Length; i++) if (points >= TierTresholds[i]) t = i;
            return t;
        }

        internal const string BookPrefabPrefix = "GEAR_DoubleBarrelBook";

        internal static bool IsShotgunBook(GameObject go)
        {
            if (go == null) return false;
            string name = go.name;
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith(BookPrefabPrefix);
        }

        internal static Skill GetSkillObject()
        {
            SkillsManager mgr = GameManager.GetSkillsManager();
            if (mgr == null || mgr.m_Skills == null) return null;
            string goName = "Skill_" + SkillName;
            for (int i = 0; i < mgr.m_Skills.Count; i++)
            {
                Skill sk = mgr.m_Skills[i];
                if (sk != null && sk.name == goName) return sk;
            }
            return null;
        }

        internal static void ApplyToGunItem(GunItem gunItem)
        {

        }

        
        internal static long LastSkillUpTime;

        internal static void MaybeIncreaseSkill()
        {
            if (DateTime.Now.Ticks - LastSkillUpTime >= TimeSpan.TicksPerSecond)
            {
                LastSkillUpTime = DateTime.Now.Ticks;
                IncreaseSkillPoints?.Invoke(SkillName, 1);
            }
        }

        #endregion

        #region Animation Skill Gates

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

        internal static void ReapplyAnimatorParamsIfTierChanged(Animator animator)
        {
            if (AnimationTier == mAppliedTier) return;
            ApplyAnimatorParams(animator);
        }

        internal static void ApplyAnimatorParams(Animator animator)
        {
            if (animator == null) return;
            mAppliedTier = AnimationTier;
            animator.SetBool("CanCatchLeftOnAmmoSwitch", CanCatchLeftOnAmmoSwitch);
            animator.SetBool("CanCloseBarrelToAim", CanCloseBarrelToAim);
            animator.SetInteger("ManualRemoveSpeed", ManualRemoveSpeed);
            animator.SetInteger("InsertSpeed", InsertSpeed);
            animator.SetInteger("RecoilHandlingLevel", RecoilHandlingLevel);
            animator.SetFloat("Reload_Speed_Multiplier", Settings.Instance.BaseReloadSpeedMultiplier + ((AnimationTier * Settings.Instance.AdditionalReloadSpeedMultiplier) * 0.01f));
            animator.SetFloat("Aim_Speed_Multiplier", Settings.Instance.BaseAimSpeedMultiplier + ((AnimationTier * Settings.Instance.AdditionalAimSpeedMultiplier) * 0.01f));
            animator.SetFloat("Equip_Speed_Multiplier", Settings.Instance.BaseEquipSpeedMultiplier + ((AnimationTier * Settings.Instance.AdditionalEquipSpeedMultiplier) * 0.01f));
        }

        #endregion
    }
}
