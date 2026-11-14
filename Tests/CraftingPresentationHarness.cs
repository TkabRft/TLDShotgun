global using System;
global using System.Collections.Generic;
global using System.Linq;
using System.IO.Compression;
using System.Text.Json;
using Il2Cpp;
using Il2CppTLD.Gear;
using Shotgun;
using UnityEngine;
using UnityEngine.AddressableAssets;

// Execute the production UI postfixes and original inventory-quality harness.
// Only native Unity/game/Harmony boundaries are replaced. Fixture IDs and English
// labels come from the actual package, not a second copy of the naming policy.
internal static class CraftingPresentationHarness
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }

    private static GearItem Gear(string id, int units = 1) => new GameObject(id).AddComponent<GearItem>().With(id, units);

    private static void Main(string[] args)
    {
        using (var archive = ZipFile.OpenRead(args[0]))
        {
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("auto-mapped/doublebarrelammosingle")))
            {
                using var json = JsonDocument.Parse(entry.Open());
                string key = json.RootElement.GetProperty("ModAmmoComponent").GetProperty("DisplayNameLocalizationId").GetString()!;
                string load = key["GAMEPLAY_DoubleBarrel_AmmoSingle".Length..^"Name".Length];
                ShotgunAmmoCatalog.Definitions.Add(new(load));
            }
        }
        int originalChecks = AmmoPresentationHarness.Run(args[0]);
        foreach (var load in ShotgunAmmoCatalog.Definitions)
        {
            string expected = load.Name+" Shell";
            var single = Gear("GEAR_DoubleBarrelAmmoSingle"+load.Name);
            var box = Gear("GEAR_DoubleBarrelAmmoBox"+load.Name, 6);
            var bp = new BlueprintData { m_CraftedResultGear = single };
            string result = "native";
            ShotgunCraftingPresentation.RecipeName.Postfix(bp, ref result);
            Check(result == expected, "Recipe name");
            bp.m_CraftedResultCount = 3;
            ShotgunCraftingPresentation.RecipeName.Postfix(bp, ref result);
            Check(result == expected+" (3)", "Recipe count");
            bp.m_CraftedResultCount = 1;
            var panel = new Panel_Crafting { SelectedBPI = bp };
            ShotgunCraftingPresentation.SelectedRecipe.Postfix(panel);
            Check(panel.m_SelectedName.text == expected, "Selected heading");
            Check(panel.m_SelectedImage.mainTexture?.name == "ico_CraftingPreview__DoubleBarrelAmmoSingle"+load.Name, "Selected color preview");
            var preview = panel.m_SelectedImage.mainTexture;
            ShotgunCraftingPresentation.SelectedRecipe.Postfix(panel);
            Check(ReferenceEquals(preview, panel.m_SelectedImage.mainTexture), "Texture cache");
            panel.m_SelectedName.text = "BIRDSHOT AMMUNITION";
            ShotgunCraftingPresentation.SelectedRecipe.Postfix(panel);
            Check(panel.m_SelectedName.text == expected.ToUpperInvariant(), "Native heading capitalization");

            foreach (string original in new[] { single.DisplayName, single.GetDisplayNameForInventoryInterfaces() })
            {
                result = "[color]"+original+"[/color]\nNative status";
                ShotgunCraftingPresentation.WorldHover.Postfix(single, ref result);
                Check(result == "[color]"+expected+"[/color]\nNative status", "World name/markup");
                ShotgunCraftingPresentation.WorldHoverProperty.Postfix(single, ref result);
                Check(result == "[color]"+expected+"[/color]\nNative status", "Property/method hover idempotence");
            }
            var manager = new PlayerManager { Inspected = single };
            var hud = new Panel_HUD();
            ShotgunCraftingPresentation.WorldInspection.Postfix(manager, hud);
            Check(hud.m_InspectMode_Title.text == expected, "World inspection");
            hud.m_InspectMode_Title.text = "NATIVE TITLE";
            ShotgunCraftingPresentation.WorldInspection.Postfix(manager, hud);
            Check(hud.m_InspectMode_Title.text == expected.ToUpperInvariant(), "Native inspection capitalization");
            result = single.DisplayName.ToUpperInvariant();
            ShotgunCraftingPresentation.WorldHover.Postfix(single, ref result);
            Check(result == expected.ToUpperInvariant(), "Uppercase hover");
            single.m_DisplayNameOverrideLocID = "custom name";
            Check(ShotgunCraftingPresentation.ShellName(single) == null, "Explicit name override ignored");
            single.m_DisplayNameOverrideLocID = "";
            single.m_InPlayerInventory = true;
            hud.m_InspectMode_Title.text = "native inventory title";
            ShotgunCraftingPresentation.WorldInspection.Postfix(manager, hud);
            Check(hud.m_InspectMode_Title.text == "native inventory title", "Inventory inspection was renamed");
            result = single.GetDisplayNameForInventoryInterfaces();
            string inventory = result;
            ShotgunCraftingPresentation.WorldHover.Postfix(single, ref result);
            Check(result == inventory && result == "Perfect "+load.Name+" Ammunition", "Inventory quality changed");
            single.m_InPlayerInventory = false;

            result = box.GetDisplayNameForInventoryInterfaces();
            inventory = result;
            ShotgunCraftingPresentation.WorldHover.Postfix(box, ref result);
            Check(result == inventory, "Full box renamed");
            box.m_StackableItem!.m_Units = 2;
            Check(!ShotgunCraftingPresentation.IsLooseWorldShell(box), "Partial box treated as loose");
            box.m_StackableItem.m_Units = 1;
            Check(ShotgunCraftingPresentation.IsLooseWorldShell(box), "One-round box representation missed");
            result = inventory;
            ShotgunCraftingPresentation.WorldHover.Postfix(box, ref result);
            Check(result == expected, "One dropped round label");
            box.m_StackableItem.m_Units = 0;
            Check(!ShotgunCraftingPresentation.IsLooseWorldShell(box), "Zero-round stack renamed");
            box.m_StackableItem = null;
            Check(!ShotgunCraftingPresentation.IsLooseWorldShell(box), "Missing stack renamed");

            var casing = Gear("GEAR_DoubleBarrelAmmoCasing");
            bp.m_RequiredGear = new[] { new BlueprintData.RequiredGearItem { m_Item = casing } };
            var requirement = new CraftingRequirementMaterial();
            ShotgunCraftingPresentation.RequirementIcon.Postfix(requirement, bp, 0);
            Check(requirement.m_Icon.mainTexture?.name == "ico_CraftItem__DoubleBarrelAmmoCasing", "Casing silhouette");
            foreach (string ingredient in new[] { "LeadBuckshot", "LeadBirdshot", "ScrapBuckshot" })
            {
                bp.m_RequiredGear[0].m_Item = Gear("GEAR_DoubleBarrel"+ingredient);
                ShotgunCraftingPresentation.RequirementIcon.Postfix(requirement, bp, 0);
                Check(requirement.m_Icon.mainTexture?.name == "ico_CraftItem__DoubleBarrel"+ingredient, "Projectile silhouette");
            }
            // Simulate the native UI resetting reused widgets before our postfixes.
            requirement.m_Icon.mainTexture = new Texture2D { name = "vanilla bullet" };
            bp.m_RequiredGear[0].m_Item = Gear("GEAR_Bullet");
            ShotgunCraftingPresentation.RequirementIcon.Postfix(requirement, bp, 0);
            Check(requirement.m_Icon.mainTexture.name == "vanilla bullet", "Vanilla ingredient changed");
            ShotgunCraftingPresentation.RequirementIcon.Postfix(requirement, bp, -1);
            ShotgunCraftingPresentation.RequirementIcon.Postfix(requirement, bp, 1);
            Check(requirement.m_Icon.mainTexture.name == "vanilla bullet", "Invalid ingredient index");

            panel.SelectedBPI = new() { m_CraftedResultGear = Gear("GEAR_RifleAmmoSingle") };
            panel.m_SelectedName.text = "Rifle Cartridge";
            panel.m_SelectedImage.mainTexture = new() { name = "vanilla rifle" };
            ShotgunCraftingPresentation.SelectedRecipe.Postfix(panel);
            Check(panel.m_SelectedName.text == "Rifle Cartridge" && panel.m_SelectedImage.mainTexture.name == "vanilla rifle", "Vanilla selection contaminated");
            panel.SelectedBPI = null;
            ShotgunCraftingPresentation.SelectedRecipe.Postfix(panel);
            manager.Inspected = null;
            ShotgunCraftingPresentation.WorldInspection.Postfix(manager, hud);

            string key = "GAMEPLAY_DoubleBarrel_"+load.Name+"ShellName";
            Localization.Entries.Remove(key);
            result = "native fallback";
            ShotgunCraftingPresentation.RecipeName.Postfix(bp, ref result);
            Check(result == "native fallback", "Missing localization replaced native text");
            Localization.Entries[key] = expected;
            Check(single.GearItemData.DisplayNameLocID == "GAMEPLAY_DoubleBarrel_AmmoSingle"+load.Name+"Name"
                && single.m_DisplayNameOverrideLocID == "", "Presentation mutated gear identity/localization");
        }
        foreach (string id in new[] { "GEAR_RifleAmmoSingle", "GEAR_RevolverAmmoBox", "GEAR_DoubleBarrelAmmoCasing", "GEAR_Unrelated" })
        {
            var gear = Gear(id);
            string result = "unchanged";
            ShotgunCraftingPresentation.WorldHover.Postfix(gear, ref result);
            ShotgunCraftingPresentation.RecipeName.Postfix(new() { m_CraftedResultGear = gear }, ref result);
            Check(result == "unchanged", "Unrelated name changed");
        }
        Check(ShotgunCraftingPresentation.ShellName(null) == null, "Null gear");
        Console.WriteLine($"PASS: {checks} crafting/world presentation checks and {originalChecks} original inventory quality checks using packaged localization.");
        Console.WriteLine("Native rendering, Addressables loading, and widget refresh order require game acceptance.");
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch : Attribute
    { public HarmonyPatch(Type type, string method) { } }
}
namespace UnityEngine
{
    public class Texture2D { public string name = ""; }
    public class GameObject
    {
        public string name;
        public GameObject(string name) { this.name = name; }
        public T AddComponent<T>() where T : GearItem, new() => new() { gameObject = this };
    }
}
namespace UnityEngine.AddressableAssets
{
    public static class Addressables
    {
        public static Handle<T> LoadAssetAsync<T>(string address) => new(address);
        public sealed class Handle<T>
        {
            private readonly string address;
            public Handle(string address) { this.address = address; }
            public T WaitForCompletion() => (T)(object)new Texture2D { name = address };
        }
    }
}
namespace MelonLoader { public static class MelonLogger { public static void Warning(string message) { } } }
namespace Shotgun.Utility { public static class Utility { public const int GunTypeCustom = 123; } }
namespace Shotgun
{
    // Fixture-only catalog boundary, populated from packaged item definitions.
    internal static class ShotgunAmmoCatalog
    {
        internal sealed record Definition(string Name);
        internal static List<Definition> Definitions = new();
        internal static Definition? Find(string id) => Definitions.FirstOrDefault(d =>
            id == "GEAR_DoubleBarrelAmmoSingle"+d.Name || id == "GEAR_DoubleBarrelAmmoBox"+d.Name);
    }
}
namespace Il2CppTLD.Gear
{
    public class BlueprintData
    {
        public GearItem? m_CraftedResultGear;
        public int m_CraftedResultCount = 1;
        public RequiredGearItem[] m_RequiredGear = Array.Empty<RequiredGearItem>();
        public class RequiredGearItem { public GearItem? m_Item; }
        public string GetDisplayedNameWithCount()
        { string result = "native"; ShotgunCraftingPresentation.RecipeName.Postfix(this, ref result); return result; }
    }
}
namespace Il2Cpp
{
    public class AmmoItem { public int m_AmmoForGunType; }
    public class StackableItem { public int m_Units; }
    public class GearData
    {
        public string name = "", DisplayNameLocID = "";
        public ConditionTableType ConditionType;
        public int NameCalls;
    }
    public class GearItem
    {
        public GameObject gameObject = new("");
        public string name => gameObject.name;
        public GearData GearItemData = new();
        public GearData m_GearItemData => GearItemData;
        public AmmoItem? m_AmmoItem;
        public StackableItem? m_StackableItem;
        public bool m_InPlayerInventory;
        public float m_CurrentHP = 100;
        public string m_DisplayNameOverrideLocID = "";
        public GearItem With(string id, int units)
        {
            GearItemData.name = id;
            string form = id.Contains("AmmoBox") ? "Box" : "Single";
            string load = ShotgunCraftingPresentation.LoadName(this) ?? "";
            GearItemData.DisplayNameLocID = "GAMEPLAY_DoubleBarrel_Ammo"+form+load+"Name";
            GearItemData.ConditionType = ConditionTableType.Ammunition;
            m_AmmoItem = new() { m_AmmoForGunType = Shotgun.Utility.Utility.GunTypeCustom };
            m_StackableItem = new() { m_Units = units };
            return this;
        }
        public float GetNormalizedCondition() => m_CurrentHP/100;
        public string DisplayName => Localization.Get(GearItemData.DisplayNameLocID);
        public string GetDisplayNameForInventoryInterfaces()
        { string result = "native"; ShotgunAmmoPresentation.InventoryName.Postfix(this, ref result); return result; }
        public string GetTitleToDisplayWhenSelected() => "native title";
        public string DisplayNameWithCondition => "native condition";
        public string GetHoverText() => DisplayName;
    }
    public static class Localization
    {
        public static Dictionary<string, string> Entries = new();
        public static bool Exists(string key) => Entries.ContainsKey(key);
        public static string Get(string key) => Entries.GetValueOrDefault(key, key);
    }
    public static class GameManager { public static ConditionTable GetConditionTable() => new(); }
    public class UILabel { public string text = "native"; }
    public class UITexture { public Texture2D? mainTexture; }
    public class Panel_Crafting
    {
        public BlueprintData? SelectedBPI;
        public UILabel m_SelectedName = new(); public UITexture m_SelectedImage = new();
        public void RefreshSelectedBlueprint(int index) { }
    }
    public class CraftingRequirementMaterial
    { public UITexture m_Icon = new(); public void Enable(Panel_Crafting panel, BlueprintData bp, int requiredIndex) { } }
    public class Panel_HUD { public UILabel m_InspectMode_Title = new(); }
    public class PlayerManager
    {
        public GearItem? Inspected;
        public GearItem? GearItemBeingInspected() => Inspected;
        public void UpdateInspectGear(Panel_HUD hud) { }
    }
}
