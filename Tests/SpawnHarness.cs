using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Il2Cpp;
using ModData;
using Shotgun;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Ledger = Shotgun.SpawnLedger;

internal static class SpawnHarness
{
    private static int checks;
    private static readonly Func<string, Random> Rng = _ => new Random(913);
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (Exception e) when (e is InvalidDataException or JsonException or InvalidOperationException or ArgumentException) { rejected = true; }
        Check(rejected, message);
    }
    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    private static GroupSpawner.Member Member(string id, string scene, float x = 1) =>
        new(id, scene, "GEAR_DoubleBarrel", new Vector3(x, 2, 3), Quaternion.Euler(new Vector3(0, 90, 0)));
    private static GroupSpawner.Group Group(string name = "Fixture", int count = 1, int members = 3) => new()
    {
        Name = name, Picks = new(StringComparer.OrdinalIgnoreCase) { ["high"] = count, ["medium"] = 0, ["veryhigh"] = 2 },
        Members = Enumerable.Range(1, members).Select(i => Member($"{name}_{i:000}", $"Scene{i}", i)).ToList()
    };
    private static Ledger.State Fresh(GroupSpawner.Group g, int gunCount = 1) => Ledger.Migrate(null, Array.Empty<string>(),
        new[] { g }, "High", _ => true, Rng, gunCount);
    private static void Update(Ledger.State state, GroupSpawner.Group g, Func<string, bool>? available = null, int gunCount = 1) =>
        Ledger.Update(state, new[] { g }, "High", available ?? (_ => true), Rng, gunCount);
    private static List<Ledger.Placement> Process(Ledger.State state, GroupSpawner.Group g, string scene, Ledger.Outcome outcome = Ledger.Outcome.Usable)
    {
        var plan = Ledger.Plan(state, new[] { g }, scene, (_, _) => outcome);
        Ledger.Commit(state, scene, plan); return plan;
    }

    private static void DataContracts(string root)
    {
        string frozen = Resource("Shotgun.legacy-v1-groupspawns.txt").Replace("\r\n", "\n");
        Check(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(frozen))).ToLowerInvariant() == GroupSpawner.BaselineHash, "Migration baseline seal");
        var baseline = GroupSpawner.Parse(frozen);
        var current = GroupSpawner.Parse(Resource("Shotgun.groupspawns.txt"));
        Check(baseline.Count == 110 && baseline.Sum(g => g.Members.Count) == 283, "Original 110 groups / 283 points");
        Check(Ledger.LegacyAliases(baseline).Count == 283, "One unambiguous old key per point");
        var gunBaseline = baseline.Single(g => g.Name == "SG_Shotgun");
        Check(GroupSpawner.Pick(gunBaseline.Members, 1, GroupSpawner.RngFor(gunBaseline.Name)).Single().Scene == "MiningRegionMine", "Golden original seed 12345:1 with optional region");
        Check(GroupSpawner.Pick(gunBaseline.Members.Where(m => !m.Scene.StartsWith("Mod")), 1, GroupSpawner.RngFor(gunBaseline.Name)).Single().Scene == "BlackrockInteriorASurvival", "Golden original seed without optional region");
        foreach (var old in baseline)
        {
            var now = current.Single(g => g.Name == old.Name);
            Check(old.Picks.OrderBy(p => p.Key).SequenceEqual(now.Picks.OrderBy(p => p.Key)), $"Counts unchanged: {old.Name}");
            Check(old.Members.SequenceEqual(now.Members), $"Identity, prefab, scene, position and rotation unchanged: {old.Name}");
        }
        string catalog = File.ReadAllText(Path.Combine(root, "Main/ShotgunAmmoCatalog.cs"));
        var loads = Regex.Matches(catalog, @"new\(ShotgunPellets\.ShotgunAmmo\.(\w+),").Select(m => m.Groups[1].Value);
        Check(loads.SequenceEqual(new[] { "Slug", "Buckshot", "Birdshot", "Scrapshot" }), "Native selected-ammo index order contract");
        var ids = current.SelectMany(g => g.Members).Select(m => m.Prefab).Distinct().OrderBy(x => x);
        Check(ids.SequenceEqual(new[] { "GEAR_DoubleBarrel", "GEAR_DoubleBarrelAmmoBoxBirdshot", "GEAR_DoubleBarrelAmmoBoxBuckshot", "GEAR_DoubleBarrelAmmoBoxSlug", "GEAR_DoubleBarrelAmmoSingleBirdshot", "GEAR_DoubleBarrelAmmoSingleBuckshot", "GEAR_DoubleBarrelAmmoSingleSlug", "GEAR_DoubleBarrelPartBarrel", "GEAR_DoubleBarrelPartForend", "GEAR_DoubleBarrelPartStock", "GEAR_DoubleBarrelPartTrigger" }.OrderBy(x => x)), "Spawn gear-ID contract");
        string valid = "group=Fixture scene=Scene1 high=1\nitem=GEAR_DoubleBarrel id=Fixture_001 p=1,2,3 r=0,0,0\n";
        Reject(() => GroupSpawner.Parse(valid.Replace(" id=Fixture_001", "")), "Missing ID rejected");
        Reject(() => GroupSpawner.Parse(valid + "item=GEAR_DoubleBarrel id=Fixture_001 p=4,5,6 r=0,0,0"), "Duplicate ID rejected");
        Reject(() => GroupSpawner.Parse(valid + "nonsense"), "Unknown line rejected");
        Reject(() => GroupSpawner.Parse(valid.Replace("p=1,2,3", "p=1,2")), "Malformed vector rejected");
        Reject(() => GroupSpawner.Parse(valid.Replace(" scene=Scene1", "")), "Missing scene rejected");
        Reject(() => GroupSpawner.Parse(valid.Replace("high=1", "high=no")), "Malformed count rejected");
        Reject(() => GroupSpawner.Parse(valid + valid), "Repeated group rejected");
        var ambiguous = Group(); ambiguous.Members.Add(ambiguous.Members[0] with { Id = "AnotherID" });
        Reject(() => Ledger.LegacyAliases(new[] { ambiguous }), "Ambiguous historical alias rejected");
    }

    private static void QuotasAndUpdates()
    {
        var group = Group(count: 2, members: 5);
        var state = Fresh(group);
        var ledger = state.Groups[group.Name];
        var expected = GroupSpawner.Pick(group.Members, 2, Rng(group.Name)).Select(m => m.Id);
        Check(ledger.Pending.Select(p => p.Id).SequenceEqual(expected), "Initial lottery preserves old RNG/order");
        string unselectedScene = group.Members.First(m => ledger.Pending.All(p => p.Id != m.Id)).Scene;
        string originalPending = JsonSerializer.Serialize(ledger.Pending);
        Process(state, group, unselectedScene);
        Update(state, group);
        Check(JsonSerializer.Serialize(ledger.Pending) == originalPending, "Visiting an unselected scene does not reroll");
        string firstScene = ledger.Pending[0].Scene;
        var first = Process(state, group, firstScene, Ledger.Outcome.Empty).Single();
        Check(ledger.Processed.Count == 1 && ledger.Pending.Count == 1, "Empty consumes a slot");
        group.Members.Add(Member("Fixture_006", "AddedScene")); Update(state, group);
        Check(ledger.Processed.Count == 1 && ledger.Pending.Count == 1, "Added point respects remaining quota");
        Check(ledger.Pending.All(p => !state.VisitedScenes.Contains(p.Scene)), "Updated choices exclude all visited scenes");
        string fingerprint = ledger.Fingerprint;
        string beforeReorder = JsonSerializer.Serialize(ledger.Pending);
        group.Members.Reverse(); Update(state, group);
        Check(ledger.Fingerprint == fingerprint && JsonSerializer.Serialize(ledger.Pending) == beforeReorder, "Reordering cannot change a plan");
        var pendingMember = group.Members.Single(m => m.Id == ledger.Pending[0].Id);
        group.Members[group.Members.IndexOf(pendingMember)] = pendingMember with { Position = new Vector3(999, 2, 3) };
        Update(state, group);
        Check(ledger.Fingerprint == fingerprint && JsonSerializer.Serialize(ledger.Pending) == beforeReorder, "Coordinate correction preserves identity/lottery");
        var moved = Process(state, group, ledger.Pending[0].Scene).Single();
        Check(moved.Member.Position.x == 999 && ledger.Processed.Count == 2, "Unvisited placement uses corrected coordinates");
        group.Members.RemoveAll(m => m.Id == first.Member.Id); group.Members.Add(Member("Fixture_007", "LaterScene")); Update(state, group);
        Check(ledger.Processed.Count == 2 && ledger.Pending.Count == 0, "Removed processed point never refunds quota");
        Check(Ledger.Plan(state, new[] { group }, firstScene, (_, _) => Ledger.Outcome.Usable).Count == 0, "Visited scene cannot refill");
        var roundtrip = Ledger.Read(JsonSerializer.Serialize(state));
        Check(roundtrip.Groups[group.Name].Processed.Count == 2 && roundtrip.VisitedScenes.Contains(firstScene.ToUpperInvariant()), "State roundtrip and scene comparer");
        var reused = group.Members[0] with { Prefab = "GEAR_Other" };
        group.Members[0] = reused;
        Reject(() => Update(roundtrip, group), "Persisted ID cannot change prefab");

        group = Group(); state = Fresh(group); ledger = state.Groups[group.Name];
        string absent = ledger.Pending[0].Scene;
        Update(state, group, scene => scene != absent);
        Check(ledger.Pending.Count == 1 && ledger.Pending[0].Scene != absent, "Unavailable unresolved region can be replaced");
        Process(state, group, ledger.Pending[0].Scene);
        Update(state, group);
        Check(ledger.Pending.Count == 0, "Region return cannot restore spent quota");
        var added = Group("NewGroup", members: 1); added.Members[0] = added.Members[0] with { Scene = state.VisitedScenes.First() };
        Ledger.Update(state, new[] { group, added }, "High", _ => true, Rng, 1);
        Check(state.Groups[added.Name].Pending.Count == 0, "New group cannot fill a visited scene");
        added.Members.Add(Member("NewGroup_002", "NeverVisited"));
        Ledger.Update(state, new[] { group, added }, "High", _ => true, Rng, 1);
        Check(state.Groups[added.Name].Pending.Single().Scene == "NeverVisited", "New group can fill unvisited content");
        Ledger.Update(state, new[] { group }, "High", _ => true, Rng, 1);
        state.VisitedScenes.Add("NeverVisited");
        added.Members.Add(Member("NewGroup_003", "AfterReturn"));
        Ledger.Update(state, new[] { group, added }, "High", _ => true, Rng, 1);
        Check(state.Groups[added.Name].Pending.Single().Scene == "AfterReturn", "Reintroduced group excludes scenes visited while absent");
        var renamed = added.Members[0]; added.Members[0] = renamed with { Id = renamed.Id.ToLowerInvariant() };
        Reject(() => Ledger.Update(state, new[] { added }, "High", _ => true, Rng, 1), "ID case changes cannot create a fresh identity");
    }

    private static void MigrationAndValidation()
    {
        var gun = Group("SG_Shotgun");
        var oldPicked = GroupSpawner.Pick(gun.Members, 1, Rng(gun.Name)).Single();
        var authoritative = gun.Members.First(m => m.Id != oldPicked.Id);
        string key = Ledger.LegacyMemberKey(gun.Name, authoritative);
        var v1 = new Ledger.LegacyState
        {
            Version = 1, LegacyScenes = new() { [authoritative.Scene] = false },
            Selections = new() { [authoritative.Scene] = new() { key } }, Outcomes = new() { [key] = Ledger.Outcome.Ruined }
        };
        var migrated = Ledger.Migrate(JsonSerializer.Serialize(v1), new[] { authoritative.Scene }, new[] { gun }, "High", _ => true, Rng, 1);
        Check(migrated.Groups[gun.Name].Processed[authoritative.Id].Outcome == Ledger.Outcome.Ruined, "V1 beats reconstructed lottery");
        Check(migrated.Groups[gun.Name].Pending.Count == 0, "V1 selected slot consumes quota");
        v1.Selections[authoritative.Scene].Clear(); v1.Outcomes.Clear();
        migrated = Ledger.Migrate(JsonSerializer.Serialize(v1), new[] { authoritative.Scene }, new[] { gun }, "High", _ => true, Rng, 1);
        Check(migrated.Groups[gun.Name].Processed.Count == 0, "Authoritative V1 empty scene does not acquire reconstructed finds");
        const string retired = "SG_Shotgun:RetiredScene:GEAR_DoubleBarrel:100,2,3";
        v1.Selections = new() { ["RetiredScene"] = new() { retired } }; v1.Outcomes = new() { [retired] = Ledger.Outcome.Empty };
        migrated = Ledger.Migrate(JsonSerializer.Serialize(v1), Array.Empty<string>(), new[] { gun }, "High", _ => true, Rng, 1);
        Check(migrated.Groups[gun.Name].Processed.Keys.Single().StartsWith("legacy:") && migrated.Groups[gun.Name].Pending.Count == 0, "Unknown historical key retained and charged");
        Check(Ledger.Read(JsonSerializer.Serialize(migrated)).Groups[gun.Name].Processed.Count == 1, "Historical tombstone survives roundtrip");
        migrated = Ledger.Migrate(null, new[] { oldPicked.Scene }, new[] { gun }, "High", _ => true, Rng, 1);
        Check(migrated.Groups[gun.Name].Processed.ContainsKey(oldPicked.Id) && migrated.Groups[gun.Name].Pending.Count == 0, "Missing history replays old lottery");
        migrated = Fresh(gun);
        Ledger.ReconcileSavedScenes(migrated, new[] { migrated.Groups[gun.Name].Pending[0].Scene });
        Check(migrated.Groups[gun.Name].Processed.Count == 1 && migrated.Groups[gun.Name].Pending.Count == 0, "Native save ahead of ledger never refunds a reserved slot");
        string valid = JsonSerializer.Serialize(migrated);
        Reject(() => Ledger.Read(valid.Replace("\"Version\":2", "\"Version\":3")), "Unknown V2 format rejected");
        Reject(() => Ledger.Read("{}"), "Missing root fields rejected");
        Reject(() => Ledger.Read(valid.Replace("\"Quota\":1", "\"Quota\":-1")), "Negative quota rejected");
        Reject(() => Ledger.Read(valid.Replace("\"Outcome\":0", "\"Outcome\":99")), "Unknown outcome rejected");
        Reject(() => Ledger.Read(valid.Replace("\"Version\":2", "\"Version\":2,\"Version\":2")), "Duplicate JSON property rejected");
        v1.Outcomes.Clear();
        Reject(() => Ledger.Migrate(JsonSerializer.Serialize(v1), Array.Empty<string>(), new[] { gun }, "High", _ => true, Rng, 1), "Incomplete V1 cannot reroll");
    }

    private static void Reset(bool preserveData = false)
    {
        ShotgunSpawnPolicy.Reset();
        GroupSpawner.SetGroups(new[] { Group("Runtime", members: 1) });
        if (!preserveData) ModDataManager.Values.Clear();
        ModDataManager.FailWrite = false; ModDataCore.cacheLoaded = true;
        SaveGameSystem.CurrentSave = "fixture"; SaveGameSlots.Scenes = new();
        GameManager.m_ActiveScene = "Boot"; GameManager.m_SceneTransitionData = new();
        ExperienceModeManager.Instance = new(); ExperienceModeManager.Level = ResourceLevel.High;
        ExperienceModeManager.Kind = ExperienceModeType.Other; ExperienceModeManager.s_CurrentGameMode = new();
        ModComponent.Public.Ready = true; Settings.Instance = new();
        ShotgunAvailability.Allowed = true; ShotgunAvailability.Loaded = false;
        Addressables.AbsentScenes.Clear(); Addressables.MissingPrefabs.Clear(); Addressables.Queries = 0;
        Addressables.FailLocations = false;
        // A fixture reset simulates process restart, including Addressables ownership.
        ((System.Collections.IDictionary)typeof(GroupSpawner).GetField("prefabHandles", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Clear();
        GameObject.Spawned.Clear(); GameObject.FailInstantiate = false; Time.realtimeSinceStartup += 2;
        MelonLoader.MelonLogger.Messages.Clear();
    }
    private static void Enter(string scene = "Scene1") { GameManager.m_ActiveScene = scene; GroupSpawner.RequestScene(scene); }
    private static void Tick() { Time.realtimeSinceStartup += 2; GroupSpawner.Tick(); }
    private static Ledger.State Stored() => Ledger.Read(ModDataManager.Values[(SaveGameSystem.CurrentSave, ShotgunSpawnPolicy.SaveKey)]);

    private static void RuntimeLifecycle()
    {
        Reset(); ModDataCore.cacheLoaded = false; Enter();
        Check(GameObject.Spawned.Count == 0 && !ShotgunSpawnPolicy.Save(), "Unloaded ModData is not mistaken for a new save");
        ModDataCore.cacheLoaded = true; Tick();
        Check(GameObject.Spawned.Count == 1, "Ready-cache retry completes");
        Enter(); Tick(); Check(GameObject.Spawned.Count == 1, "Repeated quality callbacks cannot duplicate");
        Check(ShotgunSpawnPolicy.Save(), "Ordinary V2 save succeeds");
        Check(!ModDataManager.Values.ContainsKey(("fixture", ShotgunSpawnPolicy.LegacySaveKey)), "V1 key is never written");
        Check(Stored().Groups["Runtime"].Processed.Count == 1, "Runtime group uses production ledger");
        ModDataManager.Values[("fixture", ShotgunSpawnPolicy.LegacySaveKey)] = "malformed obsolete V1";
        Reset(preserveData: true); SaveGameSlots.Scenes!.Add("Scene1"); Enter();
        Check(GameObject.Spawned.Count == 0 && ShotgunSpawnPolicy.Save(), "V2 wins over obsolete V1; native scene is not filled");
        Check(ModDataManager.Values[("fixture", ShotgunSpawnPolicy.LegacySaveKey)] == "malformed obsolete V1", "Legacy bytes preserved");

        Reset(); SaveGameSlots.Scenes = null; Enter();
        Check(GameObject.Spawned.Count == 0 && !ShotgunSpawnPolicy.Save(), "Unavailable scene keys defer without migration");
        SaveGameSlots.Scenes = new(); Tick(); Check(GameObject.Spawned.Count == 1, "Scene-key retry completes");
        Reset(); ModComponent.Public.Ready = false; Enter();
        Check(Addressables.Queries == 0 && GameObject.Spawned.Count == 0, "Unready catalog is not queried/committed");
        ModComponent.Public.Ready = true; Tick(); Check(GameObject.Spawned.Count == 1, "Catalog retry completes");
        Reset(); Addressables.FailLocations = true; Enter();
        Check(GameObject.Spawned.Count == 0 && !ShotgunSpawnPolicy.Save(), "Failed catalog lookup cannot become an empty pool");
        Addressables.FailLocations = false; Tick(); Check(GameObject.Spawned.Count == 1, "Failed catalog lookup retries without corrupting state");
        Reset(); SaveGameSystem.CurrentSave = ""; Enter();
        Check(GameObject.Spawned.Count == 0 && !ShotgunSpawnPolicy.Save(), "Missing slot name defers");
        SaveGameSystem.CurrentSave = "fixture"; Tick(); Check(GameObject.Spawned.Count == 1, "Slot name becoming available resumes deferred work");
        Reset(); Addressables.MissingPrefabs.Add("GEAR_DoubleBarrel"); Enter();
        Check(GameObject.Spawned.Count == 0 && ShotgunSpawnPolicy.Save() && !Stored().VisitedScenes.Contains("Scene1"), "Missing prefab does not commit a scene");
        Addressables.MissingPrefabs.Clear(); Tick(); Check(GameObject.Spawned.Count == 1, "Prefab recovery uses same pending plan");
        Reset(); GameObject.FailInstantiate = true; Enter();
        Check(GameObject.Spawned.Count == 0, "Native instantiation failure caught");
        GameObject.FailInstantiate = false; Tick(); Enter();
        Check(GameObject.Spawned.Count == 1, "Native failure retries exact committed plan once");

        Reset(); Enter(); ModDataManager.FailWrite = true;
        Check(!ShotgunSpawnPolicy.Save() && !ModDataManager.Values.ContainsKey(("fixture", ShotgunSpawnPolicy.SaveKey)), "Failed write cannot claim success");
        ModDataManager.FailWrite = false;
        Check(ShotgunSpawnPolicy.Save() && Stored().Groups["Runtime"].Processed.Count == 1, "Next normal save retries retained decisions");
        Reset(); string bad = "{\"Version\":99,\"VisitedScenes\":[],\"Groups\":{},\"Identities\":{}}";
        ModDataManager.Values[("fixture", ShotgunSpawnPolicy.SaveKey)] = bad;
        Enter(); Tick(); Check(GameObject.Spawned.Count == 0 && !ShotgunSpawnPolicy.Save(), "Unknown V2 blocks new injection");
        Check(ModDataManager.Values[("fixture", ShotgunSpawnPolicy.SaveKey)] == bad, "Unknown state not overwritten");
        Reset(); ModDataManager.Values[("fixture", ShotgunSpawnPolicy.LegacySaveKey)] = "{broken"; Enter();
        Check(GameObject.Spawned.Count == 0 && !ShotgunSpawnPolicy.Save(), "Corrupt V1 never falls back to legacy spawn");

        Reset(); ModComponent.Public.Ready = false; Enter();
        GroupSpawner.ForgetScene("Scene1"); ModComponent.Public.Ready = true; Tick();
        Check(GameObject.Spawned.Count == 0, "Unload cancels deferred work");
        Reset(); ModComponent.Public.Ready = false; Enter(); SaveGameSystem.CurrentSave = "other";
        ModComponent.Public.Ready = true; Tick(); Check(GameObject.Spawned.Count == 0, "Slot switch cancels deferred work");
        Enter(); Check(GameObject.Spawned.Count == 1 && ShotgunSpawnPolicy.Save(), "New slot gets its own plan/attempts");
        Check(ModDataManager.Values.ContainsKey(("other", ShotgunSpawnPolicy.SaveKey)) && !ModDataManager.Values.ContainsKey(("fixture", ShotgunSpawnPolicy.SaveKey)), "State cannot cross slots");
        Reset(); ShotgunAvailability.Allowed = false; Enter();
        Check(GameObject.Spawned.Count == 0 && ShotgunSpawnPolicy.Save() && Stored().Groups["Runtime"].Processed.Values.Single().Outcome == Ledger.Outcome.Empty, "Ban records empty without creating gear");
    }

    private static void Chances()
    {
        Reset();
        Check(ShotgunSpawnPolicy.CandidateCount("Low") == 0 && ShotgunSpawnPolicy.CandidateCount("Medium") == 1
            && ShotgunSpawnPolicy.CandidateCount("High") == 1 && ShotgunSpawnPolicy.CandidateCount("VeryHigh") == 2, "Resource-level defaults");
        Check(ShotgunSpawnPolicy.Roll(0, 0, 0) == Ledger.Outcome.Empty && ShotgunSpawnPolicy.Roll(99, 100, 0) == Ledger.Outcome.Usable
            && ShotgunSpawnPolicy.Roll(99, 0, 100) == Ledger.Outcome.Ruined && ShotgunSpawnPolicy.Roll(50, 50, 0) == Ledger.Outcome.Empty, "Chance endpoints and empty remainder");
        var gun = Group("SG_Shotgun"); var state = Fresh(gun, 0);
        Check(state.Groups[gun.Name].Quota == 1 && state.Groups[gun.Name].Pending.Count == 0, "Disabled gun eligibility retains base quota");
        Update(state, gun, gunCount: 1); var selected = state.Groups[gun.Name].Pending.Single();
        Process(state, gun, selected.Scene, Ledger.Outcome.Empty); Update(state, gun, gunCount: 0); Update(state, gun, gunCount: 1);
        Check(state.Groups[gun.Name].Pending.Count == 0, "Settings cannot refill a processed empty slot");
        Reset(); Settings.Instance.UsableShotgunChance = 0; Settings.Instance.RuinedShotgunChance = 100;
        var singleton = Group("SG_Shotgun", members: 1); GroupSpawner.SetGroups(new[] { singleton }); Enter();
        Check(GameObject.Spawned.Count == 1 && GameObject.Spawned[0].Ruined, "Production outcome marks only newly spawned ruined gun");
    }

    private static int Main(string[] args)
    {
        try
        {
            DataContracts(args[0]); QuotasAndUpdates(); MigrationAndValidation(); RuntimeLifecycle(); Chances();
            Console.WriteLine($"PASS: {checks} spawn compatibility checks; native game execution remains separate."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
