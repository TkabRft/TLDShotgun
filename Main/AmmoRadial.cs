using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Shotgun
{
    internal static class AmmoRadial
    {
        internal static int PendingAmmoIndex { get; private set; } = -1;
        internal static bool IsOpen { get; private set; }

        private static float mPressStartTime = -1f;
        private static bool mOpenedThisPress;
        private static readonly Dictionary<string, Sprite> mIcons = new();
        private static readonly List<ArmLayout> mResizedArms = new();

        private struct ArmLayout
        {
            internal UITexture Texture;
            internal int Width;
            internal int Height;
            internal Rect UvRect;
            internal UIWidget.AspectRatioSource KeepAspectRatio;
        }

        internal static void Tick(GunItem gunItem)
        {
            KeyCode key = Settings.Instance.AmmoSwapKeyCode;
            bool hasContext = InputManager.HasContext(InputManager.m_CurrentContext);

            if (Input.GetKeyDown(key) && hasContext && gunItem.IsEquipped())
            {
                mPressStartTime = Time.unscaledTime;
                mOpenedThisPress = false;
                return;
            }
            if (mPressStartTime < 0f) return;

            if (Input.GetKey(key))
            {
                if (!mOpenedThisPress && Time.unscaledTime - mPressStartTime >= Settings.Instance.AmmoRadialHoldSeconds)
                {
                    mOpenedThisPress = true;
                    TryOpen(gunItem);
                }
                return;
            }

            bool wasTap = !mOpenedThisPress;
            mPressStartTime = -1f;
            mOpenedThisPress = false;
            if (wasTap && hasContext && gunItem.IsEquipped())
            {
                PendingAmmoIndex = -1;
                Patches.TryBeginAmmoSwap(gunItem);
            }
        }

        internal static void ClearPending() => PendingAmmoIndex = -1;

        private static bool CanSwap(GunItem gunItem) => gunItem.CanReload() && !gunItem.m_IsJammed;

        private static void TryOpen(GunItem gunItem)
        {
            if (!CanSwap(gunItem)) return;
            if (gunItem.GetNextValidAmmoIndex() == gunItem.m_SelectedAmmoIndex) return;
            Panel_ActionsRadial panel = InterfaceManager.GetPanel<Panel_ActionsRadial>();
            if (panel == null || panel.IsEnabled() || panel.IsRadialDisabled()) return;

            panel.m_Queue.Clear();
            panel.m_Queue.Add((Il2CppSystem.Action)(System.Action)(() => Populate(panel, gunItem)));
            Populate(panel, gunItem);
            panel.DoFirstOpen();
            IsOpen = true;
        }

        private static void Populate(Panel_ActionsRadial panel, GunItem gunItem)
        {
            Inventory inventory = GameManager.GetInventoryComponent();
            if (inventory == null) return;
            Panel_ActionsRadial.RadialInfo template = FindTemplate(panel);

            RestoreArms();
            panel.Enable(true, false);
            Il2CppSystem.Collections.Generic.IList<GearItemData> validAmmo = gunItem.ValidAmmo;
            int armCount = panel.m_RadialArms.Length;
            for (int i = 0, iMax = validAmmo.Cast<Il2CppSystem.Collections.Generic.ICollection<GearItemData>>().Count; i < iMax && panel.m_NextArmToAdd < armCount; i++)
            {
                GearItemData ammo = validAmmo[i];
                int count = inventory.GetAmmoAvailableForWeapon(ammo);
                bool greyOut = i == gunItem.m_SelectedAmmoIndex || count <= 0;
                int index = i;

                panel.AddRadialSelection(CopyInfo(template), (Il2CppSystem.Action)(System.Action)(() => Select(gunItem, index)), greyOut, false);
                RadialMenuArm arm = panel.m_RadialArms[panel.m_NextArmToAdd - 1];
                string ammoType = GetAmmoType(ammo);
                Sprite icon = GetIcon(ammoType);
                if (icon != null) ApplyIcon(arm, icon, greyOut);
                arm.m_NameWhenHoveredOver = ammo.DisplayName;
                if (arm.m_StackLabel != null) arm.m_StackLabel.text = count.ToString();
            }

            for (int i = 0; i < armCount; i++) panel.m_RadialArms[i].SetHoverColor(false);
        }

        private static Panel_ActionsRadial.RadialInfo FindTemplate(Panel_ActionsRadial panel)
        {
            foreach (Panel_ActionsRadial.RadialInfo info in panel.m_PrimaryRadial)
            {
                if (info != null && info.m_RadialElement == Panel_ActionsRadial.RadialType.Weapons) return info;
            }
            return panel.m_PrimaryRadial[0];
        }

        private static Panel_ActionsRadial.RadialInfo CopyInfo(Panel_ActionsRadial.RadialInfo template)
        {
            return new Panel_ActionsRadial.RadialInfo
            {
                m_RadialElement = Panel_ActionsRadial.RadialType.Weapons,
                m_SpriteName = template.m_SpriteName,
                m_SpriteNameHover = template.m_SpriteNameHover,
                m_PixelSize = template.m_PixelSize,
                m_GreyOutSpriteName = template.m_GreyOutSpriteName,
                m_GreyOutPixelSize = template.m_GreyOutPixelSize,
                m_IconActiveColor = template.m_IconActiveColor,
                m_IconActiveHoverColor = template.m_IconActiveHoverColor,
            };
        }

        private static string GetAmmoType(GearItemData ammo)
        {
            foreach (string ammoType in Configuration.ShotgunAmmoTypes)
            {
                if (ammo.name.EndsWith(ammoType)) return ammoType;
            }
            return null;
        }

        private static Sprite GetIcon(string ammoType)
        {
            if (ammoType == null) return null;
            if (!mIcons.TryGetValue(ammoType, out Sprite sprite) || sprite == null)
            {
                sprite = Addressables.LoadAssetAsync<Sprite>($"ico_DoubleBarrel{ammoType}HUD").WaitForCompletion();
                mIcons[ammoType] = sprite;
            }
            return sprite;
        }

        private static void ApplyIcon(RadialMenuArm arm, Sprite icon, bool greyOut)
        {
            arm.OverrideSpriteWithGearIcon(icon.texture, greyOut, string.Empty);
            UITexture texture = arm.m_Texture;
            if (texture == null) return;

            mResizedArms.Add(new ArmLayout { Texture = texture, Width = texture.width, Height = texture.height, UvRect = texture.uvRect, KeepAspectRatio = texture.keepAspectRatio });
            Rect rect = icon.textureRect;
            texture.uvRect = new Rect(rect.x / icon.texture.width, rect.y / icon.texture.height, rect.width / icon.texture.width, rect.height / icon.texture.height);
            texture.keepAspectRatio = UIWidget.AspectRatioSource.Free;
            texture.width = Mathf.Max(1, Mathf.RoundToInt(texture.height * rect.width / rect.height));
        }

        private static void RestoreArms()
        {
            foreach (ArmLayout layout in mResizedArms)
            {
                if (layout.Texture == null) continue;
                layout.Texture.keepAspectRatio = layout.KeepAspectRatio;
                layout.Texture.uvRect = layout.UvRect;
                layout.Texture.width = layout.Width;
                layout.Texture.height = layout.Height;
            }
            mResizedArms.Clear();
        }

        private static void Select(GunItem gunItem, int index)
        {
            if (gunItem == null || !gunItem.IsEquipped()) return;
            if (index == gunItem.m_SelectedAmmoIndex) return;
            Inventory inventory = GameManager.GetInventoryComponent();
            if (inventory == null || inventory.GetAmmoAvailableForWeapon(gunItem.ValidAmmo[index]) <= 0) return;
            if (!CanSwap(gunItem)) return;

            PendingAmmoIndex = index;
            if (!Patches.TryBeginAmmoSwap(gunItem)) PendingAmmoIndex = -1;
        }


        [HarmonyPatch(typeof(GunItem), nameof(GunItem.GetNextValidAmmoIndex))]
        internal static class GunItem_PickedAmmoIsNext
        {
            internal static void Postfix(GunItem __instance, ref int __result)
            {
                if (__instance.m_GunType != GunTypeCustom) return;
                if (PendingAmmoIndex < 0) return;
                __result = PendingAmmoIndex;
            }
        }


        [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetRadialButtonHeldDown))]
        internal static class InputManager_AmmoKeyHoldsRadial
        {
            internal static void Postfix(ref bool __result)
            {
                if (__result || !IsOpen) return;
                if (Input.GetKey(Settings.Instance.AmmoSwapKeyCode)) __result = true;
            }
        }


        [HarmonyPatch(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.Enable), new Type[] { typeof(bool) })]
        internal static class Panel_ActionsRadial_TrackAmmoRadialClosed
        {
            internal static void Postfix(bool enable)
            {
                if (enable) return;
                IsOpen = false;
                RestoreArms();
            }
        }
    }
}
