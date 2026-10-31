using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Shotgun;

// Pure managed bridge. All game/resource handling remains in ModComponent 7.0.
internal sealed class ModComponentPackageBridge
{
    internal const string PackageName = "DoubleBarrel";
    internal const string ResourceName = "Shotgun.DoubleBarrel.modcomponent";
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly Func<Stream> openResource;
    private readonly Action<string> info;
    private readonly Action<string> warning;
    private readonly string modsDirectory;
    private readonly MethodInfo getFileType;
    private readonly MethodInfo handleFile;
    private readonly MethodInfo addDependency;
    private readonly FieldInfo hashes;
    private readonly FieldInfo dependencyNames;
    private readonly PropertyInfo catalogTests;
    private readonly PropertyInfo tempDirectory;
    private bool attempted;
    private bool migrationChecked;

    internal MethodInfo InitializeMethod { get; }
    internal MethodInfo StandaloneMethod { get; }
    internal MethodInfo DependencyReadMethod { get; }
    internal string LegacyArchivePath { get; }
    internal bool Loaded { get; private set; }
    internal string ContentHash { get; private set; } = "";

    internal ModComponentPackageBridge(Assembly componentAssembly, string modsPath,
        Func<Stream> resource, Action<string> information, Action<string> warnings)
    {
        Version? version = componentAssembly.GetName().Version;
        if (componentAssembly.GetName().Name != "ModComponent" || version?.Major != 7 || version.Minor != 0)
            throw new NotSupportedException("Embedded shotgun assets require the ModComponent 7.0 loader API.");
        modsDirectory = Path.GetFullPath(modsPath);
        LegacyArchivePath = Path.Combine(modsDirectory, PackageName + ".modcomponent");
        openResource = resource;
        info = information;
        warning = warnings;
        Type loader = RequireType(componentAssembly, "ModComponent.Mapper.ZipFileLoader");
        Type dependencies = RequireType(componentAssembly, "ModComponent.Utils.DependencyChecker");
        Type processor = RequireType(componentAssembly, "ModComponent.Utils.AssetBundleProcessor");
        Type api = RequireType(componentAssembly, "ModComponent.Public");
        InitializeMethod = RequireMethod(loader, "Initialize", typeof(void));
        StandaloneMethod = RequireMethod(loader, "LoadMCFile", typeof(void), typeof(string));
        DependencyReadMethod = RequireMethod(dependencies, "ReadFiles", typeof(void));
        getFileType = loader.GetMethod("GetFileType", StaticMembers, null, new[] { typeof(string) }, null)
            ?? throw Missing("GetFileType(string)");
        if (!getFileType.ReturnType.IsEnum || !Enum.GetNames(getFileType.ReturnType).Contains("other")) throw Missing("GetFileType enum");
        handleFile = RequireMethod(loader, "TryHandleFile", typeof(bool), typeof(string), typeof(string), getFileType.ReturnType, typeof(MemoryStream));
        hashes = RequireField(loader, "hashes", typeof(List<byte[]>));
        dependencyNames = RequireField(dependencies, "fileNames", typeof(List<string>));
        catalogTests = RequireProperty(processor, "catalogTestList", typeof(Dictionary<string, string>));
        tempDirectory = RequireProperty(processor, "tempFolderPath", typeof(string));
        addDependency = RequireMethod(api, "AddDependencyEntry", typeof(void), typeof(string), typeof(string[]), typeof(bool));
    }

    internal bool ShouldLoadStandalone(string path) => !string.Equals(Path.GetFullPath(path), LegacyArchivePath, StringComparison.OrdinalIgnoreCase);

    internal void Load()
    {
        if (Loaded) return;
        if (attempted) throw new InvalidOperationException("Embedded shotgun package registration previously failed; refusing a partial retry.");
        attempted = true;
        using Stream source = openResource();
        if (!source.CanSeek) throw new InvalidDataException("Embedded package resource must be seekable.");
        using (var hash = SHA256.Create()) ContentHash = Convert.ToHexString(hash.ComputeHash(source)).ToLowerInvariant();
        source.Position = 0;
        using var archive = new ZipArchive(source, ZipArchiveMode.Read);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            string path = entry.FullName;
            if (path.Contains('\\') || Path.IsPathRooted(path) || path.Split('/').Any(p => p is ".." or ".") || !paths.Add(path))
                throw new InvalidDataException("Unsafe or duplicate embedded package entry: " + path);
            if (IsDataEntry(path) && path != path.ToLowerInvariant()) throw new InvalidDataException("Noncanonical ModComponent data filename: " + path);
        }
        ZipArchiveEntry buildInfo = archive.GetEntry("buildinfo.json") ?? throw new InvalidDataException("Embedded package has no buildinfo.json.");
        using Stream metadata = buildInfo.Open();
        using JsonDocument build = JsonDocument.Parse(metadata);
        string[] requires = build.RootElement.TryGetProperty("Requires", out JsonElement required)
            ? required.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : Array.Empty<string>();
        bool requiresDlc = build.RootElement.TryGetProperty("RequiresDLC", out JsonElement dlc) && dlc.GetBoolean();
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            object kind = Invoke(getFileType, entry.Name)!;
            if (kind.ToString() == "other") continue;
            using var data = new MemoryStream();
            using (Stream input = entry.Open()) input.CopyTo(data);
            data.Position = 0;
            if (Invoke(handleFile, LegacyArchivePath, entry.FullName, kind, data) is not true)
                throw new InvalidDataException("ModComponent rejected embedded shotgun entry: " + entry.FullName);
            if (entry.FullName == "bundle/catalog_shotgun.json") SelectCatalogProbe(entry.Name, data);
        }
        Invoke(addDependency, PackageName, requires, requiresDlc);
        ((List<byte[]>)hashes.GetValue(null)!).Add(Convert.FromHexString(ContentHash));
        Loaded = true;
        info("Loaded embedded shotgun package " + ContentHash + " through ModComponent (no standalone archive).");
    }

    internal void RegisterDependencyIdentity()
    {
        if (!Loaded) return;
        var names = (List<string>)dependencyNames.GetValue(null)!;
        string name = PackageName.ToLowerInvariant();
        if (!names.Contains(name)) names.Add(name);
    }

    internal void CompleteMigration(bool componentReady)
    {
        if (!Loaded || !componentReady || migrationChecked) return;
        migrationChecked = true;
        if (!File.Exists(LegacyArchivePath)) return;
        try
        {
            if ((File.GetAttributes(LegacyArchivePath) & FileAttributes.ReparsePoint) != 0)
            {
                warning("Preserved linked standalone shotgun package: " + LegacyArchivePath);
                return;
            }
            string digest;
            using (Stream source = File.OpenRead(LegacyArchivePath))
            using (var hash = SHA256.Create()) digest = Convert.ToHexString(hash.ComputeHash(source)).ToLowerInvariant();
            // Immutable template and the previously shipped, case-broken package.
            if (digest != ContentHash
                && digest != "1000a4d12fcda90ad2af4fade3d2e4c934231205536a0f2ce3c67a4a53c378a3"
                && digest != "597e241e6eb8caa8138a1ceb314f411d1e90c48d587b658dfc4a2535d8a07178")
            {
                warning("Standalone shotgun package was skipped but preserved because its hash is unknown: " + LegacyArchivePath);
                return;
            }
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(LegacyArchivePath)), modsDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Legacy package escaped the Mods directory.");
            File.Delete(LegacyArchivePath); // Exact, hash-verified file only; never a directory.
            info("Removed redundant legacy shotgun archive after successful ModComponent registration.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            warning("Could not remove redundant shotgun archive: " + error.Message);
        }
    }

    private void SelectCatalogProbe(string filename, MemoryStream data)
    {
        data.Position = 0;
        using JsonDocument catalog = JsonDocument.Parse(data);
        string? probe = catalog.RootElement.GetProperty("m_InternalIds").EnumerateArray()
            .Select(e => e.GetString()).FirstOrDefault(path => path != null && path.Replace('\\', '/').EndsWith("/GEAR_DoubleBarrel.prefab", StringComparison.OrdinalIgnoreCase));
        if (probe == null) throw new InvalidDataException("Shotgun catalog is missing the gear prefab self-test target.");
        string path = Path.Combine((string)tempDirectory.GetValue(null)!, PackageName, filename);
        var tests = (Dictionary<string, string>)catalogTests.GetValue(null)!;
        if (!tests.ContainsKey(path)) throw new InvalidDataException("ModComponent did not register the shotgun catalog test.");
        tests[path] = probe;
    }

    private static bool IsDataEntry(string path) => path.Split('/')[0].ToLowerInvariant() is "auto-mapped" or "blueprints" or "localizations" or "recipes" or "gear-spawns";
    private static object? Invoke(MethodInfo method, params object[] arguments)
    {
        try { return method.Invoke(null, arguments); }
        catch (TargetInvocationException error) { throw new InvalidOperationException("ModComponent." + method.Name + " failed.", error.InnerException ?? error); }
    }
    private static NotSupportedException Missing(string member) => new("Incompatible ModComponent 7.0 loader member: " + member);
    private static Type RequireType(Assembly assembly, string name) => assembly.GetType(name, false) ?? throw Missing(name);
    private static MethodInfo RequireMethod(Type type, string name, Type result, params Type[] parameters)
    {
        MethodInfo? method = type.GetMethod(name, StaticMembers, null, parameters, null);
        return method != null && method.ReturnType == result ? method : throw Missing(type.FullName + "." + name);
    }
    private static FieldInfo RequireField(Type type, string name, Type fieldType)
    {
        FieldInfo? field = type.GetField(name, StaticMembers);
        return field != null && field.FieldType == fieldType ? field : throw Missing(type.FullName + "." + name);
    }
    private static PropertyInfo RequireProperty(Type type, string name, Type propertyType)
    {
        PropertyInfo? property = type.GetProperty(name, StaticMembers);
        return property != null && property.PropertyType == propertyType && property.GetMethod != null ? property : throw Missing(type.FullName + "." + name);
    }
}
