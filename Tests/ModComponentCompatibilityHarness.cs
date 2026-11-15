global using MelonLogger = CompatibilityLogger;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using Mono.Cecil;
using Shotgun;

// Extend the shared harness with the real crafting prefix, compatibility failures,
// and binding to the installed DLL. Unity-facing handlers remain shared stand-ins.
internal static class ModComponentCompatibilityHarness
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static ModComponentPackageBridge Bridge(Assembly assembly, string work, Func<Stream> resource) =>
        new(assembly, work, resource, _ => { }, _ => { });

    private static void CheckRejected(Assembly assembly, string work, string expected)
    {
        int hooks = Harmony.GetAllPatchedMethods().Count();
        try
        {
            Bridge(assembly, work, () => throw new Exception("Resource opened before API validation"));
            throw new Exception("Incompatible assembly accepted: " + assembly.Location);
        }
        catch (NotSupportedException error)
        {
            Check(error.Message.Contains(assembly.GetName().FullName), "Diagnostic omits detected assembly/version");
            Check(error.Message.Contains(expected), "Diagnostic omits incompatible member: " + expected);
        }
        Check(Harmony.GetAllPatchedMethods().Count() == hooks, "API rejection installed hooks");
    }

    private static void CheckIncompatibleAssemblies(string work)
    {
        Directory.CreateDirectory(work);
        var cases = new (string Name, Action<ModuleDefinition> Change, string Expected)[]
        {
            ("major6", m => m.Assembly.Name.Version = new Version(6, 99, 0, 0), "7.x"),
            ("major8", m => m.Assembly.Name.Version = new Version(8, 0, 0, 0), "7.x"),
            ("identity", m => m.Assembly.Name.Name = "OtherComponent", "7.x"),
            ("type", m => m.GetType("ModComponent.Mapper.ZipFileLoader").Name = "MissingLoader", "ModComponent.Mapper.ZipFileLoader"),
            ("method", m => m.GetType("ModComponent.Mapper.ZipFileLoader").Methods.Single(x => x.Name == "Initialize").Name = "MissingInitialize", "ZipFileLoader.Initialize"),
            ("result", m => m.GetType("ModComponent.Mapper.ZipFileLoader").Methods.Single(x => x.Name == "Initialize").ReturnType = m.TypeSystem.Int32, "ZipFileLoader.Initialize"),
            ("parameters", m => m.GetType("ModComponent.Mapper.ZipFileLoader").Methods.Single(x => x.Name == "LoadMCFile").Parameters[0].ParameterType = m.TypeSystem.Int32, "ZipFileLoader.LoadMCFile"),
            ("field", m => m.GetType("ModComponent.Mapper.ZipFileLoader").Fields.Single(x => x.Name == "hashes").FieldType = m.TypeSystem.String, "ZipFileLoader.hashes"),
            ("property", m => m.GetType("ModComponent.Utils.AssetBundleProcessor").Properties.Single(x => x.Name == "catalogTestList").Name = "MissingCatalogTests", "AssetBundleProcessor.catalogTestList"),
            ("enum", m => { var t = m.GetType("ModComponent.Mapper.FileType"); t.Fields.Remove(t.Fields.Single(x => x.Name == "other")); }, "GetFileType enum")
        };
        foreach (var test in cases)
        {
            string path = Path.Combine(work, test.Name + ".dll");
            using (var module = ModuleDefinition.ReadModule(typeof(ModComponentCompatibilityHarness).Assembly.Location))
            {
                module.Assembly.Name.Version = new Version(7, 42, 0, 0);
                // API-only negative controls must not duplicate the embedded asset pack.
                module.Resources.Clear();
                test.Change(module);
                module.Write(path);
            }
            CheckRejected(Assembly.LoadFile(path), work, test.Expected);
        }
    }

    private static void LoadRecipes() => new Il2CppTLD.Gear.BlueprintManager().LoadAllUserBlueprints();

    private static void Main(string[] args)
    {
        CheckIncompatibleAssemblies(Path.Combine(args[3], "incompatible"));
        var registry = CraftingRevisions.BlueprintManager.jsonUserBlueprints;
        const string other = "{\"Name\":\"OtherModRecipe\"}";
        registry.Add(other);
        Settings.Instance = new() { AllowVanillaBulletsAtForge = true, AllowLeadShotAtForge = true, AllowLeadShellsAtWorkbench = true };
        var harmony = new Harmony("Shotgun.Tests.CraftingAlternatives");
        harmony.CreateClassProcessor(typeof(ShotgunCrafting).GetNestedType("RegisterAlternatives", BindingFlags.NonPublic)!).Patch();
        Check(!EmbeddedAssetPackage.Loaded, "Package unexpectedly ready before installation");
        LoadRecipes(); LoadRecipes();
        Check(registry.SetEquals(new[] { other }), "Alternatives registered without embedded assets");
        Check(CompatibilityLogger.Warnings.Count == 1, "Unavailable-assets warning was repeated");
        Check(Il2CppTLD.Gear.BlueprintManager.Loads == 2, "Crafting prefix suppressed native loading");

        // Runs real JsonHandler lookup, package failure/migration/hash tests and
        // production Harmony installation, using this executable's 7.x identity.
        typeof(RuntimeHarness).GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { args.Take(5).ToArray() });
        Check(EmbeddedAssetPackage.Loaded, "Successful lifecycle did not expose loaded status");
        var installed = Assembly.LoadFile(Path.GetFullPath(args[5]));
        Bridge(installed, args[3], () => throw new Exception("Binding executed game code"));
        Check(installed.GetName().Version!.Major == 7, "Installed binding did not use ModComponent 7.x");

        var names = new[] { "VanillaBulletForge", "LeadBuckshotForge", "LeadBirdshotForge", "SlugWorkbench", "BuckshotWorkbench", "BirdshotWorkbench" };
        foreach (int options in Enumerable.Range(0, 8))
        {
            Settings.Instance = new() { AllowVanillaBulletsAtForge = (options & 1) != 0,
                AllowLeadShotAtForge = (options & 2) != 0, AllowLeadShellsAtWorkbench = (options & 4) != 0 };
            var expected = names.Where((_, i) => (options & (i == 0 ? 1 : i < 3 ? 2 : 4)) != 0)
                .Select(n => "Shotgun_Alternative_" + n).Append("OtherModRecipe").ToHashSet();
            LoadRecipes();
            var snapshot = registry.ToHashSet();
            Check(registry.Select(j => JsonDocument.Parse(j).RootElement.GetProperty("Name").GetString()!).ToHashSet().SetEquals(expected), "Wrong recipes for option combination " + options);
            LoadRecipes();
            Check(registry.SetEquals(snapshot), "Repeated load changed recipes for option combination " + options);
        }
        Settings.Instance = null;
        LoadRecipes();
        Check(registry.SetEquals(new[] { other }), "Missing settings retained owned alternatives");

        Settings.Instance = new() { AllowLeadShotAtForge = true, AllowLeadShellsAtWorkbench = true };
        LoadRecipes();
        Check(registry.Count == 6, "Failure scenario did not start with five enabled alternatives");
        var bridgeField = typeof(EmbeddedAssetPackage).GetField("bridge", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? successful = bridgeField.GetValue(null);
        var failed = Bridge(typeof(ModComponentCompatibilityHarness).Assembly, args[3], () => new MemoryStream(new byte[] { 0 }));
        try { failed.Load(); throw new Exception("Invalid package accepted"); }
        catch (InvalidDataException) { }
        try
        {
            bridgeField.SetValue(null, failed);
            Check(!EmbeddedAssetPackage.Loaded, "Failed registration exposed loaded status");
            LoadRecipes(); LoadRecipes();
            Check(registry.SetEquals(new[] { other }), "Failed registration retained/re-added alternatives");
            Check(CompatibilityLogger.Warnings.Count == 1, "Failure warning repeated within the process");
        }
        finally { bridgeField.SetValue(null, successful); harmony.UnpatchSelf(); }
        Check(CompatibilityLogger.Errors.Count == 0, "Crafting prefix logged unexpected errors");
        Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS", checks,
            fixtureVersion = typeof(ModComponentCompatibilityHarness).Assembly.GetName().Version!.ToString(),
            installedVersion = installed.GetName().Version!.ToString(), optionCombinations = 8,
            incompatibleFixtures = 10, productionCraftingPrefix = true, nativeGameExecuted = false }));
    }
}

internal static class CompatibilityLogger
{
    internal static readonly List<string> Warnings = new();
    internal static readonly List<string> Errors = new();
    internal static void Msg(string text) { }
    internal static void Warning(string text) { if (text.StartsWith("Shotgun crafting alternatives")) Warnings.Add(text); }
    internal static void Error(string text) => Errors.Add(text);
}

namespace ModComponent.API.Components { public sealed class ModGenericComponent { } }
namespace Shotgun
{
    internal sealed class Settings
    {
        internal static Settings? Instance;
        internal bool AllowVanillaBulletsAtForge, AllowLeadShotAtForge, AllowLeadShellsAtWorkbench;
    }
}
namespace CraftingRevisions
{
    internal static class BlueprintManager
    {
        internal static readonly HashSet<string> jsonUserBlueprints = new();
        private static void AddBlueprintFromJson(string json) => jsonUserBlueprints.Add(json);
    }
}
namespace Il2CppTLD.Gear
{
    internal sealed class BlueprintManager
    {
        internal static int Loads;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void LoadAllUserBlueprints() => Loads++;
    }
}
