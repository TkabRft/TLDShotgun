using Il2Cpp;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Runtime;
using Il2CppSystem;
using Il2CppTLD.Gameplay;
using Il2CppTLD.Gear;
using ModComponent.API.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Shotgun
{
    internal class ConfigurationReferences
    {
        internal vp_FPSPlayer? FPSPlayer { get; set; }
        internal vp_FPSCamera? FPSCamera { get; set; }
        internal Animator? VanillaAnimator { get; set; }
        internal Animator? ModdedAnimator { get; set; }
        internal ShotgunRefs? Shotgun { get; set; } = new();
        internal GunRefs? Rifle { get; set; } = new();
        internal AudioMgr.ClipManager? ClipManager { get; set; }
        internal AudioMgr.Shot? AudioShot { get; set; }
    }


    internal class GunRefs
    {
        internal GunPrefabRefs Gun { get; set; } = new();
        internal AmmoPrefabRefs AmmoSingle { get; set; } = new();
        internal AmmoPrefabRefs AmmoBox { get; set; } = new();
        internal CasingPrefabRefs Casing { get; set; } = new();
        internal WeaponViewRefs WeaponView { get; set; } = new();
        internal FPHRefs FPH { get; set; } = new();
        internal FloatingRefs Floating { get; set; } = new();
        internal Il2CppSystem.Collections.Generic.List<Il2CppTLD.Gear.GearItemData> ValidAmmoGearItemDatas { get; set; } = new();
    }


    internal class ShotgunRefs
    {
        internal GunPrefabRefs Gun { get; set; } = new();
        internal Dictionary<string, ShotgunAmmoTypeRefs> AmmoTypes { get; set; } = new();
        internal CasingPrefabRefs Casing { get; set; } = new();
        internal WeaponViewRefs WeaponView { get; set; } = new();
        internal FPHRefs FPH { get; set; } = new();
        internal FloatingRefs Floating { get; set; } = new();
        internal Il2CppSystem.Collections.Generic.List<Il2CppTLD.Gear.GearItemData> ValidAmmoGearItemDatas { get; set; } = new();
    }


    internal class ShotgunAmmoTypeRefs
    {
        internal AmmoPrefabRefs AmmoSingle { get; set; } = new();
        internal AmmoPrefabRefs AmmoBox { get; set; } = new();
    }


    internal class AmmoPrefabRefs
    {
        internal GameObject? Prefab { get; set; }
        internal GearItem? GearItem { get; set; }
        internal AmmoItem? AmmoItem { get; set; }
        internal StackableItem? StackableItem { get; set; }
        internal Harvest? Harvest { get; set; }
    }

    internal class CasingPrefabRefs
    {
        internal GameObject? Prefab { get; set; }
        internal GearItem? GearItem { get; set; }
        internal StackableItem? StackableItem { get; set; }
    }


    internal class GunPrefabRefs
    {
        internal GameObject? Prefab { get; set; }
        internal GearItem? GearItem { get; set; }
        internal GunItem? GunItem { get; set; }
        internal FirstPersonItem? FirstPersonItem { get; set; }
        internal StruggleBonus? StruggleBonus { get; set; }
        internal Cleanable? Cleanable { get; set; }
        internal ModGenericComponent? ModComponent { get; set; }
    }


    internal class FPHRefs
    {
        internal GameObject? Root { get; set; }
        internal FirstPersonWeapon? FirstPersonWeapon { get; set; }
        internal Animator? Animator { get; set; }
    }


    internal class WeaponViewRefs
    {
        internal GameObject? Root { get; set; }
        internal vp_FPSWeapon? FPSWeapon { get; set; }
        internal vp_FPSShooter? FPSShooter { get; set; }
    }


    internal class FloatingRefs
    {
        internal FPSItem? FPSItem { get; set; }
    }
    
}
