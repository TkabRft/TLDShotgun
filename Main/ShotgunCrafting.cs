using System.Reflection;
using HarmonyLib;
using MelonLoader;
using ModComponent.API.Components;

namespace Shotgun
{
    internal static class ShotgunCrafting
    {
        private static readonly HashSet<string> OwnedJson = new();

        // ModComponent keeps its managed recipe registry between game loads. Remove only
        // our alternatives before its postfix maps recipes into the new game's manager.
        [HarmonyPatch(typeof(Il2CppTLD.Gear.BlueprintManager), nameof(Il2CppTLD.Gear.BlueprintManager.LoadAllUserBlueprints))]
        private static class RegisterAlternatives
        {
            [HarmonyPriority(Priority.First)]
            private static void Prefix()
            {
                try
                {
                    Type registry = typeof(ModGenericComponent).Assembly.GetType("CraftingRevisions.BlueprintManager", true)!;
                    var recipes = (HashSet<string>)AccessTools.Field(registry, "jsonUserBlueprints").GetValue(null);
                    foreach (string previous in OwnedJson) recipes.Remove(previous);
                    OwnedJson.Clear();
                    Settings settings = Settings.Instance;
                    if (settings == null) return;
                    var add = (Action<string>)AccessTools.Method(registry, "AddBlueprintFromJson")
                        .CreateDelegate(typeof(Action<string>));
                    if (settings.AllowVanillaBulletsAtForge) Add("VanillaBulletForge", add);
                    if (settings.AllowLeadShotAtForge)
                    {
                        Add("LeadBuckshotForge", add);
                        Add("LeadBirdshotForge", add);
                    }
                    if (settings.AllowLeadShellsAtWorkbench)
                        foreach (string load in new[] { "Slug", "Buckshot", "Birdshot" }) Add(load + "Workbench", add);
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Could not register shotgun crafting alternatives: {e}");
                }
            }
        }

        private static void Add(string name, Action<string> register)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Shotgun.Crafting.{name}.json")
                ?? throw new InvalidOperationException($"Missing alternative recipe {name}");
            using StreamReader reader = new(stream);
            string json = reader.ReadToEnd();
            register(json);
            OwnedJson.Add(json);
        }
    }
}
