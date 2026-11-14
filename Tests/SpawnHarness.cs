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
        var evidence=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Spawns/placement-evidence.json")));
        Check(current.Sum(g=>g.Members.Count)==303 && evidence.RootElement.GetProperty("placements").GetArrayLength()==22,"Twenty measured additions and two position alternatives");
        var farm = current.Single(g => g.Name == "SG_Shotgun");
        Check(farm.Members.Count(m => m.Id == "SG_Shotgun_007") == 1
            && farm.Alternatives["SG_Shotgun_007"].Select(a => a.Name).OrderBy(x => x).SequenceEqual(new[] { "bedroom", "office" }),
            "Farmhouse remains one candidate with two alternatives");
        foreach(var entry in evidence.RootElement.GetProperty("placements").EnumerateArray())
        {
            string id=entry.GetProperty("id").GetString()!;
            var member=current.SelectMany(g=>g.Members).Single(m=>m.Id==id);
            if (entry.TryGetProperty("alternative", out var variant))
            {
                var alternative = current.Single(g => g.Members.Contains(member)).Alternatives[id].Single(a => a.Name == variant.GetString());
                member = member with { Position = alternative.Position, Rotation = alternative.Rotation };
            }
            if (id == "SG_Shotgun_007")
            {
                Check(entry.GetProperty("auditVersion").GetInt32() == 2
                    && entry.GetProperty("meshCollidersChecked").GetInt32() > 0
                    && entry.GetProperty("furnitureRenderMeshesChecked").GetInt32() > 0
                    && entry.GetProperty("structuralRenderMeshesChecked").GetInt32() > 0
                    && entry.GetProperty("renderersChecked").GetInt32() > 0
                    && entry.GetProperty("unresolvedRelevantMeshes").GetInt32() == 0
                    && entry.GetProperty("horizontalObstacleClearanceM").GetDouble() >= .1
                    && entry.GetProperty("minimumFurnitureBoundsClearanceM").GetDouble() >= .1,
                    "Farmhouse evidence requires complete mesh coverage and ten-centimetre clearance");
                string template = Path.Combine(root, "DoubleBarrel.modcomponent");
                Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(template))).ToLowerInvariant()
                    == entry.GetProperty("modelPackageSha256").GetString(), "Farmhouse rendered-model template seal");
            }
            var xyz=entry.GetProperty("position").EnumerateArray().Select(n=>n.GetSingle()).ToArray();
            var angles=entry.GetProperty("rotation").EnumerateArray().Select(n=>n.GetSingle()).ToArray();
            Check(member.Scene==entry.GetProperty("scene").GetString() && member.Prefab==entry.GetProperty("prefab").GetString()
                && member.Position.x==xyz[0] && member.Position.y==xyz[1] && member.Position.z==xyz[2]
                && member.Rotation.Equals(Quaternion.Euler(new Vector3(angles[0], angles[1], angles[2]))),"Measured source transform: "+id);
            string prefab=Path.Combine(root,"unity/Assets/Shotgun/Prefabs",member.Prefab+".prefab");
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(prefab))).ToLowerInvariant()==entry.GetProperty("prefabSha256").GetString(),"Placement prefab envelope current: "+id);
            var placed=entry.GetProperty("placedBounds").EnumerateArray().Select(a=>a.EnumerateArray().Select(v=>v.GetDouble()).ToArray()).ToArray();
            foreach(var old in baseline.SelectMany(g=>g.Members).Where(m=>m.Scene==member.Scene))
            {
                string oldPrefab=Path.Combine(root,"unity/Assets/Shotgun/Prefabs",old.Prefab+".prefab");
                if(!File.Exists(oldPrefab))continue;
                string yaml=File.ReadAllText(oldPrefab);
                float[]? Vector(string key)
                {
                    var match=Regex.Match(yaml,key+@": \{x: ([^,]+), y: ([^,]+), z: ([^}]+)");
                    return match.Success?match.Groups.Cast<Group>().Skip(1).Select(g=>float.Parse(g.Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray():null;
                }
                var size=Vector("m_Size");var centre=Vector("m_Center");if(size==null || centre==null)continue;
                const float radians=MathF.PI/180;
                var rotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(old.Rotation.y*radians,old.Rotation.x*radians,old.Rotation.z*radians);
                var vertices=(from x in new[]{-1,1} from y in new[]{-1,1} from z in new[]{-1,1}
                    select System.Numerics.Vector3.Transform(new(centre[0]+x*size[0]/2,centre[1]+y*size[1]/2,centre[2]+z*size[2]/2),rotation)
                        +new System.Numerics.Vector3(old.Position.x,old.Position.y,old.Position.z)).ToArray();
                double[][] oldBounds={new[]{(double)vertices.Min(v=>v.X),vertices.Max(v=>v.X)},new[]{(double)vertices.Min(v=>v.Y),vertices.Max(v=>v.Y)},new[]{(double)vertices.Min(v=>v.Z),vertices.Max(v=>v.Z)}};
                Check(!Enumerable.Range(0,3).All(a=>Math.Min(placed[a][1],oldBounds[a][1])-Math.Max(placed[a][0],oldBounds[a][0])>.002),"New placement clears original collider: "+id+" / "+old.Id);
            }
        }
        Check(baseline.Count == 110 && baseline.Sum(g => g.Members.Count) == 283, "Original 110 groups / 283 points");
        Check(Ledger.LegacyAliases(baseline).Count == 283, "One unambiguous old key per point");
        var gunBaseline = baseline.Single(g => g.Name == "SG_Shotgun");
        Check(GroupSpawner.Pick(gunBaseline.Members, 1, GroupSpawner.RngFor(gunBaseline.Name)).Single().Scene == "MiningRegionMine", "Golden original seed 12345:1 with optional region");
        Check(GroupSpawner.Pick(gunBaseline.Members.Where(m => !m.Scene.StartsWith("Mod")), 1, GroupSpawner.RngFor(gunBaseline.Name)).Single().Scene == "BlackrockInteriorASurvival", "Golden original seed without optional region");
        foreach (var old in baseline)
        {
            var now = current.Single(g => g.Name == old.Name);
            Check(old.Name=="SG_Shotgun" || old.Picks.OrderBy(p => p.Key).SequenceEqual(now.Picks.OrderBy(p => p.Key)), $"Part/ammo counts unchanged: {old.Name}");
            Check(old.Members.All(m=>now.Members.Contains(m with { Scene = ShotgunSceneNames.Canonical(m.Scene) })), $"Original identity, prefab, scene, position and rotation unchanged: {old.Name}");
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
        GearManager.m_Gear.Clear(); GearItem.FailStartName=null; GameManager.Story=false; ExperienceModeManager.Challenge=false;
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
        Check(ShotgunSpawnPolicy.CandidateCount("Low") == 0 && ShotgunSpawnPolicy.CandidateCount("Medium") == 2
            && ShotgunSpawnPolicy.CandidateCount("High") == 3 && ShotgunSpawnPolicy.CandidateCount("VeryHigh") == 4, "Resource-level defaults");
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

    private static void AlternativePlacements()
    {
        string source = Resource("Shotgun.groupspawns.txt");
        var group = GroupSpawner.Parse(source).Single(g => g.Name == "SG_Shotgun");
        var unchanged = GroupSpawner.Parse(string.Join('\n', source.Split('\n').Where(l => !l.StartsWith("alternative="))))
            .Single(g => g.Name == group.Name);
        var farm = group.Members.Single(m => m.Id == "SG_Shotgun_007");
        var alternatives = group.Alternatives[farm.Id];
        string valid = "group=Fixture scene=Scene1 high=1\nitem=GEAR_DoubleBarrel id=Fixture_001 p=1,2,3 r=0,0,0\n";
        string row = "alternative=office for=Fixture_001 p=4,5,6 r=0,90,90\n";
        Check(GroupSpawner.Parse(valid + row).Single().Members.Count == 1, "Alternative is not a lottery member");
        Reject(() => GroupSpawner.Parse(valid + row + row.Replace("office", "Office")), "Case-colliding alternative names rejected");
        Reject(() => GroupSpawner.Parse(valid + row.Replace("office", "default")), "Default position name reserved");
        Reject(() => GroupSpawner.Parse(valid + row.Replace("Fixture_001", "Absent")), "Missing alternative owner rejected");
        Reject(() => GroupSpawner.Parse(valid + "group=Other scene=Scene2 high=1\n" + row), "Cross-group alternative owner rejected");
        Reject(() => GroupSpawner.Parse(valid + row.Replace("4,5,6", "4,5")), "Malformed alternative vector rejected");
        Reject(() => GroupSpawner.Parse(valid + row.Replace(" p=", " scene=Scene2 p=")), "Alternative cannot override scene");
        Reject(() => GroupSpawner.Parse(valid + row.Replace(" p=", " item=GEAR_Rifle p=")), "Alternative cannot override prefab");
        Reject(() => GroupSpawner.Parse(valid + row.TrimEnd() + " unknown=1"), "Unknown alternative field rejected");

        int oldSeed = GameManager.m_SceneTransitionData!.m_GameRandomSeed;
        var reached = new HashSet<float>();
        for (int seed = 0; seed < 96; seed++)
        {
            GameManager.m_SceneTransitionData.m_GameRandomSeed = seed;
            var before = Ledger.Migrate(null, Array.Empty<string>(), new[] { unchanged }, "High", _ => true, GroupSpawner.RngFor, 3);
            var after = Ledger.Migrate(null, Array.Empty<string>(), new[] { group }, "High", _ => true, GroupSpawner.RngFor, 3);
            Check(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after), "Alternatives preserve lottery, identities and fingerprint");
            var resolved = GroupSpawner.ResolvePlacement(group, farm);
            reached.Add(resolved.Position.x);
            Check(resolved.Id == farm.Id && resolved.Scene == farm.Scene && resolved.Prefab == farm.Prefab,
                "Resolved position preserves permanent identity");
            Check(GroupSpawner.ResolvePlacement(group, farm) == resolved, "Same seed repeats position");
            alternatives.Reverse();
            Check(GroupSpawner.ResolvePlacement(group, farm) == resolved, "Alternative text order cannot change position");
            var beforeRoll = GroupSpawner.RngFor("ShotgunOutcome:" + farm.Id).Next(100);
            GroupSpawner.ResolvePlacement(group, farm);
            Check(GroupSpawner.RngFor("ShotgunOutcome:" + farm.Id).Next(100) == beforeRoll, "Position RNG leaves outcome roll unchanged");
        }
        GameManager.m_SceneTransitionData.m_GameRandomSeed = oldSeed;
        Check(reached.SetEquals(new[] { farm.Position.x }.Concat(alternatives.Select(a => a.Position.x))), "All three farmhouse positions reachable");
        Check(ReferenceEquals(GroupSpawner.ResolvePlacement(group, group.Members[0]), group.Members[0]), "Non-alternative members retain original placement");

        var pending = Fresh(unchanged);
        string reservations = JsonSerializer.Serialize(pending);
        Update(pending, group);
        Check(JsonSerializer.Serialize(pending) == reservations, "Adding alternatives retains existing pending reservations");
        var fixture = new GroupSpawner.Group { Name = group.Name, Picks = group.Picks,
            Members = new() { farm }, Alternatives = group.Alternatives };
        Reset(); GroupSpawner.SetGroups(new[] { fixture });
        // Persist a pre-alternative pending candidate, then load it through the production policy.
        var previous = Fresh(new GroupSpawner.Group { Name = group.Name, Picks = group.Picks, Members = new() { farm } });
        Ledger.ExpandDistribution(previous, "High");
        ModDataManager.Values[("fixture", ShotgunSpawnPolicy.SaveKey)] = JsonSerializer.Serialize(previous);
        var expected = GroupSpawner.ResolvePlacement(fixture, farm);
        GameObject.FailInstantiate = true; Enter(farm.Scene);
        Check(GameObject.Spawned.Count == 0, "Failed alternative instantiation leaves no object");
        GameObject.FailInstantiate = false; Tick(); Enter(farm.Scene); Tick();
        Check(GameObject.Spawned.Count == 1 && GameObject.Spawned.Single().Position.Equals(expected.Position)
            && GameObject.Spawned.Single().transform.rotation.Equals(expected.Rotation), "Retry and repeated callbacks use exactly one resolved transform");
        Check(ShotgunSpawnPolicy.Save() && Stored().Groups[group.Name].Processed.Keys.SequenceEqual(new[] { farm.Id }),
            "Resolved farm position spends one original candidate slot");
        Reset(preserveData: true); GroupSpawner.SetGroups(new[] { fixture }); Enter(farm.Scene);
        Check(GameObject.Spawned.Count == 0, "Reloaded farm cannot refill a processed alternative");
        SaveGameSystem.CurrentSave = "second-slot"; Tick(); Enter(farm.Scene);
        Check(GameObject.Spawned.Count == 1, "Second save slot has its own farm opportunity");
        Reset(); GroupSpawner.SetGroups(new[] { fixture }); SaveGameSlots.Scenes!.Add(farm.Scene); Enter(farm.Scene);
        Check(GameObject.Spawned.Count == 0, "Native visited farmhouse remains untouched");
        Reset(); GroupSpawner.SetGroups(new[] { fixture });
        Settings.Instance.UsableShotgunChance = 0; Settings.Instance.RuinedShotgunChance = 0;
        Enter(farm.Scene);
        Check(GameObject.Spawned.Count == 0 && ShotgunSpawnPolicy.Save()
            && Stored().Groups[group.Name].Processed[farm.Id].Outcome == Ledger.Outcome.Empty, "Empty farm outcome spends one slot");
        Settings.Instance.UsableShotgunChance = 100; Enter(farm.Scene); Tick();
        Check(GameObject.Spawned.Count == 0, "Enabling finds cannot reroll an empty farm outcome");
        Reset();
    }

    private static int Main(string[] args)
    {
        try
        {
            DataContracts(args[0]); UpstreamSpawnTests.Run(); QuotasAndUpdates(); MigrationAndValidation(); RuntimeLifecycle(); Chances(); DistributionChecks(); AlternativePlacements();
            Console.WriteLine($"PASS: {checks} spawn compatibility checks; native game execution remains separate."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static void DistributionChecks()
    {
        var guns=Group("SG_Shotgun",members:8); var state=Fresh(guns);
        var old=state.Groups[guns.Name].Pending.Single(); Process(state,guns,old.Scene,Ledger.Outcome.Empty);
        state.VisitedScenes.Add("OldUnopenedContainer");
        Ledger.ExpandDistribution(state,"High"); Update(state,guns,gunCount:3);
        Check(state.Groups[guns.Name].Quota==3 && state.Groups[guns.Name].Pending.Count==2,"Expansion subtracts old empty slot");
        Check(!ShotgunDistribution.AllowsScene(state,"oldunopenedcontainer") && ShotgunDistribution.AllowsScene(state,"NewInterior"),"Frozen old scenes exclude containers; new interior allowed");
        state.VisitedScenes.Add("NewInterior"); Ledger.ExpandDistribution(state,"High");
        Check(ShotgunDistribution.AllowsScene(state,"NewInterior"),"New scene retains expanded container rules after revisit");
        Check(Ledger.Read(JsonSerializer.Serialize(state)).DistributionVersion==1,"Distribution state roundtrip");
        var incomplete=JsonSerializer.SerializeToNode(state)!;incomplete.AsObject().Remove("PreDistributionScenes");
        Reject(()=>Ledger.Read(incomplete.ToJsonString()),"Expanded state without frozen scene snapshot fails closed");
        Check(Ledger.DistributionQuota("Low")==0 && Ledger.DistributionQuota("Medium")==2 && Ledger.DistributionQuota("VeryHigh")==4,"Expansion quota table");
        int rolls=0;
        Check(!ShotgunDistribution.ChooseRifle(state,"NewInterior","zero",true,0,()=>{rolls++;return 0;}),"Zero percent retains rifle");
        Check(ShotgunDistribution.ChooseRifle(state,"NewInterior","full",true,100,()=>99),"100 percent replaces rifle");
        Check(ShotgunDistribution.ChooseRifle(state,"NewInterior","quarter",true,25,()=>24)
            && !ShotgunDistribution.ChooseRifle(state,"NewInterior","quarter2",true,25,()=>25),"25 percent boundary");
        Check(!ShotgunDistribution.ChooseRifle(state,"NewInterior","zero",true,100,()=>throw new Exception("reroll")),"Retained decision cannot reroll");
        Check(!ShotgunDistribution.ChooseRifle(state,"NewInterior","disabled",false,100,()=>throw new Exception("disabled roll")),"Disabled replacements recorded retained");
        var restored=Ledger.Read(JsonSerializer.Serialize(state));
        Check(ShotgunDistribution.ChooseRifle(restored,"NewInterior","full",false,0,()=>throw new Exception("reload roll")),"Saved replacement decision survives reload/settings");

        var table=new LootTableData {name="LootTableLockerLocked"};
        table.m_BaseEntries.Add(new(){m_Item=new AssetReferenceGearItem("GEAR_Vanilla"),m_Weight=73});
        var container=new Container {m_LootTable=table,m_LootTableData=table};
        var scope=new ShotgunContainerLoot.Scope(container); var copy=scope.Copy(table);
        Check(copy.m_BaseEntries.Count==5 && table.m_BaseEntries.Count==1 && copy.m_BaseEntries[0].m_Weight==73,"Scoped parts preserve original entries and weights");
        Check(!ReferenceEquals(copy.m_BaseEntries[0],table.m_BaseEntries[0]) && !ReferenceEquals(copy.m_BaseEntries[0].m_Item,table.m_BaseEntries[0].m_Item),"Native asset-reference ownership is not shared");
        Check(copy.m_BaseEntries.Skip(1).All(e=>e.m_Weight==1) && ReferenceEquals(copy,scope.Copy(table)),"Weight one, alias reuse and no duplicate entries");
        container.m_LootTable=copy; scope.Restore();
        Check(ReferenceEquals(container.m_LootTable,table) && ReferenceEquals(container.m_LootTableData,table) && copy.Released,"Scope restores references and releases clone");

        object? BeginContainer(Container value)
        {
            object?[] arguments={value,null};
            typeof(ShotgunContainerLoot).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,arguments);
            return arguments[1];
        }
        Reset(); SaveGameSlots.Scenes!.Add("OldContainerScene"); Enter("OldContainerScene"); ExperienceModeManager.Level=ResourceLevel.Medium;
        Check(BeginContainer(container)==null,"Previously visited unopened container gets no parts");
        GameManager.m_ActiveScene="NewContainerScene";
        var newScope=(ShotgunContainerLoot.Scope?)BeginContainer(container);
        Check(newScope!=null && container.m_LootTable.m_BaseEntries.Count==5,"Native prefix injects parts in a new Medium scene");
        typeof(ShotgunContainerLoot).GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object?[]{newScope});
        Check(ReferenceEquals(container.m_LootTable,table),"Native finalizer restores container table");
        ShotgunAvailability.Allowed=false;Check(BeginContainer(container)==null,"Bans suppress container parts");ShotgunAvailability.Allowed=true;
        ExperienceModeManager.Level=ResourceLevel.Low;Check(BeginContainer(container)==null,"Low suppresses container parts");

        Reset(); var sourceObject=new GameObject{name="GEAR_Rifle"};var source=sourceObject.GetComponent<GearItem>();
        Check(ShotgunRifleReplacement.IsCandidate(source),"Loose ordinary rifle eligible");
        source.m_BeenInPlayerInventory=true;Check(!ShotgunRifleReplacement.IsCandidate(source),"Player dropped rifle excluded");source.m_BeenInPlayerInventory=false;
        source.m_InsideContainer=true;Check(!ShotgunRifleReplacement.IsCandidate(source),"Container rifle excluded");source.m_InsideContainer=false;
        sourceObject.name="GEAR_RifleVariant";Check(!ShotgunRifleReplacement.IsCandidate(source),"Named/modded rifle excluded");sourceObject.name="GEAR_Rifle";
        source.m_MissionObject=new();Check(!ShotgunRifleReplacement.IsCandidate(source),"Mission rifle excluded");source.m_MissionObject=null;
        source.GetComponent<GunItem>().m_Clip=new(){0,0,0};
        Check(ShotgunRifleReplacement.Replace(source) && sourceObject.Destroyed,"Successful replacement removes rifle");
        var replacement=GameObject.Spawned.Single(o=>o.name=="GEAR_DoubleBarrel");
        Check(replacement.GetComponent<GearItem>().Condition==source.Condition && replacement.GetComponent<GunItem>().m_Clip.Count==0,"Replacement inherits condition and is unloaded");
        Check(GameObject.Spawned.Count(o=>o.name=="GEAR_RifleAmmoSingle")==3,"Loaded rifle rounds preserved");
        GameObject.Spawned.Clear();sourceObject.Destroyed=false;GearItem.FailStartName="GEAR_RifleAmmoSingle";
        Check(!ShotgunRifleReplacement.Replace(source) && !sourceObject.Destroyed && GameObject.Spawned.All(o=>o.Destroyed),"Ammo initialization failure rolls back all new objects and retains original");
        GearItem.FailStartName=null;

        void AddFreshRifle()
        {
            GameManager.m_ActiveScene="FreshRifleScene";Settings.Instance.ReplaceStandardRifleFinds=true;Settings.Instance.RifleReplacementChance=100;
            GearManager.m_Gear.Add(new GameObject{name="GEAR_Rifle"}.GetComponent<GearItem>());
            Time.realtimeSinceStartup+=2;
        }
        Reset();AddFreshRifle();ShotgunRifleReplacement.Tick();
        Check(GameObject.Spawned.Any(g=>g.name=="GEAR_DoubleBarrel") && ShotgunSpawnPolicy.Save(),"Native update converts a fresh rifle and saves its decision");
        Reset(preserveData:true);AddFreshRifle();ShotgunRifleReplacement.Tick();
        Check(GameObject.Spawned.Count==0,"Reloaded scene cannot reroll or replace another rifle");
        Reset();SaveGameSlots.Scenes!.Add("FreshRifleScene");AddFreshRifle();ShotgunRifleReplacement.Tick();
        Check(GameObject.Spawned.Count==0,"Native saved scene protected even without distribution history");
        Reset();AddFreshRifle();ShotgunAvailability.Allowed=false;ShotgunRifleReplacement.Tick();
        Check(GameObject.Spawned.Count==0,"Native update obeys shotgun bans");
        Reset();AddFreshRifle();ExperienceModeManager.Level=ResourceLevel.Medium;ShotgunRifleReplacement.Tick();
        Check(GameObject.Spawned.Count==0,"Native update obeys usable minimum resources");
        Reset();AddFreshRifle();ExperienceModeManager.Challenge=true;ShotgunRifleReplacement.Tick();
        Check(GameObject.Spawned.Count==0,"Native update preserves challenge rifles");
        Reset();AddFreshRifle();GameManager.Story=true;ShotgunRifleReplacement.Tick();
        Check(GameObject.Spawned.Count==0,"Native update preserves story rifles");
        Reset();AddFreshRifle();Settings.Instance.ReplaceStandardRifleFinds=false;ShotgunRifleReplacement.Tick();
        Check(GameObject.Spawned.Count==0,"Off-by-default mode preserves rifle");
    }
}
