using System.Globalization;
using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using ModData;
using MelonLoader;

namespace Shotgun
{
    internal static class ShotgunSpawnPolicy
    {
        internal enum Outcome { Empty, Usable, Ruined }
        private const string SaveKey = "ShotgunSpawnOutcomesV1";
        private static readonly ModDataManager Data = new("Shotgun");
        private static string? loadedSave;
        private static State? state;
        private static HashSet<string> previousScenes = new(StringComparer.OrdinalIgnoreCase);

        internal sealed class State
        {
            public State() { }
            public int Version { get; set; } = 1;
            public Dictionary<string, bool> LegacyScenes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, Outcome> Outcomes { get; set; } = new();
            public Dictionary<string, List<string>> Selections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        internal static void Reset()
        {
            loadedSave = null;
            state = null;
            previousScenes.Clear();
            ShotgunRestoration.Reset();
            GroupSpawner.ResetAttempts();
        }

        private static bool EnsureState()
        {
            string save = SaveGameSystem.GetCurrentSaveName();
            if (string.IsNullOrEmpty(save)) return false;
            if (save == loadedSave) return state != null;
            loadedSave = save;
            state = null;
            GroupSpawner.ResetAttempts();
            try
            {
                var sceneKeys = SaveGameSlots.GetSceneKeysForCurrentSaveSlot();
                if (sceneKeys == null) throw new InvalidOperationException("Saved-scene keys are unavailable");
                previousScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in sceneKeys) previousScenes.Add(key);
                string stored = Data.Load(SaveKey);
                State candidate = string.IsNullOrEmpty(stored) ? new State() :
                    JsonSerializer.Deserialize<State>(stored) ?? throw new InvalidOperationException("Empty spawn state");
                if (candidate.Version != 1 || candidate.LegacyScenes == null || candidate.Outcomes == null || candidate.Selections == null)
                    throw new InvalidOperationException("Unsupported or incomplete spawn state");
                if (candidate.Selections.Values.Any(v => v == null) || candidate.Outcomes.Values.Any(v => !Enum.IsDefined(typeof(Outcome), v)))
                    throw new InvalidOperationException("Invalid spawn selections or outcomes");
                candidate.LegacyScenes = new(candidate.LegacyScenes, StringComparer.OrdinalIgnoreCase);
                candidate.Selections = new(candidate.Selections, StringComparer.OrdinalIgnoreCase);
                state = candidate;
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Preserving legacy shotgun spawning for this load: {e.Message}");
                return false;
            }
        }

        internal static bool UseLegacy(string scene)
        {
            if (!EnsureState()) return true;
            if (!state!.LegacyScenes.TryGetValue(scene, out bool legacy))
                state.LegacyScenes[scene] = legacy = previousScenes.Contains(scene);
            return legacy;
        }

        internal static int ResourceRank(string level) =>
            Enum.TryParse(level, true, out ShotgunResourceLevel rank) ? (int)rank : (int)ShotgunResourceLevel.High;

        internal static int CandidateCount(string level)
        {
            Settings s = Settings.Instance;
            int rank = ResourceRank(level);
            bool eligible = s.SpawnUsableShotguns && rank >= (int)s.UsableShotgunMinimumResources
                || s.SpawnRuinedShotguns && rank >= (int)s.RuinedShotgunMinimumResources;
            return eligible ? (rank == (int)ShotgunResourceLevel.VeryHigh ? 2 : 1) : 0;
        }

        internal static string MemberKey(string group, GroupSpawner.Member member) =>
            $"{group}:{member.Scene}:{member.Prefab}:" +
            member.Position.x.ToString("R", CultureInfo.InvariantCulture) + "," +
            member.Position.y.ToString("R", CultureInfo.InvariantCulture) + "," +
            member.Position.z.ToString("R", CultureInfo.InvariantCulture);

        internal static IEnumerable<GroupSpawner.Member> PickForScene(GroupSpawner.Group group, string scene, string level)
        {
            if (!state!.Selections.TryGetValue(scene, out var selected))
            {
                selected = GroupSpawner.Pick(group, level, GroupSpawner.RngFor(group.Name), CandidateCount(level))
                    .Where(m => string.Equals(m.Scene, scene, StringComparison.OrdinalIgnoreCase))
                    .Select(m => MemberKey(group.Name, m)).ToList();
                state.Selections.Add(scene, selected);
                // Snapshot outcomes now, including empty/banned results. Later setting
                // or optional-region changes must not add finds to this visited scene.
                foreach (var member in group.Members.Where(m => selected.Contains(MemberKey(group.Name, m))))
                    ForMember(group.Name, member, level);
            }
            return group.Members.Where(m => selected.Contains(MemberKey(group.Name, m)));
        }

        internal static Outcome Roll(int roll, int usable, int ruined) =>
            roll < usable ? Outcome.Usable : roll < usable + ruined ? Outcome.Ruined : Outcome.Empty;

        internal static Outcome ForMember(string group, GroupSpawner.Member member, string level)
        {
            if (state == null) return Outcome.Usable; // Only reached on the conservative legacy path.
            string key = MemberKey(group, member);
            if (state.Outcomes.TryGetValue(key, out Outcome previous)) return previous;
            Settings s = Settings.Instance;
            s.ValidateSpawnChances();
            int rank = ResourceRank(level);
            int usable = s.SpawnUsableShotguns && rank >= (int)s.UsableShotgunMinimumResources ? s.UsableShotgunChance : 0;
            int ruined = s.SpawnRuinedShotguns && rank >= (int)s.RuinedShotgunMinimumResources ? s.RuinedShotgunChance : 0;
            Outcome result = ShotgunAvailability.AllowsWorldItems()
                ? Roll(GroupSpawner.RngFor("ShotgunOutcome:" + key).Next(100), usable, ruined) : Outcome.Empty;
            state.Outcomes.Add(key, result);
            return result;
        }

        [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGame))]
        private static class ResetOnLoad
        {
            private static void Prefix() => Reset();
        }

        [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.WriteSlotToDisk), new[] { typeof(SlotData), typeof(SaveGameSlots.Timestamp) })]
        private static class SaveOutcomes
        {
            private static void Prefix()
            {
                if (state != null && loadedSave == SaveGameSystem.GetCurrentSaveName())
                    Data.Save(JsonSerializer.Serialize(state), SaveKey);
            }
        }
    }
}
