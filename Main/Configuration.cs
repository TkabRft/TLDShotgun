using Il2Cpp;
using Il2CppTLD.Gameplay;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine;
using Shotgun.Utility;
using Harmony;
using ModComponent.API.Components;
using System.Runtime.CompilerServices;
using Il2CppTLD.Gear;
using Il2CppSystem.Collections.Generic;

namespace Shotgun
{
    internal class Configuration
    {
        internal ConfigurationReferences Refs { get; set; }

        internal bool PrepareShotgun()
        {
            string stage = "Get input references";
            try
            {
                ConfigurationReferences configRefs = new();
                Refs = configRefs;
                if (!Get(configRefs)) return false;
                stage = "Resolve maintenance inputs";
                configRefs.Maintenance = ShotgunMaintenance.Resolve(configRefs.Rifle.Gun.Prefab);
                stage = "Instantiate weapon components";
                if (!Instantiate(configRefs)) return false;
                stage = "Configure weapon components";
                if (!Configure(configRefs)) return false;
                stage = "Register first-person weapon";
                if (!RegisterShotgun(configRefs)) return false;
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during PrepareShotgun [{stage}]: {e}");
                return false;
            }
        }

        internal bool RegisterShotgun(ConfigurationReferences refs)
        {
            if (!refs.FPSCamera.m_Weapons.TryAdd(refs.Shotgun.Floating.FPSItem, refs.Shotgun.WeaponView.FPSWeapon))
            {
                MelonLogger.Error("Failed to add shotgun to vpFPSCamera.m_weapons dictionary");
                return false;
            }
            MelonLogger.Msg("Registered shotgun first-person weapon");
            return true;
        }


        internal static bool Attempt(Action action, Func<bool> test, string errorMessage, [CallerMemberName] string methodName = "")
        {
            MelonLogger.Msg(methodName);
            action.Invoke();
            if (!test.Invoke())
            {
                MelonLogger.Error($"[CONFIG FAIL]: {errorMessage}");
                return false;
            }
            return true;
        }


        #region Get



        internal bool Get(ConfigurationReferences refs)
        {
            if (!GetTopLevel(refs)) return false;
            if (!GetRifle(refs)) return false;
            if (!GetShotgun(refs)) return false;
            return true;
        }


        internal bool GetTopLevel(ConfigurationReferences refs)
        {
            if (!GetFPSPlayer(refs)) return false;
            if (!GetFPSCamera(refs)) return false;
            return true;
        }


        internal static readonly string[] ShotgunAmmoTypes = ShotgunAmmoCatalog.Definitions.Select(d => d.Name).ToArray();

        internal bool GetShotgun(ConfigurationReferences refs)
        {
            if (!GetShotgunPrefab(refs)) return false;
            if (!GetShotgunGearItem(refs)) return false;
            if (!GetShotgunModComponent(refs)) return false;
            if (!GetShotgunCasing(refs)) return false;
            foreach (var ammoType in ShotgunAmmoTypes)
            {
                refs.Shotgun.AmmoTypes[ammoType] = new ShotgunAmmoTypeRefs();
                if (!GetShotgunAmmoSingle(refs, ammoType)) return false;
                if (!GetShotgunAmmoBox(refs, ammoType)) return false;
            }
            return true;
        }


        internal bool GetRifle(ConfigurationReferences refs)
        {
            if (!GetRifleAsset(refs)) return false;
            if (!GetRifleFirstPersonItem(refs)) return false;
            if (!GetRifleFPSItem(refs)) return false;
            if (!GetRifleVPFPSRoot(refs)) return false;
            if (!GetRifleVPFPSWeapon(refs)) return false;
            if (!GetRifleVPFPShooter(refs)) return false;
            if (!GetRifleFPH(refs)) return false;
            if (!GetRifleFirstPersonWeapon(refs)) return false;
            if (!GetRifleAnimator(refs)) return false;
            if (!GetRifleGearItem(refs)) return false;
            if (!GetRifleGunItem(refs)) return false;
            if (!GetRifleAmmoSingle(refs)) return false;
            if (!GetRifleAmmoBox(refs)) return false;
            if (!GetRifleCasing(refs)) return false;
            return true;
        }


        internal bool GetRifleAmmoSingle(ConfigurationReferences refs)
        {
            if (!GetRifleAmmoSingleAsset(refs)) return false;
            if (!GetRifleAmmoSingleGearItem(refs)) return false;
            if (!GetRifleAmmoSingleAmmoItem(refs)) return false;
            if (!GetRifleAmmoSingleStackableItem(refs)) return false;
            if (!GetRifleAmmoSingleHarvest(refs)) return false;
            return true;
        }


        internal bool GetRifleAmmoBox(ConfigurationReferences refs)
        {
            if (!GetRifleAmmoBoxAsset(refs)) return false;
            if (!GetRifleAmmoBoxGearItem(refs)) return false;
            if (!GetRifleAmmoBoxAmmoItem(refs)) return false;
            if (!GetRifleAmmoBoxStackableItem(refs)) return false;
            if (!GetRifleAmmoBoxHarvest(refs)) return false;
            return true;
        }

        internal bool GetRifleCasing(ConfigurationReferences refs)
        {
            if (!GetRifleCasingAsset(refs)) return false;
            if (!GetRifleCasingGearItem(refs)) return false;
            if (!GetRifleCasingStackableItem(refs)) return false;
            return true;
        }


        internal bool GetShotgunAmmoSingle(ConfigurationReferences refs, string ammoType)
        {
            if (!GetShotgunAmmoSingleAsset(refs, ammoType)) return false;
            if (!GetShotgunAmmoSingleGearItem(refs, ammoType)) return false;
            if (!GetShotgunAmmoSingleAmmoItem(refs, ammoType)) return false;
            if (!GetShotgunAmmoSingleStackableItem(refs, ammoType)) return false;
            if (!GetShotgunAmmoSingleHarvest(refs, ammoType)) return false;
            return true;
        }


        internal bool GetShotgunAmmoBox(ConfigurationReferences refs, string ammoType)
        {
            if (!GetShotgunAmmoBoxAsset(refs, ammoType)) return false;
            if (!GetShotgunAmmoBoxGearItem(refs, ammoType)) return false;
            if (!GetShotgunAmmoBoxAmmoItem(refs, ammoType)) return false;
            if (!GetShotgunAmmoBoxStackableItem(refs, ammoType)) return false;
            if (!GetShotgunAmmoBoxHarvest(refs, ammoType)) return false;
            return true;
        }

        internal bool GetShotgunCasing(ConfigurationReferences refs)
        {
            if (!GetShotgunCasingAsset(refs)) return false;
            if (!GetShotgunCasingGearItem(refs)) return false;
            if (!GetShotgunCasingStackableItem(refs)) return false;
            return true;
        }


        internal bool GetRifleAmmoSingleAsset(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoSingle.Prefab = Addressables.LoadAssetAsync<GameObject>("GEAR_RifleAmmoSingle").WaitForCompletion(), () => refs.Rifle.AmmoSingle.Prefab != null, "Null GEAR_RifleAmmoSingle");
        internal bool GetRifleAmmoSingleGearItem(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoSingle.GearItem = refs.Rifle.AmmoSingle.Prefab.GetComponent<GearItem>(), () => refs.Rifle.AmmoSingle.GearItem != null, "Null GEAR_RifleAmmoSingle.GearItem");
        internal bool GetRifleAmmoSingleAmmoItem(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoSingle.AmmoItem = refs.Rifle.AmmoSingle.Prefab.GetComponent<AmmoItem>(), () => refs.Rifle.AmmoSingle.AmmoItem != null, "Null GEAR_RifleAmmoSingle.AmmoItem");
        internal bool GetRifleAmmoSingleStackableItem(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoSingle.StackableItem = refs.Rifle.AmmoSingle.Prefab.GetComponent<StackableItem>(), () => refs.Rifle.AmmoSingle.StackableItem != null, "Null GEAR_RifleAmmoSingle.StackableItem");
        // Vanilla ammunition's disassembly component is optional for shotgun setup.
        internal bool GetRifleAmmoSingleHarvest(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoSingle.Harvest = refs.Rifle.AmmoSingle.Prefab.GetComponent<Harvest>(), () => refs.Rifle.AmmoSingle.GearItem != null, "Null GEAR_RifleAmmoSingle.GearItem");

        internal bool GetRifleAmmoBoxAsset(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoBox.Prefab = Addressables.LoadAssetAsync<GameObject>("GEAR_RifleAmmoBox").WaitForCompletion(), () => refs.Rifle.AmmoBox.Prefab != null, "Null GEAR_RifleAmmoBox");
        internal bool GetRifleAmmoBoxGearItem(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoBox.GearItem = refs.Rifle.AmmoBox.Prefab.GetComponent<GearItem>(), () => refs.Rifle.AmmoBox.GearItem != null, "Null GEAR_RifleAmmoBox.GearItem");
        internal bool GetRifleAmmoBoxAmmoItem(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoBox.AmmoItem = refs.Rifle.AmmoBox.Prefab.GetComponent<AmmoItem>(), () => refs.Rifle.AmmoBox.AmmoItem != null, "Null GEAR_RifleAmmoBox.AmmoItem");
        internal bool GetRifleAmmoBoxStackableItem(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoBox.StackableItem = refs.Rifle.AmmoBox.Prefab.GetComponent<StackableItem>(), () => refs.Rifle.AmmoBox.StackableItem != null, "Null GEAR_RifleAmmoBox.StackableItem");
        internal bool GetRifleAmmoBoxHarvest(ConfigurationReferences refs) => Attempt(() => refs.Rifle.AmmoBox.Harvest = refs.Rifle.AmmoBox.Prefab.GetComponent<Harvest>(), () => refs.Rifle.AmmoBox.GearItem != null, "Null GEAR_RifleAmmoBox.GearItem");
        
        internal bool GetRifleCasingAsset(ConfigurationReferences refs) => Attempt(() => refs.Rifle.Casing.Prefab = Addressables.LoadAssetAsync<GameObject>("GEAR_RifleAmmoCasing").WaitForCompletion(), () => refs.Rifle.Casing.Prefab != null, "Null GEAR_RifleAmmoCasing");
        internal bool GetRifleCasingGearItem(ConfigurationReferences refs) => Attempt(() => refs.Rifle.Casing.GearItem = refs.Rifle.Casing.Prefab.GetComponent<GearItem>(), () => refs.Rifle.Casing.GearItem != null, "Null GEAR_RifleAmmoCasing.GearItem");
        internal bool GetRifleCasingStackableItem(ConfigurationReferences refs) => Attempt(() => refs.Rifle.Casing.StackableItem = refs.Rifle.Casing.Prefab.GetComponent<StackableItem>(), () => refs.Rifle.Casing.StackableItem != null, "Null GEAR_RifleAmmoCasing.StackableItem");

        internal bool GetShotgunAmmoSingleAsset(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Prefab = Addressables.LoadAssetAsync<GameObject>($"GEAR_DoubleBarrelAmmoSingle{ammoType}").WaitForCompletion(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Prefab != null, $"Null GEAR_DoubleBarrelAmmoSingle{ammoType}");
        internal bool GetShotgunAmmoSingleGearItem(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.GearItem = refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Prefab.GetComponent<GearItem>(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.GearItem != null, $"Null GEAR_DoubleBarrelAmmoSingle{ammoType}.GearItem");
        internal bool GetShotgunAmmoSingleAmmoItem(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.AmmoItem = refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Prefab.GetComponent<AmmoItem>(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.AmmoItem != null, $"Null GEAR_DoubleBarrelAmmoSingle{ammoType}.AmmoItem");
        internal bool GetShotgunAmmoSingleStackableItem(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.StackableItem = refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Prefab.GetComponent<StackableItem>(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.StackableItem != null, $"Null GEAR_DoubleBarrelAmmoSingle{ammoType}.StackableItem");
        // ModComponent maps ModHarvestableBehaviour to Harvest, not Harvestable.
        internal bool GetShotgunAmmoSingleHarvest(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Harvest = refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Prefab.GetComponent<Harvest>(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Harvest != null, $"Null GEAR_DoubleBarrelAmmoSingle{ammoType}.Harvest");

        internal bool GetShotgunAmmoBoxAsset(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Prefab = Addressables.LoadAssetAsync<GameObject>($"GEAR_DoubleBarrelAmmoBox{ammoType}").WaitForCompletion(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Prefab != null, $"Null GEAR_DoubleBarrelAmmoBox{ammoType}");
        internal bool GetShotgunAmmoBoxGearItem(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.GearItem = refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Prefab.GetComponent<GearItem>(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.GearItem != null, $"Null GEAR_DoubleBarrelAmmoBox{ammoType}.GearItem");
        internal bool GetShotgunAmmoBoxAmmoItem(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.AmmoItem = refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Prefab.GetComponent<AmmoItem>(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.AmmoItem != null, $"Null GEAR_DoubleBarrelAmmoBox{ammoType}.AmmoItem");
        internal bool GetShotgunAmmoBoxStackableItem(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.StackableItem = refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Prefab.GetComponent<StackableItem>(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.StackableItem != null, $"Null GEAR_DoubleBarrelAmmoBox{ammoType}.StackableItem");
        internal bool GetShotgunAmmoBoxHarvest(ConfigurationReferences refs, string ammoType) => Attempt(() => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Harvest = refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Prefab.GetComponent<Harvest>(), () => refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Harvest != null, $"Null GEAR_DoubleBarrelAmmoBox{ammoType}.Harvest");

        internal bool GetShotgunCasingAsset(ConfigurationReferences refs) => Attempt(() => refs.Shotgun.Casing.Prefab = Addressables.LoadAssetAsync<GameObject>($"GEAR_DoubleBarrelAmmoCasing").WaitForCompletion(), () => refs.Shotgun.Casing.Prefab != null, $"Null GEAR_DoubleBarrelAmmoCasing");
        internal bool GetShotgunCasingGearItem(ConfigurationReferences refs) => Attempt(() => refs.Shotgun.Casing.GearItem = refs.Shotgun.Casing.Prefab.GetComponent<GearItem>(), () => refs.Shotgun.Casing.GearItem != null, $"Null GEAR_DoubleBarrelAmmoCasing.GearItem");
        internal bool GetShotgunCasingStackableItem(ConfigurationReferences refs) => Attempt(() => refs.Shotgun.Casing.StackableItem = refs.Shotgun.Casing.Prefab.GetComponent<StackableItem>(), () => refs.Shotgun.Casing.StackableItem != null, $"Null GEAR_DoubleBarrelAmmoCasing.StackableItem");

        internal bool GetFPSPlayer(ConfigurationReferences refs)
        {
            refs.FPSPlayer = GameManager.GetVpFPSPlayer();
            if (refs.FPSPlayer == null) MelonLogger.Error("Could not summon vp_FPSPlayer");
            return refs.FPSPlayer != null;
        }


        internal bool GetFPSCamera(ConfigurationReferences refs)
        {
            refs.FPSCamera = refs.FPSPlayer?.FPSCamera ?? null;
            if (refs.FPSCamera == null) MelonLogger.Error("Could not get vp_FPSCamera from vp_FPSPlayer instance");
            return refs.FPSCamera != null;
        }

        internal bool GetRifleAsset(ConfigurationReferences refs)
        {
            refs.Rifle.Gun.Prefab = Addressables.LoadAssetAsync<GameObject>("GEAR_Rifle").WaitForCompletion();
            if (refs.Rifle.Gun.Prefab == null)
            {
                MelonLogger.Error("Could not summon rifle prefab");
                return false;
            }
            return true;
        }


        internal bool GetRifleFirstPersonItem(ConfigurationReferences refs)
        {
            refs.Rifle.Gun.FirstPersonItem = refs.Rifle.Gun.Prefab.GetComponent<FirstPersonItem>();
            if (refs.Rifle.Gun.FirstPersonItem == null)
            {
                MelonLogger.Error("Could not find rifle first person item");
                return false;
            }
            return true;
        }


        internal bool GetRifleFPSItem(ConfigurationReferences refs)
        {
            refs.Rifle.Floating.FPSItem = refs.Rifle.Gun.FirstPersonItem.m_ItemData;
            if (refs.Rifle.Gun.FirstPersonItem == null)
            {
                MelonLogger.Error("Could not find rifle first person item");
                return false;
            }
            return true;
        }

        internal bool GetRifleVPFPSRoot(ConfigurationReferences refs)
        {
            foreach (var kvp in refs.FPSCamera.m_Weapons)
            {
                if (kvp.Value.name.Contains("Rifle"))
                {
                    refs.Rifle.WeaponView.Root = kvp.Value.gameObject;
                    break;
                }
            }
            if (refs.Rifle.WeaponView.Root == null)
            {
                MelonLogger.Error("Could not find rifle vp_FPSWeapon script amongst all of vp_FPSCamera.m_Weapons");
                return false;
            }
            return true;
        }


        internal bool GetRifleVPFPSWeapon(ConfigurationReferences refs)
        {
            refs.Rifle.WeaponView.FPSWeapon = refs.Rifle.WeaponView.Root.GetComponent<vp_FPSWeapon>();
            if (refs.Rifle.WeaponView.FPSWeapon == null)
            {
                MelonLogger.Error("Could not find rifle vp_FPSWeapon");
                return false;
            }
            return true;
        }


        internal bool GetRifleVPFPShooter(ConfigurationReferences refs)
        {
            refs.Rifle.WeaponView.FPSShooter = refs.Rifle.WeaponView.Root.GetComponent<vp_FPSShooter>();
            if (refs.Rifle.WeaponView.FPSWeapon == null)
            {
                MelonLogger.Error("Could not find rifle vp_FPSShooter");
                return false;
            }
            return true;
        }


        internal bool GetRifleFPH(ConfigurationReferences refs)
        {
            refs.Rifle.FPH.Root = refs.Rifle.WeaponView.FPSWeapon.m_FirstPersonWeaponShoulder.gameObject;
            if (refs.Rifle.FPH.Root == null)
            {
                MelonLogger.Error("Could not find rifle FPH root from rifle vp_FPSWeapon");
                return false;
            }
            return true;
        }


        internal bool GetRifleFirstPersonWeapon(ConfigurationReferences refs)
        {
            refs.Rifle.FPH.FirstPersonWeapon = refs.Rifle.FPH.Root.GetComponent<FirstPersonWeapon>();
            if (refs.Rifle.FPH.FirstPersonWeapon == null)
            {
                MelonLogger.Error("Could not find rifle first person weapon from rifle FPH root");
                return false;
            }
            return true;
        }


        internal bool GetRifleAnimator(ConfigurationReferences refs)
        {
            refs.Rifle.FPH.Animator = refs.Rifle.FPH.Root.GetComponent<Animator>();
            if (refs.Rifle.FPH.Animator == null)
            {
                MelonLogger.Error("Could not find rifle animator from rifle FPH root");
                return false;
            }
            return true;
        }


        internal bool GetRifleGunItem(ConfigurationReferences refs)
        {
            refs.Rifle.Gun.GunItem = refs.Rifle.Gun.Prefab.GetComponent<GunItem>();
            if (refs.Rifle.Gun.GearItem == null)
            {
                MelonLogger.Error("Could not find rifle GunItem from rifle prefab");
                return false;
            }
            return true;
        }

        internal bool GetRifleGearItem(ConfigurationReferences refs)
        {
            refs.Rifle.Gun.GearItem = refs.Rifle.Gun.Prefab.GetComponent<GearItem>();
            if (refs.Rifle.Gun.GearItem == null)
            {
                MelonLogger.Error("Could not find rifle gearitem from rifle prefab");
                return false;
            }
            return true;
        }


        internal bool GetShotgunPrefab(ConfigurationReferences refs)
        {
            refs.Shotgun.Gun.Prefab = Addressables.LoadAssetAsync<GameObject>("GEAR_DoubleBarrel").WaitForCompletion();
            if (refs.Shotgun.Gun.Prefab == null) MelonLogger.Error("Could not summon GEAR_DoubleBarrel");
            return refs.Shotgun.Gun.Prefab != null;
        }


        internal bool GetShotgunGearItem(ConfigurationReferences refs)
        {
            refs.Shotgun.Gun.GearItem = refs.Shotgun.Gun.Prefab?.GetComponent<GearItem>();
            if (refs.Shotgun.Gun.GearItem == null) MelonLogger.Error("Null Shotgun GearItem");
            return refs.Shotgun.Gun.GearItem != null;
        }


        internal bool GetShotgunModComponent(ConfigurationReferences refs)
        {
            refs.Shotgun.Gun.ModComponent = refs.Shotgun.Gun.Prefab?.GetComponent<ModGenericComponent>();
            if (refs.Shotgun.Gun.GearItem == null) MelonLogger.Error("Null Shotgun GearItem");
            return refs.Shotgun.Gun.GearItem != null;
        }


        #endregion


        #region Instantiate

        internal bool Instantiate(ConfigurationReferences refs)
        {
            if (!InstantiateTopLevel(refs)) return false;
            if (!InstantiateShotgun(refs)) return false;
            return true;
        }


        internal bool InstantiateTopLevel(ConfigurationReferences refs)
        {
            //nothing top level to instantiate yet, so far I've been able to do everything with vanilla logic.
            return true;
        }


        internal bool InstantiateShotgun(ConfigurationReferences refs)
        {
            if (!InstantiateShotgunFPH(refs)) return false;
            if (!InstantiateShotgunFirstPersonWeapon(refs)) return false;
            if (!InstantiateShotgunAnimator(refs)) return false;
            if (!InstantiateModdedAnimator(refs)) return false;
            if (!InstantiateShotgunWeaponGroup(refs)) return false;
            if (!InstantiateShotgunVPFPSWeapon(refs)) return false;
            if (!InstantiateShotgunVPFPSShooter(refs)) return false;
            if (!InstantiateShotgunStruggleBonus(refs)) return false;
            if (!InstantiateShotgunCleanable(refs)) return false;
            if (!InstantiateShotgunGunItem(refs)) return false;
            if (!InstantiateShotgunFirstPersonItem(refs)) return false;
            if (!InstantiateShotgunFPSItem(refs)) return false;
            return true;
        }


        internal bool InstantiateShotgunStruggleBonus(ConfigurationReferences refs)
        {
            refs.Shotgun.Gun.StruggleBonus = refs.Shotgun.Gun.Prefab?.GetOrCreateComponent<StruggleBonus>() ?? null;
            if (refs.Shotgun.Gun.StruggleBonus == null)
            {
                MelonLogger.Error("Null StruggleBonus");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunCleanable(ConfigurationReferences refs)
        {
            refs.Shotgun.Gun.Cleanable = refs.Shotgun.Gun.Prefab?.GetOrCreateComponent<Cleanable>() ?? null;
            if (refs.Shotgun.Gun.Cleanable == null)
            {
                MelonLogger.Error("Null Cleanable");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunGunItem(ConfigurationReferences refs)
        {
            refs.Shotgun.Gun.GunItem = refs.Shotgun.Gun.Prefab?.GetOrCreateComponent<GunItem>() ?? null;
            if (refs.Shotgun.Gun.GunItem == null)
            {
                MelonLogger.Error("Null GunItem");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunFirstPersonItem(ConfigurationReferences refs)
        {
            refs.Shotgun.Gun.FirstPersonItem = refs.Shotgun.Gun.Prefab?.GetOrCreateComponent<FirstPersonItem>() ?? null;
            if (refs.Shotgun.Gun.FirstPersonItem == null)
            {
                MelonLogger.Error("Null FirstPersonItem");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunWeaponGroup(ConfigurationReferences refs)
        {
            refs.Shotgun.WeaponView.Root = new GameObject();
            refs.Shotgun.WeaponView.Root.transform.SetParent(refs.FPSCamera.m_WeaponParent.transform, false);
            return true;
        }


        internal bool InstantiateShotgunVPFPSWeapon(ConfigurationReferences refs)
        {
            refs.Shotgun.WeaponView.FPSWeapon = refs.Shotgun.WeaponView.Root.GetOrCreateComponent<vp_FPSWeapon>();
            if (refs.Shotgun.WeaponView.FPSWeapon == null)
            {
                MelonLogger.Error("Null vp_FPSWeapon");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunVPFPSShooter(ConfigurationReferences refs)
        {
            refs.Shotgun.WeaponView.FPSShooter = refs.Shotgun.WeaponView.Root.GetOrCreateComponent<vp_FPSShooter>();
            if (refs.Shotgun.WeaponView.FPSShooter == null)
            {
                MelonLogger.Error("Null vp_FPSShooter");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunFPSItem(ConfigurationReferences refs)
        {
            refs.Shotgun.Floating.FPSItem = ScriptableObject.CreateInstance<FPSItem>();
            if (refs.Shotgun.Floating.FPSItem == null)
            {
                MelonLogger.Error("Null FPSItem");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunFPH(ConfigurationReferences refs)
        {
            refs.Shotgun.FPH.Root = Addressables.LoadAssetAsync<GameObject>("FPH_DoubleBarrel").WaitForCompletion();
            if (refs.Shotgun.FPH.Root == null)
            {
                MelonLogger.Error("Could not summon FPH_DoubleBarrel");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunFirstPersonWeapon(ConfigurationReferences refs)
        {
            refs.Shotgun.FPH.FirstPersonWeapon = refs.Shotgun.FPH.Root.GetOrCreateComponent<FirstPersonWeapon>();
            if (refs.Shotgun.FPH.FirstPersonWeapon == null)
            {
                MelonLogger.Error("Could not attach FirstPersonWeapon to FPH_DoubleBarrel");
                return false;
            }
            return true;
        }


        internal bool InstantiateShotgunAnimator(ConfigurationReferences refs)
        {
            refs.Shotgun.FPH.Animator = refs.Shotgun.FPH.Root.GetOrCreateComponent<Animator>();
            if (refs.Shotgun.FPH.FirstPersonWeapon == null)
            {
                MelonLogger.Error("Could not attach Animator to FPH_DoubleBarrel");
                return false;
            }
            return true;
        }


        internal bool InstantiateModdedAnimator(ConfigurationReferences refs)
        {
            Animator playerAnimator = GameManager.GetPlayerAnimationComponent().m_Animator;
            if (playerAnimator == null)
            {
                MelonLogger.Error("Could not find player animator for sub-animator placement");
                return false;
            }

            refs.VanillaAnimator = playerAnimator;

            Transform gameDataChild = null;
            for (int i = 0, iMax = playerAnimator.gameObject.transform.childCount; i < iMax; i++)
            {
                Transform child = playerAnimator.gameObject.transform.GetChild(i);
                if (child.gameObject.name.Contains("GAME_DATA"))
                {
                    gameDataChild = child;
                    break;
                }
            }

            if (gameDataChild == null)
            {
                MelonLogger.Error("Could not find GAME_DATA child of player animator");
                return false;
            }

            refs.ModdedAnimator = gameDataChild.gameObject.GetOrCreateComponent<Animator>();
            return true;
        }

        #endregion


        #region Configure

        private bool Configure(ConfigurationReferences refs)
        {
            if (!ConfigureTopLevel(refs)) return false;
            if (!ConfigureShotgun(refs)) return false;
            return true;
        }


        internal bool ConfigureTopLevel(ConfigurationReferences refs)
        {
            if (!ConfigureRadial(refs)) return false;
            return true;
        }


        internal bool ConfigureShotgun(ConfigurationReferences refs)
        {
            string stage = "Configure ammunition";
            void Step(string name, Action action) { stage = name; action(); }
            try
            {
                foreach (var ammoType in ShotgunAmmoTypes)
                {
                    Step($"AmmoSingle {ammoType}", () => ConfigureShotgunAmmoSingle(refs, ammoType));
                    Step($"AmmoBox {ammoType}", () => ConfigureShotgunAmmoBox(refs, ammoType));
                    Step($"AmmoCasing {ammoType}", () => ConfigureShotgunAmmoCasing(refs, ammoType));
                }
                Step(nameof(ConfigureFirearmBans), () => ConfigureFirearmBans(refs));
                Step(nameof(ConfigureShotgunGearItem), () => ConfigureShotgunGearItem(refs));
                Step(nameof(ConfigureShotgunGunItem), () => ConfigureShotgunGunItem(refs));
                Step(nameof(ConfigureShotgunFirstPersonItem), () => ConfigureShotgunFirstPersonItem(refs));
                Step(nameof(ConfigureShotgunStruggleBonus), () => ConfigureShotgunStruggleBonus(refs));
                Step(nameof(ConfigureShotgunMaintenance), () => ConfigureShotgunMaintenance(refs));
                Step(nameof(ConfigureShotgunFPSItem), () => ConfigureShotgunFPSItem(refs));
                Step(nameof(ConfigureShotgunFPHRoot), () => ConfigureShotgunFPHRoot(refs));
                Step(nameof(ConfigureShotgunFirstPersonWeapon), () => ConfigureShotgunFirstPersonWeapon(refs));
                Step(nameof(ConfigureShotgunAnimator), () => ConfigureShotgunAnimator(refs));
                Step(nameof(ConfigureModdedAnimator), () => ConfigureModdedAnimator(refs));
                Step(nameof(ConfigureShotgunAudio), () => ConfigureShotgunAudio(refs));
                Step(nameof(ConfigureShotgunFPSShooter), () => ConfigureShotgunFPSShooter(refs));
                Step(nameof(ConfigureShotgunFPSWeapon), () => ConfigureShotgunFPSWeapon(refs));
                Step(nameof(ConfigureShotgunFPSRoot), () => ConfigureShotgunFPSRoot(refs));
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during detailed configuration [{stage}]: {e}");
                return false;
            }
        }


        internal bool ConfigureRadial(ConfigurationReferences refs)
        {
            Panel_ActionsRadial radialActionsPanel = InterfaceManager.GetPanel<Panel_ActionsRadial>();
            if (radialActionsPanel == null)
            {
                MelonLogger.Error("Null Panel_ActionsRadial");
                return false;
            }
            for (int i = 0, iMax = radialActionsPanel.m_WeaponRadialOrder.Length; i < iMax; i++)
            {
                if (radialActionsPanel.m_WeaponRadialOrder[i] == "GEAR_DoubleBarrel")
                {
                    return true;
                }
            }
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray newWeaponFilterList = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(radialActionsPanel.m_WeaponRadialOrder.Length + 1);
            for (int i = 0, iMax = radialActionsPanel.m_WeaponRadialOrder.Length; i < iMax; i++)
            {
                newWeaponFilterList[i] = radialActionsPanel.m_WeaponRadialOrder[i];
            }
            newWeaponFilterList[^1] = "GEAR_DoubleBarrel";
            radialActionsPanel.m_WeaponRadialOrder = newWeaponFilterList;
            return true;
        }

        internal void ConfigureShotgunAmmoSingle(ConfigurationReferences refs, string ammoType)
        {
            var ammoRefs = refs.Shotgun.AmmoTypes[ammoType];
            ammoRefs.AmmoSingle.AmmoItem.m_AmmoForGunType = GunTypeCustom;
            ammoRefs.AmmoSingle.AmmoItem.m_LoadedHudIconSpriteName = "";
            ammoRefs.AmmoSingle.AmmoItem.m_EmptyHudIconSpriteName = "";
            ammoRefs.AmmoSingle.GearItem.m_GearItemData.name = $"GEAR_DoubleBarrelAmmoSingle{ammoType}";
            CustomAddressablesPatch.customAddressablePaths[$"ModdedGearItemData_DoubleBarrelAmmoSingle{ammoType}"] = ammoRefs.AmmoSingle.GearItem.m_GearItemData;
            ammoRefs.AmmoSingle.GearItem.m_AmmoItem = ammoRefs.AmmoSingle.AmmoItem;
        }


        internal void ConfigureShotgunAmmoBox(ConfigurationReferences refs, string ammoType)
        {
            var ammoRefs = refs.Shotgun.AmmoTypes[ammoType];
            ammoRefs.AmmoBox.AmmoItem.m_AmmoForGunType = GunTypeCustom;
            ammoRefs.AmmoBox.AmmoItem.m_LoadedHudIconSpriteName = "";
            ammoRefs.AmmoBox.AmmoItem.m_EmptyHudIconSpriteName = "";
            ammoRefs.AmmoBox.GearItem.m_GearItemData.name = $"GEAR_DoubleBarrelAmmoBox{ammoType}";
            CustomAddressablesPatch.customAddressablePaths[$"ModdedGearItemData_DoubleBarrelAmmoBox{ammoType}"] = ammoRefs.AmmoBox.GearItem.m_GearItemData;
            ammoRefs.AmmoBox.GearItem.m_AmmoItem = ammoRefs.AmmoBox.AmmoItem;

            CustomAmmoHUD.HUDPreset preset = new CustomAmmoHUD.HUDPreset();
            preset.LoadedSprite = Addressables.LoadAssetAsync<Sprite>($"ico_DoubleBarrel{ammoType}HUD").WaitForCompletion();
            preset.UnloadedSprite = Addressables.LoadAssetAsync<Sprite>($"ico_DoubleBarrelEmptyHUD").WaitForCompletion();
            CustomAmmoHUD.TryRegisterHUDPreset("GEAR_DoubleBarrel", $"GEAR_DoubleBarrelAmmoBox{ammoType}", preset);
        }


        internal void ConfigureShotgunAmmoCasing(ConfigurationReferences refs, string ammoType)
        {
        }


        internal static readonly string[] ShotgunPartNames = { "GEAR_DoubleBarrelPartBarrel", "GEAR_DoubleBarrelPartForend", "GEAR_DoubleBarrelPartStock", "GEAR_DoubleBarrelPartTrigger" };


        internal void ConfigureFirearmBans(ConfigurationReferences refs)
        {
            DisableObjectForGameMode rifleBan = refs.Rifle.Gun.Prefab != null ? refs.Rifle.Gun.Prefab.GetComponent<DisableObjectForGameMode>() : null;
            if (rifleBan == null)
            {
                MelonLogger.Warning("GEAR_Rifle has no DisableObjectForGameMode; shotgun items will not be banned in firearm-free modes");
                return;
            }
            System.Collections.Generic.List<GameObject> prefabs = new() { refs.Shotgun.Gun.Prefab, refs.Shotgun.Casing.Prefab };
            foreach (var ammoType in ShotgunAmmoTypes)
            {
                prefabs.Add(refs.Shotgun.AmmoTypes[ammoType].AmmoSingle.Prefab);
                prefabs.Add(refs.Shotgun.AmmoTypes[ammoType].AmmoBox.Prefab);
            }
            foreach (string part in ShotgunPartNames) prefabs.Add(Addressables.LoadAssetAsync<GameObject>(part).WaitForCompletion());
            var banTags = ShotgunAvailability.BanTags(rifleBan);
            if (banTags == null)
            {
                MelonLogger.Warning("GEAR_Rifle has no Gameplay.Gear.NoFirearms tag; shotgun items will not be banned in firearm-free modes");
                return;
            }
            int banned = 0;
            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null) continue;
                DisableObjectForGameMode ban = prefab.GetComponent<DisableObjectForGameMode>();
                if (ban == null) ban = prefab.AddComponent<DisableObjectForGameMode>();
                ban.m_DisableIfAnyActive = banTags;
                banned++;
            }
            ShotgunAvailability.ApplyToCurrentMode();
            MelonLogger.Msg($"Shotgun items banned by {ShotgunAvailability.NoShotgunTagName} and Gameplay.Gear.NoFirearms: {banned} prefabs");
        }


        internal void ConfigureShotgunGearItem(ConfigurationReferences refs)
        {
            MelonLogger.Msg("ConfigureShotgunGearItem");
            GearItem gearItem = refs.Shotgun.Gun.GearItem;
            gearItem.m_StruggleBonus = refs.Shotgun.Gun.StruggleBonus;
            gearItem.m_FirstPersonItem = refs.Shotgun.Gun.FirstPersonItem;
            gearItem.m_GunItem = refs.Shotgun.Gun.GunItem;
            gearItem.m_Inspect = refs.Shotgun.Gun.Prefab.GetComponent<Inspect>();
            gearItem.m_Cleanable = refs.Shotgun.Gun.Cleanable;
            ShotgunCondition.ConfigureInitialCondition(gearItem, refs.Rifle.Gun.GearItem);
        }


        internal void ConfigureShotgunGunItem(ConfigurationReferences refs)
        {
            GunItem gunItem = refs.Shotgun.Gun.GunItem;
            GunItem rifleGunItem = refs.Rifle.Gun.GunItem;

            gunItem.m_AmmoReferences = new Il2CppSystem.Collections.Generic.List<AssetReferenceT<Il2CppTLD.Gear.GearItemData>>();
            foreach (var ammoType in ShotgunAmmoTypes)
            {
                gunItem.m_AmmoReferences.Add(new AssetReferenceT<Il2CppTLD.Gear.GearItemData>($"ModdedGearItemData_DoubleBarrelAmmoBox{ammoType}"));
            }
            gunItem.m_GearItem = refs.Shotgun.Gun.GearItem;
            gunItem.m_CasingData = refs.Shotgun.Casing.GearItem.m_GearItemData;

            // copied from rifle - expose in modcomponent!
            gunItem.m_AmmoSpriteName = rifleGunItem.m_AmmoSpriteName;
            gunItem.m_CasingAudio = rifleGunItem.m_CasingAudio;
            gunItem.m_DryFireAudio = rifleGunItem.m_DryFireAudio;
            gunItem.m_FireAudio = rifleGunItem.m_FireAudio;
            gunItem.m_ImpactAudio = rifleGunItem.m_ImpactAudio;
            gunItem.m_UncockAudio = rifleGunItem.m_UncockAudio;
            gunItem.m_MisfireTable = rifleGunItem.m_MisfireTable;
            gunItem.m_MisfirePlayerVO = rifleGunItem.m_MisfirePlayerVO;
            gunItem.m_AimingStaminaExhaustedSound = rifleGunItem.m_AimingStaminaExhaustedSound;
            gunItem.m_GunType = GunTypeCustom;


            // preset here - expose in modcomponent!
            gunItem.m_ClipSize = 2;
            gunItem.m_RoundsToReloadPerClip = 2;
            gunItem.m_DamageHP = 125;
            gunItem.m_AccuracyRange = 35;
            gunItem.m_FiringRateSeconds = 0.35f;
            gunItem.m_MinDistanceForAimAssist = 15;
            gunItem.m_MuzzleFlash_FlashDelay = 0.1f;
            gunItem.m_MuzzleFlash_SmokeDelay = 0.15f;

            // Skill-based stats: reload, sway, hold duration
            ShotgunSkill.ApplyToGunItem(gunItem);
        }


        internal void ConfigureShotgunFirstPersonItem(ConfigurationReferences refs)
        {
            FirstPersonItem firstPersonItem = refs.Shotgun.Gun.FirstPersonItem;
            FirstPersonItem rifleFirstPersonItem = refs.Rifle.Gun.FirstPersonItem;

            firstPersonItem.m_FirstPersonObjectName = ShotgunFPSPrefabName;
            firstPersonItem.m_PlayerStateTransitions = ScriptableObject.CreateInstance<PlayerStateTransitions>();
            firstPersonItem.m_PlayerStateTransitions.m_InvalidTransitions = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<PlayerAnimation.StateFlags>(rifleFirstPersonItem.m_PlayerStateTransitions.m_InvalidTransitions.Length);
            firstPersonItem.m_PlayerStateTransitions.m_ValidTransitions = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<PlayerAnimation.StateFlags>(rifleFirstPersonItem.m_PlayerStateTransitions.m_ValidTransitions.Length);
            int i = 0;
            foreach (var transition in rifleFirstPersonItem.m_PlayerStateTransitions.m_InvalidTransitions)
            {
                firstPersonItem.m_PlayerStateTransitions.m_InvalidTransitions[i] = transition;
                i++;
            }
            i = 0;
            foreach (var transition in rifleFirstPersonItem.m_PlayerStateTransitions.m_ValidTransitions)
            {
                firstPersonItem.m_PlayerStateTransitions.m_ValidTransitions[i] = transition;
                i++;
            }
            firstPersonItem.m_ItemData = refs.Shotgun.Floating.FPSItem;
            firstPersonItem.m_WieldAudioEvent = rifleFirstPersonItem.m_WieldAudioEvent;
            firstPersonItem.m_UnwieldAudioEvent = rifleFirstPersonItem.m_UnwieldAudioEvent;
        }


        internal void ConfigureShotgunStruggleBonus(ConfigurationReferences refs)
        {
            refs.Shotgun.Gun.StruggleBonus.m_StruggleWeaponType = StruggleBonus.StruggleWeaponType.Rifle;
        }


        internal void ConfigureShotgunMaintenance(ConfigurationReferences refs) =>
            ShotgunMaintenance.Configure(refs.Shotgun.Gun.Prefab, refs.Shotgun.Gun.GearItem,
                refs.Maintenance ?? throw new InvalidOperationException("Missing preflight maintenance inputs"));


        internal void ConfigureShotgunFPSItem(ConfigurationReferences refs)
        {
            refs.Shotgun.Floating.FPSItem.name = "FPSItem_DoubleBarrel";
            refs.Shotgun.Floating.FPSItem.m_FPSWeaponPrefab = refs.Rifle.Floating.FPSItem.m_FPSWeaponPrefab;
        }

        internal void ConfigureShotgunFPHRoot(ConfigurationReferences refs)
        {
            refs.Shotgun.WeaponView.Root.name = ShotgunFPSPrefabName;
        }


        internal void ConfigureShotgunFirstPersonWeapon(ConfigurationReferences refs)
        {
            refs.Shotgun.FPH.FirstPersonWeapon.m_Renderable = refs.Shotgun.FPH.Root.transform.GetChild(0).gameObject;
            refs.Shotgun.FPH.FirstPersonWeapon.m_Animator = refs.Shotgun.FPH.Animator;
            foreach (Transform transform in refs.Shotgun.FPH.Root.GetComponentsInChildren<Transform>())
            {
                switch (transform.gameObject.name)
                {
                    case "FrontSight": refs.Shotgun.FPH.FirstPersonWeapon.m_FrontSight = transform; break;
                    case "RearSight": refs.Shotgun.FPH.FirstPersonWeapon.m_RearSight = transform; break;
                    case "BulletEmissionPoint": refs.Shotgun.FPH.FirstPersonWeapon.m_BulletEmissionPoint = transform.gameObject; break;
                }
            }         
        }


        internal void ConfigureShotgunAnimator(ConfigurationReferences refs)
        {
            refs.Shotgun.FPH.Animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }


        internal void ConfigureModdedAnimator(ConfigurationReferences refs)
        {
            RuntimeAnimatorController controller = Addressables.LoadAssetAsync<RuntimeAnimatorController>("FPH_DoubleBarrel_Controller").WaitForCompletion();
            refs.ModdedAnimator.runtimeAnimatorController = controller;
            refs.ModdedAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            refs.ModdedAnimator.enabled = false;

            refs.ModdedAnimator.gameObject.GetOrCreateComponent<AnimationEventProxy>();
        }


        internal void ConfigureShotgunAudio(ConfigurationReferences refs)
        {
            ShotgunAudio.Begin(refs);

            RegisterAudioClip(refs, "GAMEPLAY_DoubleBarrel_Discharge");
            RegisterAudioClip(refs, "GAMEPLAY_DoubleBarrel_Insert");
            RegisterAudioClip(refs, "GAMEPLAY_DoubleBarrel_Remove");
            RegisterAudioClip(refs, "GAMEPLAY_DoubleBarrel_Open");
            RegisterAudioClip(refs, "GAMEPLAY_DoubleBarrel_Close");
            RegisterAudioClip(refs, "GAMEPLAY_DoubleBarrel_ShellDrop_Hard");
        }

        internal void RegisterAudioClip(ConfigurationReferences refs, string addressableKey)
        {
            if (refs.ClipManager == null) { ShotgunAudio.Failure(addressableKey, "missing ClipManager"); return; }
            try
            {
                AudioClip clip = ModBuild.EmbeddedAudio.LoadPcm(addressableKey);
                ShotgunAudio.Register(addressableKey, clip);
            }
            catch (Exception e) { ShotgunAudio.Failure(addressableKey, e.Message); }
        }


        internal static readonly System.Collections.Generic.HashSet<string> ShotgunOwnedShooterFields = new System.Collections.Generic.HashSet<string>
        {
            "ProjectilePrefab",
            "MuzzleFlashPrefab",
            "ShellPrefab",
            "BulletEmissionLocator",
            "ProjectileCount",
            "ProjectileSpread",
            "ProjectileScale",
            "MotionPositionPause",
            "ShellScale",
            "ShellEjectSpin",
        };

        internal void ConfigureShotgunFPSShooter(ConfigurationReferences refs)
        {
            vp_FPSShooter shooter = refs.Shotgun.WeaponView.FPSShooter;
            vp_FPSShooter rifleShooter = refs.Rifle.WeaponView.FPSShooter;
            shooter.m_Weapon = refs.Shotgun.WeaponView.FPSWeapon;
            shooter.MuzzleFlashPrefab = rifleShooter.MuzzleFlashPrefab;
            shooter.MuzzleFlashScale = rifleShooter.MuzzleFlashScale;
            if (shooter.m_MuzzleFlash != null) shooter.m_MuzzleFlash.transform.localScale = rifleShooter.MuzzleFlashScale;
            shooter.ProjectilePrefab = rifleShooter.ProjectilePrefab;
            shooter.ShellPrefab = rifleShooter.ShellPrefab;
            shooter.BulletEmissionLocator = refs.Shotgun.FPH.FirstPersonWeapon.m_BulletEmissionPoint.transform;

            shooter.m_StateManager.m_States.Clear();
            foreach (var state in rifleShooter.m_StateManager.m_States)
            {
                vp_StateInfo newState = new vp_StateInfo(state.TypeName, state.Name);
                vp_ComponentPreset newPreset = new vp_ComponentPreset();
                newPreset.m_ComponentType = state.Preset.m_ComponentType;
                newPreset.m_Fields = new Il2CppSystem.Collections.Generic.List<vp_ComponentPreset.Field>();
                foreach (vp_ComponentPreset.Field field in state.Preset.m_Fields)
                {
                    string fieldName = field.m_FieldInfo.Name;
                    if (ShotgunOwnedShooterFields.Contains(fieldName))
                        continue;
                    newPreset.m_Fields.Add(new vp_ComponentPreset.Field(field.m_FieldInfo, field.Args));
                }
                newState.Preset = newPreset;
                newState.TextAsset = state.TextAsset;
                shooter.m_StateManager.m_States.Add(newState);
                if (state.Name == "Default") shooter.m_DefaultState = newState;
            }

            shooter.ProjectileScale = 1.0f;
            shooter.MotionPositionPause = 0f;
            shooter.ShellScale = 1.0f;
            shooter.ShellEjectSpin = 0f;
            shooter.enabled = true;
        }

        internal void ConfigureShotgunFPSWeapon(ConfigurationReferences refs)
        {
            refs.Shotgun.WeaponView.FPSWeapon.m_FirstPersonWeaponShoulderPrefab = refs.Shotgun.FPH.Root;
            refs.Shotgun.WeaponView.FPSWeapon.m_StateManager.m_States.Clear();
            foreach (var state in refs.Rifle.WeaponView.FPSWeapon.m_StateManager.m_States)
            {
                vp_StateInfo newState = new vp_StateInfo(state.TypeName, state.Name);
                vp_ComponentPreset newPreset = new vp_ComponentPreset();
                newPreset.m_ComponentType = state.Preset.m_ComponentType;
                newPreset.m_Fields = new Il2CppSystem.Collections.Generic.List<vp_ComponentPreset.Field>();
                foreach (vp_ComponentPreset.Field field in state.Preset.m_Fields)
                {
                    newPreset.m_Fields.Add(new vp_ComponentPreset.Field(field.m_FieldInfo, field.Args));
                }
                newState.Preset = newPreset;
                newState.TextAsset = state.TextAsset;
                refs.Shotgun.WeaponView.FPSWeapon.m_StateManager.m_States.Add(newState);
                if (state.Name == "Default") refs.Shotgun.WeaponView.FPSWeapon.m_DefaultState = newState;
            }

            refs.Shotgun.WeaponView.FPSWeapon.m_GearItem = refs.Shotgun.Gun.Prefab.GetComponent<GearItem>();
            refs.Shotgun.WeaponView.FPSWeapon.m_GunItem = refs.Shotgun.Gun.Prefab.GetComponent<GunItem>();

            refs.Shotgun.WeaponView.FPSWeapon.m_FirstPersonWeaponShoulderPrefab.layer = 23;
            refs.Shotgun.WeaponView.FPSWeapon.m_UseFirstPersonHands = true;

            refs.Shotgun.WeaponView.FPSWeapon.MaybeBuildRuntimeHierarchy();
            refs.Shotgun.WeaponView.FPSWeapon.MaybeSpawnFirstPersonWeapon();
        }

        internal void ConfigureShotgunFPSRoot(ConfigurationReferences refs)
        {
            refs.Shotgun.WeaponView.Root.transform.parent.gameObject.name = $"FPSWeapon_{ShotgunFPSPrefabName}Transform";
        }

        #endregion
    }
}
