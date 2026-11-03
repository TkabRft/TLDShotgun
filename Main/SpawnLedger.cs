using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Shotgun
{
    // Placement bookkeeping only. Native gear serialization remains authoritative.
    internal static class SpawnLedger
    {
        internal enum Outcome { Empty, Usable, Ruined }
        internal sealed record Placement(string Group, GroupSpawner.Member Member, Outcome Outcome);

        internal sealed class State
        {
            public int Version { get; set; } = 2;
            public HashSet<string> VisitedScenes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, GroupState> Groups { get; set; } = new(StringComparer.Ordinal);
            // Retained even after removal: an ID cannot acquire another prefab/group.
            public Dictionary<string, string> Identities { get; set; } = new(StringComparer.Ordinal);
        }

        internal sealed class GroupState
        {
            public int Quota { get; set; }
            public string Fingerprint { get; set; } = "";
            public Dictionary<string, Decision> Processed { get; set; } = new(StringComparer.Ordinal);
            public List<Reservation> Pending { get; set; } = new();
        }

        internal sealed class Decision
        {
            public string Scene { get; set; } = "";
            public Outcome Outcome { get; set; }
        }

        internal sealed class Reservation
        {
            public string Id { get; set; } = "";
            public string Scene { get; set; } = "";
        }

        internal sealed class LegacyState
        {
            public int Version { get; set; }
            public Dictionary<string, bool> LegacyScenes { get; set; } = new();
            public Dictionary<string, Outcome> Outcomes { get; set; } = new();
            public Dictionary<string, List<string>> Selections { get; set; } = new();
        }

        internal static string LegacyMemberKey(string group, GroupSpawner.Member member) =>
            $"{group}:{member.Scene}:{member.Prefab}:" +
            member.Position.x.ToString("R", CultureInfo.InvariantCulture) + "," +
            member.Position.y.ToString("R", CultureInfo.InvariantCulture) + "," +
            member.Position.z.ToString("R", CultureInfo.InvariantCulture);

        internal static Dictionary<string, GroupSpawner.Member> LegacyAliases(IEnumerable<GroupSpawner.Group> baseline)
        {
            Dictionary<string, GroupSpawner.Member> aliases = new(StringComparer.Ordinal);
            foreach (var group in baseline)
                foreach (var member in group.Members)
                    if (!aliases.TryAdd(LegacyMemberKey(group.Name, member), member))
                        throw new InvalidDataException("Ambiguous legacy spawn key");
            return aliases;
        }

        private static void Require(JsonElement node, params string[] names)
        {
            if (node.ValueKind != JsonValueKind.Object || names.Any(n => !node.TryGetProperty(n, out _)))
                throw new InvalidDataException("Incomplete spawn state");
        }

        private static void UniqueProperties(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                HashSet<string> names = new(StringComparer.Ordinal);
                foreach (var property in node.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate spawn-state property");
                    UniqueProperties(property.Value);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
                foreach (var value in node.EnumerateArray()) UniqueProperties(value);
        }

        internal static State Read(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            UniqueProperties(doc.RootElement);
            Require(doc.RootElement, "Version", "VisitedScenes", "Groups", "Identities");
            foreach (var entry in doc.RootElement.GetProperty("Groups").EnumerateObject())
            {
                Require(entry.Value, "Quota", "Fingerprint", "Processed", "Pending");
                foreach (var decision in entry.Value.GetProperty("Processed").EnumerateObject()) Require(decision.Value, "Scene", "Outcome");
                foreach (var pending in entry.Value.GetProperty("Pending").EnumerateArray()) Require(pending, "Id", "Scene");
            }
            State state = JsonSerializer.Deserialize<State>(json) ?? throw new InvalidDataException("Null spawn state");
            if (state.Version != 2 || state.VisitedScenes == null || state.Groups == null || state.Identities == null
                || state.VisitedScenes.Any(string.IsNullOrWhiteSpace)
                || state.Identities.Any(p => string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Value))
                || state.Identities.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != state.Identities.Count)
                throw new InvalidDataException("Unsupported or invalid spawn state");
            state.VisitedScenes = new(state.VisitedScenes, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in state.Groups)
            {
                GroupState group = pair.Value;
                if (string.IsNullOrWhiteSpace(pair.Key) || group == null || group.Quota < 0 || group.Fingerprint == null
                    || group.Processed == null || group.Pending == null
                    || group.Pending.Any(p => p == null || string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Scene))
                    || group.Pending.Select(p => p.Id).Distinct().Count() != group.Pending.Count
                    || group.Pending.Count > Math.Max(0, group.Quota - group.Processed.Count))
                    throw new InvalidDataException("Invalid group quota or reservations");
                foreach (var decision in group.Processed)
                    if (decision.Value == null || string.IsNullOrWhiteSpace(decision.Value.Scene)
                        || !state.VisitedScenes.Contains(decision.Value.Scene) || !Enum.IsDefined(typeof(Outcome), decision.Value.Outcome))
                        throw new InvalidDataException("Invalid processed spawn decision");
                foreach (string id in group.Processed.Keys.Concat(group.Pending.Select(p => p.Id)))
                    if (!state.Identities.TryGetValue(id, out string? owner) || !owner.StartsWith(pair.Key + ":", StringComparison.Ordinal)
                        || group.Pending.Any(p => p.Id == id) && group.Processed.ContainsKey(id))
                        throw new InvalidDataException("Invalid spawn identity ownership");
            }
            return state;
        }

        private static LegacyState ReadLegacy(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            UniqueProperties(doc.RootElement);
            Require(doc.RootElement, "Version", "LegacyScenes", "Selections", "Outcomes");
            LegacyState state = JsonSerializer.Deserialize<LegacyState>(json) ?? throw new InvalidDataException("Null V1 state");
            if (state.Version != 1 || state.LegacyScenes == null || state.Selections == null || state.Outcomes == null
                || state.LegacyScenes.Keys.Any(string.IsNullOrWhiteSpace)
                || state.Selections.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value == null
                    || p.Value.Any(string.IsNullOrWhiteSpace) || p.Value.Distinct().Count() != p.Value.Count)
                || state.Outcomes.Any(p => !Enum.IsDefined(typeof(Outcome), p.Value)))
                throw new InvalidDataException("Unsupported or invalid V1 state");
            state.LegacyScenes = new(state.LegacyScenes, StringComparer.OrdinalIgnoreCase);
            state.Selections = new(state.Selections, StringComparer.OrdinalIgnoreCase);
            return state;
        }

        internal static bool IsGun(string group) => group == "SG_Shotgun";
        private static int Quota(GroupSpawner.Group group, string level) => IsGun(group.Name)
            ? (ShotgunSpawnPolicy.ResourceRank(level) == (int)ShotgunResourceLevel.VeryHigh ? 2 : 1)
            : GroupSpawner.PicksFor(group, level);

        private static void Register(State state, GroupSpawner.Group group)
        {
            foreach (var member in group.Members)
            {
                string identity = group.Name + ":" + member.Prefab;
                if (state.Identities.Keys.Any(id => id != member.Id && string.Equals(id, member.Id, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"Spawn ID changed case: {member.Id}");
                if (state.Identities.TryGetValue(member.Id, out string? old) && old != identity)
                    throw new InvalidDataException($"Spawn ID reused for another prefab/group: {member.Id}");
                state.Identities[member.Id] = identity;
            }
        }

        private static string Fingerprint(GroupSpawner.Group group, Func<string, bool> available, bool enabled)
        {
            string roster = (enabled ? "on\n" : "off\n") + string.Join("\n", group.Members.OrderBy(m => m.Id, StringComparer.Ordinal)
                .Select(m => $"{m.Id}:{m.Scene}:{m.Prefab}:{available(m.Scene)}"));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(roster)));
        }

        internal static State Migrate(string? legacyJson, IEnumerable<string> savedScenes, IReadOnlyList<GroupSpawner.Group> baseline,
            string level, Func<string, bool> available, Func<string, Random> rng, int gunCount)
        {
            LegacyState? legacy = legacyJson == null ? null : ReadLegacy(legacyJson);
            State state = new() { VisitedScenes = new(savedScenes, StringComparer.OrdinalIgnoreCase) };
            if (legacy != null)
                state.VisitedScenes.UnionWith(legacy.LegacyScenes.Keys.Concat(legacy.Selections.Keys));
            var aliases = LegacyAliases(baseline);
            foreach (var source in baseline)
            {
                Register(state, source);
                GroupState group = new() { Quota = Quota(source, level) };
                state.Groups.Add(source.Name, group);
                if (IsGun(source.Name) && legacy != null)
                    foreach (var selection in legacy.Selections)
                        foreach (string key in selection.Value)
                        {
                            if (!key.StartsWith(source.Name + ":", StringComparison.Ordinal) || !legacy.Outcomes.TryGetValue(key, out Outcome outcome))
                                throw new InvalidDataException("V1 selection has no matching outcome/group");
                            string id = aliases.TryGetValue(key, out var member) ? member.Id
                                : "legacy:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
                            if (member != null && !string.Equals(member.Scene, selection.Key, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("V1 selection belongs to another scene");
                            state.Identities.TryAdd(id, source.Name + ":<legacy>");
                            if (!group.Processed.TryAdd(id, new Decision { Scene = selection.Key, Outcome = outcome }))
                                throw new InvalidDataException("Repeated V1 selection");
                        }

                var pool = source.Members.Where(m => available(m.Scene)).ToList();
                var oldPicks = GroupSpawner.Pick(pool, GroupSpawner.PicksFor(source, level), rng(source.Name));
                var modernPicks = IsGun(source.Name) ? GroupSpawner.Pick(pool, gunCount, rng(source.Name)) : oldPicks;
                foreach (var member in source.Members.Where(m => state.VisitedScenes.Contains(m.Scene)))
                {
                    if (IsGun(source.Name) && legacy?.Selections.ContainsKey(member.Scene) == true) continue;
                    bool modern = IsGun(source.Name) && legacy != null && legacy.LegacyScenes.TryGetValue(member.Scene, out bool old) && !old;
                    if ((modern ? modernPicks : oldPicks).Any(m => m.Id == member.Id))
                        group.Processed.TryAdd(member.Id, new Decision { Scene = member.Scene, Outcome = Outcome.Usable });
                }
                group.Pending = modernPicks.Where(m => !state.VisitedScenes.Contains(m.Scene) && !group.Processed.ContainsKey(m.Id))
                    .Take(Math.Max(0, group.Quota - group.Processed.Count)).Select(m => new Reservation { Id = m.Id, Scene = m.Scene }).ToList();
                group.Fingerprint = Fingerprint(source, available, !IsGun(source.Name) || gunCount > 0);
            }
            return state;
        }

        // Covers a native scene save that succeeded while a ModData write did not.
        internal static void ReconcileSavedScenes(State state, IEnumerable<string> savedScenes)
        {
            state.VisitedScenes.UnionWith(savedScenes);
            foreach (var group in state.Groups.Values)
                foreach (var pending in group.Pending.Where(p => state.VisitedScenes.Contains(p.Scene)).ToList())
                {
                    group.Processed[pending.Id] = new Decision { Scene = pending.Scene, Outcome = Outcome.Empty };
                    group.Pending.Remove(pending);
                }
        }

        internal static void Update(State state, IReadOnlyList<GroupSpawner.Group> sources, string level,
            Func<string, bool> available, Func<string, Random> rng, int gunCount)
        {
            foreach (var removed in state.Groups.Where(p => !sources.Any(g => g.Name == p.Key)))
            {
                removed.Value.Pending.Clear();
                removed.Value.Fingerprint = ""; // Reintroduced groups must exclude scenes visited while absent.
            }
            foreach (var source in sources)
            {
                Register(state, source);
                if (!state.Groups.TryGetValue(source.Name, out var group))
                    state.Groups.Add(source.Name, group = new GroupState { Quota = Quota(source, level) });
                bool enabled = !IsGun(source.Name) || gunCount > 0;
                string fingerprint = Fingerprint(source, available, enabled);
                if (group.Fingerprint == fingerprint) continue;
                int remaining = Math.Max(0, group.Quota - group.Processed.Count);
                var pool = source.Members.OrderBy(m => m.Id, StringComparer.Ordinal)
                    .Where(m => available(m.Scene) && !state.VisitedScenes.Contains(m.Scene) && !group.Processed.ContainsKey(m.Id));
                group.Pending = GroupSpawner.Pick(pool, enabled ? remaining : 0, rng(source.Name))
                    .Select(m => new Reservation { Id = m.Id, Scene = m.Scene }).ToList();
                group.Fingerprint = fingerprint;
            }
        }

        internal static List<Placement> Plan(State state, IReadOnlyList<GroupSpawner.Group> groups, string scene,
            Func<string, GroupSpawner.Member, Outcome> outcome)
        {
            if (state.VisitedScenes.Contains(scene)) return new();
            List<Placement> result = new();
            foreach (var group in groups)
                foreach (var member in group.Members.Where(m => string.Equals(m.Scene, scene, StringComparison.OrdinalIgnoreCase)))
                    if (state.Groups[group.Name].Pending.Any(p => p.Id == member.Id))
                        result.Add(new Placement(group.Name, member, outcome(group.Name, member)));
            return result;
        }

        internal static void Commit(State state, string scene, IEnumerable<Placement> plan)
        {
            foreach (var placement in plan)
            {
                var group = state.Groups[placement.Group];
                group.Processed.TryAdd(placement.Member.Id, new Decision { Scene = scene, Outcome = placement.Outcome });
                group.Pending.RemoveAll(p => p.Id == placement.Member.Id);
            }
            state.VisitedScenes.Add(scene); // Also preserves a scene with no selected points.
        }
    }
}
