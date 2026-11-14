using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Shotgun;

internal static class ShotgunRifleReplacement
{
    private static readonly Dictionary<string, AsyncOperationHandle<GameObject>> Handles = new(StringComparer.Ordinal);
    private static float nextRetry;
    private static string? lastError;

    internal static bool IsCandidate(GearItem gear) => gear != null && gear.gameObject.activeInHierarchy
        && gear.name is ("GEAR_Rifle" or "GEAR_Rifle(Clone)")
        && !gear.m_InPlayerInventory && !gear.m_BeenInPlayerInventory && !gear.m_BeenInContainer
        && !gear.m_InsideContainer && !gear.IsInsideContainer() && !gear.m_BeenInspected
        && gear.m_MissionObject == null && gear.m_NarrativeCollectibleItem == null;

    private static GameObject Prefab(string name)
    {
        if (!Handles.TryGetValue(name, out var handle))
        {
            handle = Addressables.LoadAssetAsync<GameObject>(name);
            if (handle.WaitForCompletion() == null || handle.Status != AsyncOperationStatus.Succeeded)
            { Addressables.Release(handle); throw new IOException("Missing replacement prefab " + name); }
            Handles.Add(name, handle);
        }
        return handle.WaitForCompletion();
    }

    internal static bool Replace(GearItem source)
    {
        List<GameObject> created = new();
        try
        {
            GunItem oldGun = source.GetComponent<GunItem>();
            if (oldGun == null || source.GetNormalizedCondition() <= 0) return true;
            int rounds = oldGun.NumRoundsInClip();
            GameObject prefab = Prefab("GEAR_DoubleBarrel");
            GameObject? ammo = rounds > 0 ? Prefab("GEAR_RifleAmmoSingle") : null;
            var position = source.transform.position;
            var rotation = source.transform.rotation;
            GameObject item = UnityEngine.Object.Instantiate(prefab, position, rotation);
            created.Add(item); item.name = prefab.name;
            GearItem gear = item.GetComponent<GearItem>();
            GunItem gun = item.GetComponent<GunItem>();
            if (gear == null || gun == null || gun.m_Clip == null) throw new InvalidOperationException("Replacement weapon is not configured");
            gear.ManualStart();
            gear.SetNormalizedHP(source.GetNormalizedCondition(), true);
            gun.m_Clip.Clear(); gun.m_SpentCasingsInClip = 0;
            for (int i = 0; i < rounds; i++)
            {
                GameObject round = UnityEngine.Object.Instantiate(ammo!, position + new Vector3(.08f + i * .025f, .03f, 0), Quaternion.identity);
                created.Add(round); round.name = ammo!.name;
                GearItem roundGear = round.GetComponent<GearItem>();
                if (roundGear == null) throw new InvalidOperationException("Rifle ammunition has no GearItem");
                roundGear.ManualStart();
            }
            // All replacement objects exist and have initialized successfully.
            GearManager.DestroyGearObject(source);
            return true;
        }
        catch (Exception e)
        {
            foreach (var item in created) if (item != null) GearManager.DestroyGearObject(item);
            if (lastError != e.Message) MelonLogger.Warning("Shotgun rifle replacement retained original: " + e.Message);
            lastError = e.Message;
            return false;
        }
    }

    internal static void Tick()
    {
        if (Time.realtimeSinceStartup < nextRetry || Main.Instance?.LateInitialized != true
            || SaveGameSystem.IsSceneRestoreInProgress() || GameManager.IsStoryMode()
            || ExperienceModeManager.Instance == null || ExperienceModeManager.Instance.IsChallengeActive()) return;
        nextRetry = Time.realtimeSinceStartup + 1f;
        try
        {
            string scene = GameManager.m_ActiveScene;
            var state = ShotgunSpawnPolicy.DistributionState();
            if (state == null || !ShotgunDistribution.AllowsScene(state, scene) || state.RifleScenes.Contains(scene)) return;
            // Restored scene gear is authoritative; never convert it on a subsequent load,
            // even if a previous ModData write failed after the native scene save.
            var saved = SaveGameSlots.GetSceneKeysForCurrentSaveSlot();
            if (saved == null) return;
            foreach (string key in saved) if (string.Equals(key, scene, StringComparison.OrdinalIgnoreCase))
            { state.RifleScenes.Add(scene); return; }
            Settings settings = Settings.Instance;
            string? level = GroupSpawner.CurrentLevel();
            bool enabled = settings.ReplaceStandardRifleFinds && settings.SpawnUsableShotguns && level != null
                && ShotgunSpawnPolicy.ResourceRank(level) >= (int)settings.UsableShotgunMinimumResources
                && ShotgunAvailability.AllowsWorldItems();
            var candidates = new List<GearItem>();
            foreach (var gear in GearManager.m_Gear) if (IsCandidate(gear)) candidates.Add(gear);
            if (candidates.Any(g => !g.m_StartHasBeenCalled)) return;
            foreach (var gear in candidates)
            {
                string id = ObjectGuid.MaybeGetGuidFromGameObject(gear.gameObject);
                if (string.IsNullOrEmpty(id)) continue; // No invented or unstable identities.
                bool replace = ShotgunDistribution.ChooseRifle(state, scene, id, enabled && gear.GetNormalizedCondition() > 0,
                    settings.RifleReplacementChance, () => GroupSpawner.RngFor("RifleReplacement:" + scene + ":" + id).Next(100));
                if (replace && !Replace(gear)) return;
            }
            state.RifleScenes.Add(scene);
        }
        catch (Exception e)
        {
            if (lastError != e.Message) MelonLogger.Warning("Shotgun rifle replacement deferred: " + e.Message);
            lastError = e.Message;
        }
    }
}
