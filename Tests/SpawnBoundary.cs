// Native Unity/game/ModData boundaries only. Selection, migration, JSON and lifecycle
// bookkeeping execute the linked production sources, including the real parser.
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z); }
    public struct Quaternion { public float x, y, z; public static Quaternion identity => new(); public static Quaternion Euler(Vector3 v) => new() { x = v.x, y = v.y, z = v.z }; }
    public class Object
    {
        public bool Destroyed;
        public IntPtr Pointer => new(GetHashCode());
        public string name = "";
        public static T Instantiate<T>(T original) where T : Object => (T)(Object)((Il2Cpp.LootTableData)(Object)original).Clone();
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (GameObject.FailInstantiate) throw new InvalidOperationException("native instantiate fault");
            var item = new GameObject { name = prefab.name, Position = position };
            item.transform.position = position; item.transform.rotation = rotation;
            GameObject.Spawned.Add(item); return item;
        }
        public static void Destroy(Object value) { value.Destroyed = true; if(value is Il2Cpp.LootTableData table) table.Released = true; }
    }
    public class GameObject : Object
    {
        public bool activeInHierarchy = true;
        public Transform transform = new();
        public string Guid = "native-guid";
        private readonly Dictionary<Type, object> components = new();
        public Vector3 Position;
        public bool Ruined;
        public static bool FailInstantiate;
        public static readonly List<GameObject> Spawned = new();
        public T GetComponent<T>() where T : new()
        {
            if (!components.TryGetValue(typeof(T), out var value))
            { value = new T(); components.Add(typeof(T), value!); if(value is Il2Cpp.GearItem gear) gear.gameObject=this; }
            return (T)value!;
        }
    }
    public class Transform { public Vector3 position; public Quaternion rotation; }
    public static class Time { public static float realtimeSinceStartup; }
}
namespace UnityEngine.ResourceManagement.ResourceLocations { public interface IResourceLocation { } }
namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : System.Collections.Generic.List<T> { }
    public interface ICollection<T> { int Count { get; } }
    public class IList<T> : List<T>, ICollection<T> { public U Cast<U>() => (U)(object)this; }
}
namespace UnityEngine.ResourceManagement.AsyncOperations
{
    public enum AsyncOperationStatus { Failed, Succeeded }
    public struct AsyncOperationHandle<T>
    {
        public T Value;
        public AsyncOperationStatus Status => UnityEngine.AddressableAssets.Addressables.FailLocations ? AsyncOperationStatus.Failed : AsyncOperationStatus.Succeeded;
        public T WaitForCompletion() => Value;
    }
}
namespace UnityEngine.AddressableAssets
{
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceLocations;
    public static class Addressables
    {
        public static readonly HashSet<string> AbsentScenes = new();
        public static readonly HashSet<string> MissingPrefabs = new();
        public static int Queries;
        public static bool FailLocations;
        public static AsyncOperationHandle<Il2CppSystem.Collections.Generic.IList<IResourceLocation>> LoadResourceLocationsAsync(string key, object? type)
        {
            Queries++;
            var values = new Il2CppSystem.Collections.Generic.IList<IResourceLocation>();
            if (!AbsentScenes.Contains(key)) values.Add(null!);
            return new() { Value = values };
        }
        public static AsyncOperationHandle<T> LoadAssetAsync<T>(string key) => new()
        { Value = MissingPrefabs.Contains(key) ? default! : (T)(object)new UnityEngine.GameObject { name = key } };
        public static void Release<T>(AsyncOperationHandle<T> value) { }
    }
}
namespace Il2CppTLD.Gameplay { public class RandomTableDataEntry<T> { public T m_Item = default!; public int m_Weight; } }
namespace Il2Cpp
{
    public enum ExperienceModeType { Custom, Other }
    public enum ResourceLevel { Low, Medium, High, VeryHigh }
    public class ExperienceMode { public ResourceLevel m_BaseResourceAvailability = ResourceLevel.High; }
    public class CustomExperienceMode : ExperienceMode { }
    public class GameModeConfig { public object? m_ActiveTags = new(); }
    public class ExperienceModeManager
    {
        public static ExperienceModeManager? Instance = new();
        public static GameModeConfig? s_CurrentGameMode = new();
        public static ExperienceModeType Kind;
        public static ResourceLevel Level = ResourceLevel.High;
        public static bool Challenge;
        public bool IsChallengeActive() => Challenge;
        public static ExperienceModeType GetCurrentExperienceModeType() => Kind;
        public ExperienceMode GetCurrentExperienceMode() => new() { m_BaseResourceAvailability = Level };
    }
    public class Transition { public int m_GameRandomSeed = 12345; }
    public static class GameManager
    {
        public static Transition? m_SceneTransitionData = new();
        public static string m_ActiveScene = "Boot";
        public static bool Story;
        public static bool IsStoryMode() => Story;
        public static CustomExperienceMode GetCustomMode() => new() { m_BaseResourceAvailability = ExperienceModeManager.Level };
    }
    public class DisableObjectForXPMode : UnityEngine.Object { }
    public class AssetReferenceGearItem { public string AssetGUID; public AssetReferenceGearItem(string id) => AssetGUID=id; }
    public class LootTableData : UnityEngine.Object
    {
        public Il2CppSystem.Collections.Generic.List<Il2CppTLD.Gameplay.RandomTableDataEntry<AssetReferenceGearItem>> m_BaseEntries = new();
        public List<AssetReferenceGearItem> m_FilteredExtendedItems = new();
        public Dictionary<string, object> m_ExistingOperations = new();
        public bool Released;
        public LootTableData Clone() => new() { name=name, m_BaseEntries=m_BaseEntries, m_FilteredExtendedItems=m_FilteredExtendedItems, m_ExistingOperations=m_ExistingOperations };
        public void MaybeInitializeTable() { }
        public void FilterEntries() { foreach(var entry in m_BaseEntries) m_FilteredExtendedItems.Add(entry.m_Item); }
    }
    public class Container : UnityEngine.Object
    {
        public LootTableData m_LootTable=null!, m_LootTableData=null!, m_LockedLootTableData=null!;
        public void PopulateWithRandomGear() { }
    }
    public class GearItem : UnityEngine.Object
    {
        public UnityEngine.GameObject gameObject=new();
        public new string name => gameObject.name;
        public UnityEngine.Transform transform => gameObject.transform;
        public bool m_InPlayerInventory, m_BeenInPlayerInventory, m_BeenInContainer, m_InsideContainer, m_BeenInspected;
        public bool m_StartHasBeenCalled = true;
        public object? m_MissionObject, m_NarrativeCollectibleItem;
        public float Condition=.72f;
        public static string? FailStartName;
        public bool IsInsideContainer() => m_InsideContainer;
        public T GetComponent<T>() where T:new() => gameObject.GetComponent<T>();
        public float GetNormalizedCondition() => Condition;
        public void SetNormalizedHP(float hp,bool value) => Condition=hp;
        public void ManualStart() { if(name==FailStartName) throw new IOException("start failure"); m_StartHasBeenCalled=true; }
    }
    public class GunItem { public List<int> m_Clip=new(){0}; public int m_SpentCasingsInClip; public int NumRoundsInClip()=>m_Clip.Count; }
    public static class GearManager
    {
        public static List<GearItem> m_Gear=new();
        public static void DestroyGearObject(GearItem gear) => DestroyGearObject(gear.gameObject);
        public static void DestroyGearObject(UnityEngine.GameObject item) { item.Destroyed=true; m_Gear.RemoveAll(g=>ReferenceEquals(g.gameObject,item)); }
    }
    public static class ObjectGuid { public static string MaybeGetGuidFromGameObject(UnityEngine.GameObject item)=>item.Guid; }
    public static class QualitySettingsManager { public static void ApplyCurrentQualitySettings() { } }
    public class SlotData { }
    public static class SaveGameSlots
    {
        public enum Timestamp { Now }
        public static List<string>? Scenes = new();
        public static List<string>? GetSceneKeysForCurrentSaveSlot() => Scenes;
        public static void WriteSlotToDisk(SlotData data, Timestamp timestamp) { }
    }
    public static class SaveGameSystem
    {
        public static string CurrentSave = "fixture";
        public static int m_CurrentGameId = 1;
        public static string GetCurrentSaveName() => CurrentSave;
        public static void RestoreGame() { }
        public static bool IsSceneRestoreInProgress() => false;
    }
}
namespace ModData
{
    public static class ModDataCore { public static bool cacheLoaded = true; }
    public class ModDataManager
    {
        public static readonly Dictionary<(string, string), string> Values = new();
        public static bool FailWrite;
        public ModDataManager(string owner) { if (owner != "Shotgun") throw new Exception("Namespace changed"); }
        public string? Load(string suffix) => ModDataCore.cacheLoaded && Values.TryGetValue((Il2Cpp.SaveGameSystem.CurrentSave, suffix), out var value) ? value : null;
        public bool Save(string value, string suffix)
        {
            if (FailWrite || !ModDataCore.cacheLoaded) return false;
            Values[(Il2Cpp.SaveGameSystem.CurrentSave, suffix)] = value; return true;
        }
    }
}
namespace ModComponent { public static class Public { public static bool Ready = true; public static bool IsReady() => Ready; } }
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
        public HarmonyPatch(Type type, string method, Type[] parameters) { }
    }
}
namespace MelonLoader
{
    public static class MelonLogger
    {
        public static readonly List<string> Messages = new();
        public static void Warning(string message) => Messages.Add(message);
        public static void Msg(string message) => Messages.Add(message);
    }
}
namespace Shotgun
{
    internal enum ShotgunResourceLevel { Low, Medium, High, VeryHigh }
    internal class Main { public static Main? Instance = new(); public bool LateInitialized=true; }
    internal class Settings
    {
        internal static Settings Instance = new();
        public bool SpawnUsableShotguns = true, SpawnRuinedShotguns = true;
        public bool ReplaceStandardRifleFinds;
        public int RifleReplacementChance=25;
        public int UsableShotgunChance = 50, RuinedShotgunChance = 50;
        public ShotgunResourceLevel UsableShotgunMinimumResources = ShotgunResourceLevel.High;
        public ShotgunResourceLevel RuinedShotgunMinimumResources = ShotgunResourceLevel.Medium;
        public void ValidateSpawnChances()
        { UsableShotgunChance = Math.Clamp(UsableShotgunChance, 0, 100); RuinedShotgunChance = Math.Clamp(RuinedShotgunChance, 0, 100 - UsableShotgunChance); }
    }
    internal static class ShotgunAvailability
    {
        internal static bool Allowed = true;
        internal static bool Loaded;
        internal static void LoadOncePerSave() => Loaded = true;
        internal static bool AllowsWorldItems() => Loaded ? Allowed : throw new Exception("Availability used before slot settings load");
    }
    internal static class ShotgunRestoration
    {
        internal static void Reset() { }
        internal static void MarkRuined(UnityEngine.GameObject item) => item.Ruined = true;
    }
}
