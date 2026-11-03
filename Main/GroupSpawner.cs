using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Shotgun
{
    internal static class GroupSpawner
    {
        internal sealed record Member(string Id, string Scene, string Prefab, Vector3 Position, Quaternion Rotation);
        internal sealed class Group
        {
            public string Name = "";
            public string? Scene;
            public Dictionary<string, int> Picks = new(StringComparer.OrdinalIgnoreCase);
            public List<Member> Members = new();
        }

        internal const string BaselineHash = "6d45d47219273def9c7a2aeb7f629bdf8eb077ffdf55485b0b87e147564e972c";
        private static readonly Regex GroupLine = new(@"^group\s*=\s*(\w+)((?:\s+\w+\s*=\s*\w+)*)$", RegexOptions.IgnoreCase);
        private static readonly Regex Keys = new(@"(\w+)\s*=\s*(\w+)");
        private static readonly Regex ItemLine = new(@"^item\s*=\s*(\w+)\s+id\s*=\s*(\w+)(?:\s+scene\s*=\s*(\w+))?\s+p\s*=\s*([-\d.,\s]+?)\s+r\s*=\s*([-\d.,\s]+?)$", RegexOptions.IgnoreCase);
        private static List<Group>? groups;
        private static List<Group>? baseline;
        private static readonly Dictionary<string, bool> scenes = new(StringComparer.OrdinalIgnoreCase);
        // Keep one Addressables ownership handle per prefab for process lifetime, as
        // manually instantiated gear may still use its assets after a scene transition.
        private static readonly Dictionary<string, AsyncOperationHandle<GameObject>> prefabHandles = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, HashSet<string>> attempts = new(StringComparer.OrdinalIgnoreCase);
        private static string? pendingScene;
        private static string? pendingSave;
        private static string? attemptSave;
        private static float nextRetry;
        private static string? lastWarning;

        internal static void SetGroups(IEnumerable<Group> source) => groups = source.ToList();

        private static string Resource(string name)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new InvalidDataException($"Missing spawn resource {name}");
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        private static void EnsureLoaded()
        {
            if (groups == null) groups = Parse(Resource("Shotgun.groupspawns.txt"));
            if (baseline != null) return;
            string snapshot = Resource("Shotgun.legacy-v1-groupspawns.txt").Replace("\r\n", "\n");
            if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant() != BaselineHash)
                throw new InvalidDataException("The immutable V1 spawn snapshot has changed");
            baseline = Parse(snapshot);
            SpawnLedger.LegacyAliases(baseline);
        }

        internal static List<Group> Parse(string text)
        {
            List<Group> result = new();
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            Group? current = null;
            int number = 0;
            foreach (string raw in text.Split('\n'))
            {
                number++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                try
                {
                    Match match = GroupLine.Match(line);
                    if (match.Success)
                    {
                        string name = match.Groups[1].Value;
                        if (!names.Add(name)) throw new InvalidDataException($"Repeated group {name}");
                        current = new Group { Name = name };
                        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
                        foreach (Match key in Keys.Matches(match.Groups[2].Value))
                        {
                            string field = key.Groups[1].Value;
                            if (!seen.Add(field)) throw new InvalidDataException($"Repeated field {field}");
                            if (field.Equals("scene", StringComparison.OrdinalIgnoreCase)) current.Scene = key.Groups[2].Value;
                            else if (new[] { "low", "medium", "high", "veryhigh", "default" }.Contains(field.ToLowerInvariant())
                                && int.TryParse(key.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
                                current.Picks.Add(field, count);
                            else throw new InvalidDataException($"Invalid group field {field}");
                        }
                        result.Add(current);
                    }
                    else if (current != null && (match = ItemLine.Match(line)).Success)
                    {
                        string id = match.Groups[2].Value;
                        string? scene = match.Groups[3].Success ? match.Groups[3].Value : current.Scene;
                        if (string.IsNullOrEmpty(scene)) throw new InvalidDataException("Missing scene");
                        if (!ids.Add(id)) throw new InvalidDataException($"Repeated spawn ID {id}");
                        current.Members.Add(new Member(id, scene, match.Groups[1].Value, Vec(match.Groups[4].Value), Quaternion.Euler(Vec(match.Groups[5].Value))));
                    }
                    else throw new InvalidDataException("Expected a group or an item with a permanent id");
                }
                catch (Exception e) { throw new InvalidDataException($"Spawn line {number}: {e.Message}", e); }
            }
            if (result.Count == 0 || result.Any(g => g.Members.Count == 0 || g.Picks.Count == 0))
                throw new InvalidDataException("Empty spawn data or group without members/counts");
            return result;
        }

        private static Vector3 Vec(string value)
        {
            float[] v = value.Split(',').Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture)).ToArray();
            if (v.Length != 3 || v.Any(x => !float.IsFinite(x))) throw new InvalidDataException("Expected three finite coordinates");
            return new Vector3(v[0], v[1], v[2]);
        }

        internal static string? CurrentLevel()
        {
            if (ExperienceModeManager.GetCurrentExperienceModeType() == ExperienceModeType.Custom)
                return GameManager.GetCustomMode()?.m_BaseResourceAvailability.ToString();
            return ExperienceModeManager.Instance?.GetCurrentExperienceMode()?.m_BaseResourceAvailability.ToString();
        }

        internal static int PicksFor(Group group, string level) =>
            group.Picks.TryGetValue(level, out int n) || group.Picks.TryGetValue("default", out n) ? n : 0;

        internal static string GameKey() => $"{GameManager.m_SceneTransitionData!.m_GameRandomSeed}:{SaveGameSystem.m_CurrentGameId}";

        internal static System.Random RngFor(string name)
        {
            uint hash = 2166136261;
            foreach (char c in GameKey() + ":" + name.ToLowerInvariant()) hash = unchecked((hash ^ c) * 16777619);
            return new System.Random(unchecked((int)hash));
        }

        internal static bool SceneInGame(string scene)
        {
            if (scenes.TryGetValue(scene, out bool cached)) return cached;
            AsyncOperationHandle<Il2CppSystem.Collections.Generic.IList<IResourceLocation>> locations = Addressables.LoadResourceLocationsAsync(scene, null);
            bool found;
            try
            {
                var result = locations.WaitForCompletion();
                if (locations.Status != AsyncOperationStatus.Succeeded || result == null)
                    throw new IOException($"Scene catalog lookup not ready: {scene}");
                found = result.Cast<Il2CppSystem.Collections.Generic.ICollection<IResourceLocation>>().Count > 0;
            }
            catch (Exception e) { throw new IOException($"Scene catalog lookup deferred: {scene}", e); }
            finally { Addressables.Release(locations); }
            scenes.Add(scene, found);
            if (!found) MelonLogger.Msg($"GroupSpawner: {scene} is absent; excluded from available spawn pools");
            return found;
        }

        internal static List<Member> Pick(IEnumerable<Member> source, int count, System.Random rng)
        {
            List<Member> pool = source.ToList();
            List<Member> picked = new();
            for (int i = 0, n = Math.Min(count, pool.Count); i < n; i++)
            {
                int index = rng.Next(pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picked;
        }

        internal static void ResetAttempts()
        {
            attempts.Clear(); scenes.Clear(); pendingScene = pendingSave = attemptSave = lastWarning = null; nextRetry = 0;
        }

        internal static void ForgetScene(string scene)
        {
            attempts.Remove(scene);
            ShotgunSpawnPolicy.ForgetScene(scene);
            if (string.Equals(scene, pendingScene, StringComparison.OrdinalIgnoreCase)) pendingScene = pendingSave = null;
        }

        internal static void RequestScene(string scene)
        {
            if (scene is "" or "MainMenu" or "Boot" or "Empty") return;
            string save = SaveGameSystem.GetCurrentSaveName();
            if (attemptSave != save) { ResetAttempts(); attemptSave = save; }
            pendingScene = scene;
            pendingSave = save;
            nextRetry = 0;
            Tick();
        }

        // Deferred work is only for the active scene/slot and runs on the Unity thread.
        internal static void Tick()
        {
            if (pendingScene == null || Time.realtimeSinceStartup < nextRetry) return;
            string scene = pendingScene;
            string save = SaveGameSystem.GetCurrentSaveName();
            if (string.IsNullOrEmpty(pendingSave) && !string.IsNullOrEmpty(save)) pendingSave = attemptSave = save;
            if (scene != GameManager.m_ActiveScene || pendingSave != save)
            { pendingScene = pendingSave = null; return; }
            nextRetry = Time.realtimeSinceStartup + 1f;
            try
            {
                if (!ModComponent.Public.IsReady() || GameManager.m_SceneTransitionData == null
                    || ExperienceModeManager.s_CurrentGameMode?.m_ActiveTags == null) return;
                string? level = CurrentLevel();
                if (level == null) return;
                EnsureLoaded();
                if (!ShotgunSpawnPolicy.TryPlan(scene, groups!, baseline!, level, out var plan)) return;
                if (!attempts.TryGetValue(scene, out var done)) attempts[scene] = done = new(StringComparer.Ordinal);
                Dictionary<string, GameObject> prefabs = new(StringComparer.Ordinal);
                foreach (var placement in plan.Where(p => p.Outcome != SpawnLedger.Outcome.Empty && !done.Contains(p.Member.Id)))
                {
                    if (prefabs.ContainsKey(placement.Member.Prefab)) continue;
                    string address = placement.Member.Prefab;
                    if (!prefabHandles.TryGetValue(address, out var handle))
                    {
                        handle = Addressables.LoadAssetAsync<GameObject>(address);
                        try
                        {
                            if (handle.WaitForCompletion() == null || handle.Status != AsyncOperationStatus.Succeeded)
                                throw new IOException($"Missing spawn prefab {address}; scene decision deferred");
                            prefabHandles.Add(address, handle);
                        }
                        catch { Addressables.Release(handle); throw; }
                    }
                    GameObject prefab = handle.WaitForCompletion();
                    prefabs.Add(placement.Member.Prefab, prefab);
                }
                ShotgunSpawnPolicy.Commit(scene, plan);
                foreach (var placement in plan)
                {
                    Member member = placement.Member;
                    if (done.Contains(member.Id)) continue;
                    if (placement.Outcome != SpawnLedger.Outcome.Empty)
                    {
                        GameObject? gear = null;
                        try
                        {
                            GameObject prefab = prefabs[member.Prefab];
                            gear = UnityEngine.Object.Instantiate(prefab, member.Position, member.Rotation);
                            gear.name = prefab.name;
                            if (placement.Outcome == SpawnLedger.Outcome.Ruined) ShotgunRestoration.MarkRuined(gear);
                            DisableObjectForXPMode xp = gear.GetComponent<DisableObjectForXPMode>();
                            if (xp != null) UnityEngine.Object.Destroy(xp);
                        }
                        catch { if (gear != null) UnityEngine.Object.Destroy(gear); throw; }
                    }
                    done.Add(member.Id);
                }
                ShotgunSpawnPolicy.ForgetScene(scene);
                pendingScene = pendingSave = lastWarning = null;
            }
            catch (Exception e)
            {
                // Retry the same plan, never a replacement after partial success.
                if (lastWarning != e.Message) MelonLogger.Warning($"GroupSpawner: {scene}: {e.Message}");
                lastWarning = e.Message;
            }
        }

        [HarmonyPatch(typeof(QualitySettingsManager), nameof(QualitySettingsManager.ApplyCurrentQualitySettings))]
        private static class SceneReadyPatch
        {
            private static void Prefix()
            {
                string scene = GameManager.m_ActiveScene;
                if (!string.IsNullOrEmpty(scene)) RequestScene(scene);
            }
        }
    }
}
