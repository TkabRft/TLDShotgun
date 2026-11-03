using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using ModData;
using MelonLoader;

namespace Shotgun
{
    internal static class ShotgunSpawnPolicy
    {
        internal const string SaveKey = "ShotgunSpawnStateV2";
        internal const string LegacySaveKey = "ShotgunSpawnOutcomesV1";
        private static readonly ModDataManager Data = new("Shotgun");
        // ModData 1.5.5 returns null both for an absent key and an unloaded cache.
        // Inspect readiness only; never initialize or modify its private cache.
        private static readonly FieldInfo? CacheLoaded = typeof(ModDataManager).Assembly.GetType("ModData.ModDataCore")
            ?.GetField("cacheLoaded", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static string? loadedSave;
        private static SpawnLedger.State? state;
        private static bool blocked;
        private static string? lastWarning;
        private static readonly Dictionary<string, List<SpawnLedger.Placement>> plans = new(StringComparer.OrdinalIgnoreCase);

        internal static void Reset()
        {
            loadedSave = null; state = null; blocked = false; lastWarning = null; plans.Clear();
            ShotgunRestoration.Reset();
            GroupSpawner.ResetAttempts();
        }

        private static bool EnsureState(IReadOnlyList<GroupSpawner.Group> groups, IReadOnlyList<GroupSpawner.Group> baseline, string level)
        {
            string save = SaveGameSystem.GetCurrentSaveName();
            if (string.IsNullOrEmpty(save)) return false;
            if (save != loadedSave)
            {
                loadedSave = save; state = null; blocked = false; lastWarning = null; plans.Clear();
            }
            if (blocked) return false;
            try
            {
                if (CacheLoaded == null || CacheLoaded.FieldType != typeof(bool))
                    throw new InvalidDataException("ModData cache readiness API is unavailable");
                if (CacheLoaded.GetValue(null) is not true) return false;
                ShotgunAvailability.LoadOncePerSave();
                if (state != null) return true;
                var keys = SaveGameSlots.GetSceneKeysForCurrentSaveSlot();
                if (keys == null) return false;
                List<string> savedScenes = new();
                foreach (string key in keys) savedScenes.Add(key);
                string? stored = Data.Load(SaveKey);
                SpawnLedger.State candidate;
                if (stored != null) candidate = SpawnLedger.Read(stored);
                else
                {
                    string? legacy = Data.Load(LegacySaveKey);
                    candidate = SpawnLedger.Migrate(legacy, savedScenes, baseline, level, GroupSpawner.SceneInGame,
                        GroupSpawner.RngFor, CandidateCount(level));
                    if (savedScenes.Count > 0 || legacy != null)
                        MelonLogger.Msg("Spawn V2 migration prepared: V1 decisions take priority; missing history reconstructed from the 0.13.5 pool, current resource level and available regions. Written on the next normal save.");
                }
                SpawnLedger.ReconcileSavedScenes(candidate, savedScenes);
                SpawnLedger.Update(candidate, groups, level, GroupSpawner.SceneInGame, GroupSpawner.RngFor, CandidateCount(level));
                state = candidate;
                return true;
            }
            catch (Exception e) when (e is InvalidDataException or JsonException or ArgumentException or InvalidOperationException)
            {
                Block(e.Message);
                return false;
            }
            catch (Exception e)
            {
                Warn($"Spawn state is not ready; deferred without saving: {e.Message}");
                return false;
            }
        }

        private static void Warn(string message)
        {
            if (lastWarning != message) MelonLogger.Warning(message);
            lastWarning = message;
        }

        private static void Block(string reason)
        {
            state = null; plans.Clear(); blocked = true;
            Warn($"Grouped shotgun spawns disabled for this save load; stored data preserved: {reason}");
        }

        internal static bool TryPlan(string scene, IReadOnlyList<GroupSpawner.Group> groups,
            IReadOnlyList<GroupSpawner.Group> baseline, string level, out List<SpawnLedger.Placement> plan)
        {
            plan = new();
            if (!EnsureState(groups, baseline, level)) return blocked;
            if (plans.TryGetValue(scene, out var existing)) { plan = existing; return true; }
            try
            {
                SpawnLedger.Update(state!, groups, level, GroupSpawner.SceneInGame, GroupSpawner.RngFor, CandidateCount(level));
                plan = SpawnLedger.Plan(state!, groups, scene, (group, member) => ForMember(group, member, level));
                plans.Add(scene, plan);
                return true;
            }
            catch (Exception e) { Block(e.Message); return true; }
        }

        internal static void Commit(string scene, List<SpawnLedger.Placement> plan)
        {
            if (state != null && !blocked) SpawnLedger.Commit(state, scene, plan);
        }

        internal static void ForgetScene(string scene) => plans.Remove(scene);

        internal static int ResourceRank(string level) =>
            Enum.TryParse(level, true, out ShotgunResourceLevel rank) && Enum.IsDefined(typeof(ShotgunResourceLevel), rank)
                ? (int)rank : throw new InvalidDataException($"Unknown resource level {level}");

        internal static int CandidateCount(string level)
        {
            Settings s = Settings.Instance;
            int rank = ResourceRank(level);
            bool eligible = s.SpawnUsableShotguns && rank >= (int)s.UsableShotgunMinimumResources
                || s.SpawnRuinedShotguns && rank >= (int)s.RuinedShotgunMinimumResources;
            return eligible ? (rank == (int)ShotgunResourceLevel.VeryHigh ? 2 : 1) : 0;
        }

        internal static SpawnLedger.Outcome Roll(int roll, int usable, int ruined) => roll < usable ? SpawnLedger.Outcome.Usable
            : roll < usable + ruined ? SpawnLedger.Outcome.Ruined : SpawnLedger.Outcome.Empty;

        private static SpawnLedger.Outcome ForMember(string group, GroupSpawner.Member member, string level)
        {
            if (!ShotgunAvailability.AllowsWorldItems()) return SpawnLedger.Outcome.Empty;
            if (!SpawnLedger.IsGun(group)) return SpawnLedger.Outcome.Usable;
            Settings s = Settings.Instance;
            s.ValidateSpawnChances();
            int rank = ResourceRank(level);
            int usable = s.SpawnUsableShotguns && rank >= (int)s.UsableShotgunMinimumResources ? s.UsableShotgunChance : 0;
            int ruined = s.SpawnRuinedShotguns && rank >= (int)s.RuinedShotgunMinimumResources ? s.RuinedShotgunChance : 0;
            return Roll(GroupSpawner.RngFor("ShotgunOutcome:" + member.Id).Next(100), usable, ruined);
        }

        internal static bool Save()
        {
            if (state == null || blocked || loadedSave != SaveGameSystem.GetCurrentSaveName()) return false;
            try
            {
                if (Data.Save(JsonSerializer.Serialize(state), SaveKey)) return true;
                Warn("Spawn V2 ModData write failed; decisions retained in memory and will retry on the next normal save.");
            }
            catch (Exception e) { Warn($"Spawn V2 ModData write failed; decisions retained for the next normal save: {e.Message}"); }
            return false;
        }

        [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGame))]
        private static class ResetOnLoad
        {
            private static void Prefix() => Reset();
        }

        [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.WriteSlotToDisk), new[] { typeof(SlotData), typeof(SaveGameSlots.Timestamp) })]
        private static class SaveOutcomes
        {
            private static void Prefix() => Save();
        }
    }
}
