using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using Il2CppTLD.GameplayTags;
using ModData;
using UnityEngine;

namespace Shotgun
{
    internal static class ShotgunAvailability
    {
        internal const string NoShotgunTagName = "Gameplay.Gear.NoShotgun";
        private const string NoFirearmsTagName = "Gameplay.Gear.NoFirearms";
        private const string RowName = "ShotgunsInWorld";
        private const string RowNameKey = "GAMEPLAY_DoubleBarrel_AvailabilityName";
        private const string RowDescriptionKey = "GAMEPLAY_DoubleBarrel_AvailabilityDescription";
        private const string ModDataSuffix = "ShotgunsInWorld";

        private static readonly ModDataManager mModData = new("Shotgun");
        private static GameplayTag? mNoShotgunTag;
        private static bool mIsContinuing;
        private static string? mNewGameCode;
        private static bool mNewGameAllowed = true;
        private static string? mLoadedSaveName;
        private static int mYesIndex = -1;

        internal static bool IsAllowedInCustomGame { get; private set; } = true;

        internal static bool AllowsWorldItems()
        {
            if (IsCustomGame() && !IsAllowedInCustomGame) return false;
            GameModeConfig mode = ExperienceModeManager.s_CurrentGameMode;
            if (mode?.m_ActiveTags == null) return false;
            foreach (GameplayTag tag in mode.m_ActiveTags)
                if (TagName(tag) is NoFirearmsTagName or NoShotgunTagName) return false;
            return true;
        }


        internal static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameplayTag>? BanTags(DisableObjectForGameMode rifleBan)
        {
            if (rifleBan?.m_DisableIfAnyActive == null) return null;
            GameplayTag? noFirearms = null;
            foreach (GameplayTag tag in rifleBan.m_DisableIfAnyActive)
            {
                if (TagName(tag) == NoFirearmsTagName) noFirearms = tag;
            }
            if (noFirearms is null) return null;

            mNoShotgunTag = new GameplayTag(NoShotgunTagName, noFirearms.m_TagDatabase);
            return new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameplayTag>(new[] { mNoShotgunTag, noFirearms });
        }


        internal static void ApplyToCurrentMode()
        {
            GameModeConfig mode = ExperienceModeManager.s_CurrentGameMode;
            if (mode?.m_ActiveTags == null || mNoShotgunTag is null) return;

            bool shouldBan = IsCustomGame() && !IsAllowedInCustomGame;
            int index = IndexOfNoShotgunTag(mode.m_ActiveTags);
            if (shouldBan && index < 0)
                mode.m_ActiveTags.Add(mNoShotgunTag);
            else if (!shouldBan && index >= 0)
                mode.m_ActiveTags.RemoveAt(index);
        }


        internal static void AddRow(Panel_CustomXPSetup panel)
        {
            GameObject revolverRow = panel.m_RevolversInWorldPopupList?.gameObject;
            if (revolverRow == null || revolverRow.transform.parent.Find(RowName) != null) return;

            GameObject row = UnityEngine.Object.Instantiate(revolverRow, revolverRow.transform.parent);
            row.name = RowName;
            row.transform.SetSiblingIndex(revolverRow.transform.GetSiblingIndex() + 1);
            RenameRow(row);

            int orderIndex = panel.m_CustomXPMenuItemOrder.IndexOf(revolverRow);
            if (orderIndex >= 0)
                panel.m_CustomXPMenuItemOrder.Insert(orderIndex + 1, row);
            revolverRow.transform.parent.GetComponent<UIGrid>()?.Reposition();
        }


        internal static void ShowInRow(Panel_CustomXPSetup panel, bool allowed)
        {
            ConsoleComboBox revolverCombo = panel.m_RevolversInWorldPopupList;
            ConsoleComboBox combo = RowCombo(panel);
            if (combo?.items == null || revolverCombo?.items == null || panel.m_CustomMode == null) return;

            int revolverIndex = revolverCombo.items.IndexOf(revolverCombo.value);
            mYesIndex = panel.m_CustomMode.m_RevolversInWorld ? revolverIndex : 1 - revolverIndex;
            combo.value = combo.items[allowed ? mYesIndex : 1 - mYesIndex];
        }


        internal static bool ReadRow(Panel_CustomXPSetup panel)
        {
            ConsoleComboBox combo = RowCombo(panel);
            if (combo?.items == null || mYesIndex < 0) return true;
            return combo.items.IndexOf(combo.value) == mYesIndex;
        }


        internal static void BeginContinue(Panel_CustomXPSetup panel)
        {
            mIsContinuing = true;
            mNewGameCode = null;
            mNewGameAllowed = ReadRow(panel);
        }


        internal static void EndContinue() => mIsContinuing = false;


        internal static void RememberNewGameCode(string code)
        {
            if (mIsContinuing) mNewGameCode = code;
        }


        internal static void ApplyToNewGame(string code, bool decoded)
        {
            if (!decoded || mNewGameCode == null || code != mNewGameCode) return;
            IsAllowedInCustomGame = mNewGameAllowed;
            mNewGameCode = null;
            ApplyToCurrentMode();
        }


        internal static void ResetToDefault()
        {
            IsAllowedInCustomGame = true;
            mNewGameCode = null;
        }


        internal static void Save()
        {
            if (!IsCustomGame()) return;
            mModData.Save(IsAllowedInCustomGame.ToString(), ModDataSuffix);
        }


        internal static void ForgetLoadedSave() => mLoadedSaveName = null;


        internal static void LoadOncePerSave()
        {
            string saveName = SaveGameSystem.GetCurrentSaveName();
            if (saveName == mLoadedSaveName) return;
            mLoadedSaveName = saveName;
            string stored = IsCustomGame() ? mModData.Load(ModDataSuffix) : null;
            IsAllowedInCustomGame = !bool.TryParse(stored, out bool allowed) || allowed;
        }


        private static bool IsCustomGame() => GameManager.GetExperienceModeManagerComponent()?.InCustomMode() ?? false;


        private static ConsoleComboBox? RowCombo(Panel_CustomXPSetup panel)
        {
            Transform? row = panel.m_RevolversInWorldPopupList?.transform.parent.Find(RowName);
            return row?.GetComponent<ConsoleComboBox>();
        }


        private static void RenameRow(GameObject row)
        {
            ConsoleComboBox combo = row.GetComponent<ConsoleComboBox>();
            UILabel name = combo?.m_LableName;
            if (name == null) return;
            UILocalize localize = name.GetComponent<UILocalize>();
            if (localize != null) localize.key = RowNameKey;
            name.text = Localization.Get(RowNameKey);
            if (combo.m_LocalizedToolTip != null)
                combo.m_LocalizedToolTip.m_LocalizationID = RowDescriptionKey;
        }


        private static int IndexOfNoShotgunTag(Il2CppSystem.Collections.Generic.List<GameplayTag> tags)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].m_Tag == mNoShotgunTag!.m_Tag) return i;
            }
            return -1;
        }


        private static string? TagName(GameplayTag tag)
        {
            GameplayTagInfo? info = null;
            return tag.m_TagDatabase != null && tag.m_TagDatabase.TryGetInfo(tag.m_Tag, out info) ? info?.m_FullName : null;
        }
    }
}
