using HarmonyLib;
using MelonLoader;

namespace Shotgun;

internal static class EmbeddedAssetPackage
{
    private static ModComponentPackageBridge? bridge;

    internal static void Install(string modsDirectory)
    {
        if (bridge != null) return;
        var candidate = new ModComponentPackageBridge(typeof(ModComponent.Public).Assembly, modsDirectory,
            () => typeof(Main).Assembly.GetManifestResourceStream(ModComponentPackageBridge.ResourceName)
                ?? throw new InvalidDataException("Missing embedded shotgun asset package."),
            message => MelonLogger.Msg(message), message => MelonLogger.Warning(message));
        var harmony = new HarmonyLib.Harmony("Shotgun.EmbeddedPackage");
        try
        {
            harmony.Patch(candidate.StandaloneMethod, prefix: new HarmonyMethod(typeof(EmbeddedAssetPackage), nameof(LoadStandalone)));
            harmony.Patch(candidate.InitializeMethod, postfix: new HarmonyMethod(typeof(EmbeddedAssetPackage), nameof(LoadEmbedded)));
            harmony.Patch(candidate.DependencyReadMethod, postfix: new HarmonyMethod(typeof(EmbeddedAssetPackage), nameof(RegisterIdentity)));
            bridge = candidate;
        }
        catch
        {
            harmony.UnpatchSelf();
            throw;
        }
    }

    private static bool LoadStandalone(string __0) => bridge?.ShouldLoadStandalone(__0) ?? true;
    private static void LoadEmbedded() => bridge?.Load();
    private static void RegisterIdentity() => bridge?.RegisterDependencyIdentity();
    internal static void Update()
    {
        if (bridge?.Loaded == true) bridge.CompleteMigration(ModComponent.Public.IsReady());
    }
}
