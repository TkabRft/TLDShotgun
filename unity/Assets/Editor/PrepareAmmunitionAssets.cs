#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ModComponent.Components;
using ModComponent.Behaviours;
using ModComponent.Blueprints;
using ModComponent.SDK.Components;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

// Authoring only: creates/synchronizes source prefabs and SDK references. Does not
// build Addressables, export a .modcomponent, compile the runtime mod, or deploy it.
public static class PrepareAmmunitionAssets
{
    const string Root = "Assets/Shotgun/";
    [Serializable] class Manifest { public Item[] Items; }
    [Serializable] class Item { public string name, model, icon, template; public bool material; public Components components; public Recipe recipe; }
    [Serializable] class Components { public Generic ModGenericComponent, ModAmmoComponent; public Stack ModStackableBehaviour; public Harvest ModHarvestableBehaviour; }
    [Serializable] class Generic { public string DisplayNameLocalizationId, DescriptionLocalizatonId, InventoryCategory, InitialCondition; public float WeightKG, MaxHP, InspectDistance; public int DaysToDecay; public bool InspectOnPickup; public float[] InspectAngles, InspectOffset, InspectScale; }
    [Serializable] class Stack { public string SingleUnitTextId, MultipleUnitTextId, InstantiateStackItem; public string[] ShareStackWithGear; public int UnitsPerItem; public float ChanceFull, StackConditionDifferenceConstraint; }
    [Serializable] class Harvest { public int Minutes; public string[] YieldNames; public int[] YieldCounts; }
    [Serializable] class Ingredient { public string Item; public int Count; }
    [Serializable] class Recipe { public string Name, RequiredTool, CraftedResult, RequiredCraftingLocation, AppliedSkill, ImprovedSkill; public Ingredient[] RequiredGear; public bool RequiresLitFire, RequiresLight; public int CraftedResultCount, DurationMinutes; }

    [MenuItem("Tools/Shotgun/Prepare Ammunition Source Assets")]
    public static void Prepare()
    {
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("SourceArt/ammunition-definitions.json"));
        Directory.CreateDirectory(Root + "References");
        AssetDatabase.Refresh();
        foreach (var item in manifest.Items) Gear(item.name);
        foreach (var item in manifest.Items) PrepareItem(item);
        string hud = Root + "Icons/ico_DoubleBarrelScrapshotHUD.png";
        var hudImporter = AssetImporter.GetAtPath(hud) as TextureImporter;
        if (hudImporter == null) throw new InvalidOperationException("Missing scrapshot HUD icon");
        hudImporter.textureType = TextureImporterType.Sprite;
        hudImporter.spriteImportMode = SpriteImportMode.Single;
        hudImporter.alphaIsTransparency = true;
        hudImporter.SaveAndReimport();
        Register(AssetDatabase.LoadAssetAtPath<Texture2D>(hud), "ico_DoubleBarrelScrapshotHUD", "Icons");
        AssetDatabase.SaveAssets();
        Debug.Log("Shotgun ammunition source assets prepared. Review before exporting the SDK package; no bundle was built.");
    }

    static DataGearAsset Gear(string name)
    {
        var local = AssetDatabase.LoadAssetAtPath<DataGearAsset>(Root + "References/" + name + ".asset");
        if (local != null) return local;
        foreach (var guid in AssetDatabase.FindAssets(name + " t:DataGearAsset"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<DataGearAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.Name == name) return asset;
        }
        var created = ScriptableObject.CreateInstance<DataGearAsset>();
        created.Name = name;
        AssetDatabase.CreateAsset(created, Root + "References/" + name + ".asset");
        return created;
    }

    static void EnumField(object target, string field, string value)
    {
        var info = target.GetType().GetField(field);
        info.SetValue(target, Enum.Parse(info.FieldType, value));
    }

    static void PrepareItem(Item item)
    {
        string path = Root + "Prefabs/" + item.name + ".prefab";
        bool exists = File.Exists(path);
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + (item.template ?? "GEAR_DoubleBarrelAmmoSingleBuckshot") + ".prefab");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/GEAR_DoubleBarrelAmmoSingleBuckshot.prefab");
        GameObject go = exists ? PrefabUtility.LoadPrefabContents(path) : item.material ? new GameObject(item.name) : UnityEngine.Object.Instantiate(template);
        try
        {
            go.name = item.name;
            var spec = item.components.ModGenericComponent ?? item.components.ModAmmoComponent;
            var generic = go.GetComponent<ModGenericComponent>();
            if (generic == null) generic = item.material ? go.AddComponent<ModGenericComponent>() : go.AddComponent<ModAmmoComponent>();
            generic.DisplayNameLocalizationId = spec.DisplayNameLocalizationId;
            generic.DescriptionLocalizatonId = spec.DescriptionLocalizatonId;
            generic.WeightKG = spec.WeightKG;
            generic.DaysToDecay = spec.DaysToDecay;
            generic.MaxHP = spec.MaxHP;
            generic.InspectOnPickup = spec.InspectOnPickup;
            generic.InspectDistance = spec.InspectDistance;
            generic.InspectScale = new Vector3(spec.InspectScale[0], spec.InspectScale[1], spec.InspectScale[2]);
            generic.InspectAngles = new Vector3(spec.InspectAngles[0], spec.InspectAngles[1], spec.InspectAngles[2]);
            generic.InspectOffset = new Vector3(spec.InspectOffset[0], spec.InspectOffset[1], spec.InspectOffset[2]);
            EnumField(generic, "InitialCondition", spec.InitialCondition);
            EnumField(generic, "InventoryCategory", spec.InventoryCategory);
            var audio = source.GetComponent<ModGenericComponent>();
            generic.PickUpAudio = audio.PickUpAudio;
            generic.PutBackAudio = audio.PutBackAudio;
            generic.StowAudio = audio.StowAudio;

            var stack = go.GetComponent<ModStackableBehaviour>() ?? go.AddComponent<ModStackableBehaviour>();
            var ss = item.components.ModStackableBehaviour;
            stack.SingleUnitTextId = ss.SingleUnitTextId;
            stack.MultipleUnitTextId = ss.MultipleUnitTextId;
            stack.UnitsPerItem = ss.UnitsPerItem;
            stack.ChanceFull = ss.ChanceFull;
            stack.StackConditionDifferenceConstraint = ss.StackConditionDifferenceConstraint;
            stack.InstantiateStackItem = string.IsNullOrEmpty(ss.InstantiateStackItem) ? null : Gear(ss.InstantiateStackItem);
            stack.ShareStackWithGear = ss.ShareStackWithGear.Select(Gear).ToArray();

            var hs = item.components.ModHarvestableBehaviour;
            if (hs != null)
            {
                var h = go.GetComponent<ModHarvestableBehaviour>() ?? go.AddComponent<ModHarvestableBehaviour>();
                var serialized = new SerializedObject(h);
                serialized.FindProperty("Minutes").intValue = hs.Minutes;
                var yields = serialized.FindProperty("Yields");
                yields.arraySize = hs.YieldNames.Length;
                for (int i = 0; i < hs.YieldNames.Length; i++)
                {
                    var entry = yields.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("YieldCounts").intValue = hs.YieldCounts[i];
                    entry.FindPropertyRelative("YieldNames").objectReferenceValue = Gear(hs.YieldNames[i]);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (item.recipe != null)
            {
                var bp = go.GetComponent<ModBlueprint>() ?? go.AddComponent<ModBlueprint>();
                var r = item.recipe;
                bp.Name = r.Name;
                bp.RequiredGear = r.RequiredGear.Select(x => new GearRequirement { GearItem = Gear(x.Item), Count = x.Count }).ToArray();
                bp.RequiredTool = string.IsNullOrEmpty(r.RequiredTool) ? null : Gear(r.RequiredTool);
                bp.OptionalTools = Array.Empty<DataGearAsset>();
                if (item.material) bp.RequiredPowder = Array.Empty<PowderRequirement>();
                else
                {
                    string powderPath = Root + "References/POWDER_Gunpowder.asset";
                    var powder = AssetDatabase.LoadAssetAtPath<DataPowderAsset>(powderPath);
                    if (powder == null)
                    {
                        powder = ScriptableObject.CreateInstance<DataPowderAsset>();
                        powder.GetType().GetField("Name").SetValue(powder, "POWDER_Gunpowder");
                        AssetDatabase.CreateAsset(powder, powderPath);
                    }
                    bp.RequiredPowder = new[] { new PowderRequirement { PowderItem = powder, QuantityInKilograms = 0.01f } };
                }
                bp.RequiredLiquid = Array.Empty<LiquidRequirement>();
                bp.CraftedResult = Gear(r.CraftedResult);
                bp.CraftedResultCount = r.CraftedResultCount;
                bp.DurationMinutes = r.DurationMinutes;
                bp.RequiresLight = r.RequiresLight;
                bp.RequiresLitFire = r.RequiresLitFire;
                bp.CraftingAudio = source.GetComponent<ModBlueprint>().CraftingAudio;
                EnumField(bp, "RequiredCraftingLocation", r.RequiredCraftingLocation);
                EnumField(bp, "AppliedSkill", r.AppliedSkill);
                EnumField(bp, "ImprovedSkill", r.ImprovedSkill);
            }
            if (!string.IsNullOrEmpty(item.model))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Imports/" + item.model);
                if (model == null) throw new InvalidOperationException("Missing model: " + item.model);
                foreach (Transform child in go.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var visual = UnityEngine.Object.Instantiate(model, go.transform);
                visual.transform.localPosition = Vector3.zero;
                AssignAmmunitionMaterials(visual);
                var collider = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
                var renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    collider.center = go.transform.InverseTransformPoint(bounds.center);
                    collider.size = bounds.size;
                }
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Register(prefab, item.name, "Items");
            string iconPath = Root + "Icons/" + item.icon;
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                Register(texture, Path.GetFileNameWithoutExtension(item.icon), "Icons");
                Gear(item.name).Icon = texture;
                EditorUtility.SetDirty(Gear(item.name));
            }
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void AssignAmmunitionMaterials(GameObject visual)
    {
        // Use explicit Unity materials instead of relying on FBX shader conversion.
        // In particular, the carton has paper and shell slots plus a separate lid flap.
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string name = materials[i] == null ? "" : materials[i].name;
                if (name != "ShotgunCastLead" && name != "ShotgunCutSteel" &&
                    name != "ShotgunScrapshotShell" && name != "ShotgunScrapshotBox")
                    throw new InvalidOperationException("Unexpected ammunition material: " + name);
                var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + ".mat");
                if (material == null || material.mainTexture == null)
                    throw new InvalidOperationException("Missing ammunition material/texture: " + name);
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
        }
    }

    static void Register(UnityEngine.Object asset, string address, string collection)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var group = settings.FindGroup("shotgun");
        if (group == null) throw new InvalidOperationException("Missing shotgun Addressables group");
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        settings.CreateOrMoveEntry(guid, group).address = address;
        var pack = AssetDatabase.LoadMainAssetAtPath("Assets/_ModComponent/shotgun/shotgun.asset");
        var serialized = new SerializedObject(pack);
        var list = serialized.FindProperty(collection);
        bool found = Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == asset);
        if (!found) { int i = list.arraySize++; list.GetArrayElementAtIndex(i).objectReferenceValue = asset; }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
