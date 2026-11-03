// Native Unity/game/ModData boundaries only. Selection, migration, JSON and lifecycle
// bookkeeping execute the linked production sources, including the real parser.
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; } }
    public struct Quaternion { public float x, y, z; public static Quaternion Euler(Vector3 v) => new() { x = v.x, y = v.y, z = v.z }; }
    public class Object
    {
        public bool Destroyed;
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (GameObject.FailInstantiate) throw new InvalidOperationException("native instantiate fault");
            var item = new GameObject { name = prefab.name, Position = position };
            GameObject.Spawned.Add(item); return item;
        }
        public static void Destroy(Object value) => value.Destroyed = true;
    }
    public class GameObject : Object
    {
        public string name = "";
        public Vector3 Position;
        public bool Ruined;
        public static bool FailInstantiate;
        public static readonly List<GameObject> Spawned = new();
        public T GetComponent<T>() where T : new() => new();
    }
    public static class Time { public static float realtimeSinceStartup; }
}
namespace UnityEngine.ResourceManagement.ResourceLocations { public interface IResourceLocation { } }
namespace Il2CppSystem.Collections.Generic
{
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
namespace Il2CppTLD.Gameplay { }
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
        public static ExperienceModeType GetCurrentExperienceModeType() => Kind;
        public ExperienceMode GetCurrentExperienceMode() => new() { m_BaseResourceAvailability = Level };
    }
    public class Transition { public int m_GameRandomSeed = 12345; }
    public static class GameManager
    {
        public static Transition? m_SceneTransitionData = new();
        public static string m_ActiveScene = "Boot";
        public static CustomExperienceMode GetCustomMode() => new() { m_BaseResourceAvailability = ExperienceModeManager.Level };
    }
    public class DisableObjectForXPMode : UnityEngine.Object { }
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
    internal class Settings
    {
        internal static Settings Instance = new();
        public bool SpawnUsableShotguns = true, SpawnRuinedShotguns = true;
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
