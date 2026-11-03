using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using Il2CppTLD.Gameplay.Tunable;
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
            public string Scene;
            public Dictionary<string, int> Picks = new(StringComparer.OrdinalIgnoreCase);
            public List<Member> Members = new();
        }

        private static readonly Regex GroupLine = new(@"^group\s*=\s*(\w+)((?:\s+\w+\s*=\s*\w+)*)$", RegexOptions.IgnoreCase);
        private static readonly Regex Keys = new(@"(\w+)\s*=\s*(\w+)");
        private static readonly Regex ItemLine = new(@"^item\s*=\s*(\w+)\s+id\s*=\s*(\w+)(?:\s+scene\s*=\s*(\w+))?\s+p\s*=\s*([-\d.,\s]+?)\s+r\s*=\s*([-\d.,\s]+?)$", RegexOptions.IgnoreCase);

        private static List<Group> sGroups;

        internal static void SetGroups(IEnumerable<Group> groups) => sGroups = groups.Where(g => g.Members.Count > 0).ToList();

        private static void EnsureLoaded()
        {
            if (sGroups != null) return;
            using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Shotgun.groupspawns.txt");
            if (s == null) { SetGroups(Array.Empty<Group>()); return; }
            using StreamReader reader = new(s);
            SetGroups(Parse(reader.ReadToEnd()));
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

        internal static string CurrentLevel()
        {
            try
            {
                if (ExperienceModeManager.GetCurrentExperienceModeType() == ExperienceModeType.Custom)
                {
                    CustomExperienceMode custom = GameManager.GetCustomMode();
                    if (custom != null) return custom.m_BaseResourceAvailability.ToString();
                }
                ExperienceMode mode = ExperienceModeManager.Instance?.GetCurrentExperienceMode();
                if (mode != null) return mode.m_BaseResourceAvailability.ToString();
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"GroupSpawner: resource level unavailable ({e.Message}), using High");
            }
            return CustomTunableLMHV.High.ToString();
        }

        internal static int PicksFor(Group g, string level) =>
            g.Picks.TryGetValue(level, out int n) || g.Picks.TryGetValue("default", out n) ? n : 0;

        internal static string GameKey()
        {
            int seed = GameManager.m_SceneTransitionData != null ? GameManager.m_SceneTransitionData.m_GameRandomSeed : 0;
            return $"{seed}:{SaveGameSystem.m_CurrentGameId}";
        }

        internal static System.Random RngFor(string groupName)
        {
            uint h = 2166136261;
            foreach (char c in GameKey() + ":" + groupName.ToLowerInvariant()) h = (h ^ c) * 16777619;
            return new System.Random((int)h);
        }

        private static readonly HashSet<string> sScenesInGame = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> sScenesReported = new(StringComparer.OrdinalIgnoreCase);

        internal static bool SceneInGame(string scene)
        {
            if (sScenesInGame.Contains(scene)) return true;
            AsyncOperationHandle<Il2CppSystem.Collections.Generic.IList<IResourceLocation>> locations = Addressables.LoadResourceLocationsAsync(scene, null);
            bool found = locations.WaitForCompletion()?.Cast<Il2CppSystem.Collections.Generic.ICollection<IResourceLocation>>().Count > 0;
            Addressables.Release(locations);
            if (found)
            {
                sScenesInGame.Add(scene);
                return true;
            }
            if (sScenesReported.Add(scene)) MelonLogger.Msg($"GroupSpawner: {scene} is not in this game (mod not installed?), left out of the spawn pools");
            return false;
        }

        internal static List<Member> Pick(Group g, string level, System.Random rng, int? requestedCount = null)
        {
            List<Member> pool = g.Members.Where(m => SceneInGame(m.Scene)).ToList();
            List<Member> picked = new();
            int count = Math.Min(requestedCount ?? PicksFor(g, level), pool.Count);
            for (int i = 0; i < count; i++)
            {
                int index = rng.Next(pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picked;
        }

        internal static List<Member> PickedForThisGame(Group g) => Pick(g, CurrentLevel(), RngFor(g.Name));

        private static readonly Dictionary<string, HashSet<string>> sAttempts = new(StringComparer.OrdinalIgnoreCase);
        internal static void ResetAttempts() => sAttempts.Clear();
        internal static void ForgetScene(string scene) => sAttempts.Remove(scene);

        private static void SpawnScene(string scene)
        {
            EnsureLoaded();
            string level = CurrentLevel();
            // Initialize the saved-scene policy before taking a reference to the attempts
            // set: changing saves clears the sets as part of policy initialization.
            bool legacy = ShotgunSpawnPolicy.UseLegacy(scene);
            if (!sAttempts.TryGetValue(scene, out var attempts)) sAttempts[scene] = attempts = new();
            foreach (Group g in sGroups)
            {
                if (!g.Members.Any(m => string.Equals(m.Scene, scene, StringComparison.OrdinalIgnoreCase))) continue;
                bool gunGroup = g.Name == "SG_Shotgun";
                IEnumerable<Member> selected = gunGroup && !legacy ? ShotgunSpawnPolicy.PickForScene(g, scene, level)
                    : Pick(g, level, RngFor(g.Name)).Where(m => string.Equals(m.Scene, scene, StringComparison.OrdinalIgnoreCase));
                foreach (Member m in selected)
                {
                    string key = ShotgunSpawnPolicy.MemberKey(g.Name, m);
                    if (attempts.Contains(key)) continue;
                    ShotgunSpawnPolicy.Outcome outcome = gunGroup && !legacy
                        ? ShotgunSpawnPolicy.ForMember(g.Name, m, level) : ShotgunSpawnPolicy.Outcome.Usable;
                    if (outcome == ShotgunSpawnPolicy.Outcome.Empty) { attempts.Add(key); continue; }
                    if (!ShotgunAvailability.AllowsWorldItems()) continue;
                    GameObject prefab = Addressables.LoadAssetAsync<GameObject>(m.Prefab).WaitForCompletion();
                    if (prefab == null) { MelonLogger.Warning($"GroupSpawner: no prefab {m.Prefab}"); continue; }
                    GameObject gear = UnityEngine.Object.Instantiate(prefab, m.Position, m.Rotation);
                    gear.name = prefab.name;
                    if (outcome == ShotgunSpawnPolicy.Outcome.Ruined) ShotgunRestoration.MarkRuined(gear);
                    DisableObjectForXPMode xp = gear.GetComponent<DisableObjectForXPMode>();
                    if (xp != null) UnityEngine.Object.Destroy(xp);
                    attempts.Add(key);
                }
            }
        }

        [HarmonyPatch(typeof(QualitySettingsManager), nameof(QualitySettingsManager.ApplyCurrentQualitySettings))]
        private static class SceneReadyPatch
        {
            private static void Prefix()
            {
                string scene = GameManager.m_ActiveScene;
                if (scene is null or "" or "MainMenu" or "Boot" or "Empty") return;
                try { SpawnScene(scene); }
                catch (Exception e) { MelonLogger.Error($"GroupSpawner: {scene}: {e}"); }
            }
        }
    }
}
