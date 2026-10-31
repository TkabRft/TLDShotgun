"""Regenerate ammunition authoring definitions and packaging JSON; never build bundles.

Unity source components are synchronized by Editor/PrepareAmmunitionAssets.cs.
Run from any directory. Existing identifiers and localization translations survive.
"""
import copy
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "unity/Assets/_ModComponent/shotgun"
AUTHORING = Path(__file__).with_name("ammunition-definitions.json")


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def gear(name, count=1):
    return {"Item": name, "Count": count, "Units": "Count"}


def recipe(name, inputs, result, count, station, minutes, fire=False, tool="", powder=0):
    return {"Name": name, "RequiredGear": inputs,
            "RequiredPowder": [{"Powder": "POWDER_Gunpowder", "QuantityInKilograms": powder}] if powder else [],
            "RequiredLiquid": [], "RequiredTool": tool, "OptionalTools": [],
            "RequiredCraftingLocation": station, "RequiresLitFire": fire, "RequiresLight": True,
            "CraftedResult": result, "CraftedResultCount": count, "DurationMinutes": minutes,
            "CraftingAudio": "Play_HarvestingAmmo", "AppliedSkill": "Gunsmithing", "ImprovedSkill": "Gunsmithing"}


def main():
    materials = {"LeadBuckshot": ("Lead Buckshot", "A measured portion of cast lead buckshot, enough for one shotgun shell.", 6),
                 "LeadBirdshot": ("Lead Birdshot", "A measured portion of fine lead shot, enough for one birdshot shell.", 12),
                 "ScrapBuckshot": ("Scrap Buckshot", "A portion of prepared scrap-metal shot, enough for one scrapshot shell.", 6)}
    projectiles = {"Slug": "GEAR_Bullet", "Buckshot": "GEAR_DoubleBarrelLeadBuckshot",
                   "Birdshot": "GEAR_DoubleBarrelLeadBirdshot", "Scrapshot": "GEAR_DoubleBarrelScrapBuckshot"}
    base = read(DATA / "auto-mapped/DoubleBarrelAmmoSingleBuckshot.json")
    localization = read(DATA / "localizations/Localization.json")
    languages = next(iter(localization.values())).keys()

    def localize(key, english):
        localization.setdefault(key, {language: "" for language in languages})["English"] = english

    reference_guids = {}
    for kind in materials:
        name = "GEAR_DoubleBarrel" + kind
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "TLDShotgun/material-reference/"+name).hex
        reference_guids[kind] = guid
        asset = ROOT / "unity/Assets/Shotgun/References" / (name+".asset")
        asset.parent.mkdir(parents=True, exist_ok=True)
        asset.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
                         "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
                         "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
                         "  m_Script: {fileID: 11500000, guid: 3df780f930e2cf047af3a2ca3a2c1731, type: 3}\n"
                         f"  m_Name: {name}\n  m_EditorClassIdentifier: \n  Name: {name}\n  Icon: {{fileID: 0}}\n", encoding="utf-8")
        asset.with_suffix(".asset.meta").write_text(f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")

    definitions = []
    for kind, (label, description, count) in materials.items():
        name = "DoubleBarrel" + kind
        generic = copy.deepcopy(base["ModAmmoComponent"])
        generic.pop("AmmoForGunType")
        generic.update(DisplayNameLocalizationId=f"GAMEPLAY_{name}Name", DescriptionLocalizatonId=f"GAMEPLAY_{name}Description",
                       WeightKG=0.02, InventoryCategory="Material", DaysToDecay=0,
                       InspectDistance=0.2, InspectScale=[2.0, 2.0, 2.0])
        stack = copy.deepcopy(base["ModStackableBehaviour"])
        stack.update(UnitsPerItem=1, ShareStackWithGear=[], InstantiateStackItem="GEAR_"+name,
                     SingleUnitTextId="GAMEPLAY_DoubleBarrel_ShotPortionSingular",
                     MultipleUnitTextId="GAMEPLAY_DoubleBarrel_ShotPortionPlural")
        components = {"ModGenericComponent": generic, "ModStackableBehaviour": stack}
        bp = recipe(label, [gear("GEAR_ScrapMetal" if kind == "ScrapBuckshot" else "GEAR_ScrapLead")],
                    "GEAR_"+name, count, "Workbench" if kind == "ScrapBuckshot" else "AmmoWorkbench",
                    30 if kind == "ScrapBuckshot" else 60, kind != "ScrapBuckshot",
                    "GEAR_SimpleTools" if kind == "ScrapBuckshot" else "")
        write(DATA / "auto-mapped" / (name+".json"), components)
        write(DATA / "blueprints" / (name+".json"), bp)
        localize(f"GAMEPLAY_{name}Name", label)
        localize(f"GAMEPLAY_{name}Description", description)
        definitions.append({"name": "GEAR_"+name, "material": True, "components": components, "recipe": bp,
                            "model": f"Shotgun{kind}.fbx", "icon": "ico_GearItem__"+name+".png"})

    for kind, projectile in projectiles.items():
        for form in ("Single", "Box"):
            name = "DoubleBarrelAmmo"+form+kind
            source = DATA / "auto-mapped" / (name+".json")
            components = read(source) if source.exists() else read(DATA / "auto-mapped" / ("DoubleBarrelAmmo"+form+"Buckshot.json"))
            if kind == "Scrapshot":
                components = json.loads(json.dumps(components).replace("Buckshot", "Scrapshot"))
            components["ModAmmoComponent"]["WeightKG"] = 0.04
            components["ModHarvestableBehaviour"]["YieldNames"] = ["GEAR_DoubleBarrelAmmoCasing", projectile]
            components["ModHarvestableBehaviour"]["YieldCounts"] = [1, 1]
            write(source, components)
            bp = None
            if form == "Single":
                recipe_name = {"Slug": "Shotgun Slug", "Buckshot": "Shotgun Buckshot Round", "Birdshot": "Shotgun Birdshot Round"}.get(kind, "Shotgun Scrapshot Shell")
                bp = recipe(recipe_name, [gear("GEAR_DoubleBarrelAmmoCasing"), gear(projectile)],
                            "GEAR_"+name, 1, "Workbench" if kind == "Scrapshot" else "AmmoWorkbench", 5, powder=0.01)
                write(DATA / "blueprints" / ("ShotgunAmmoSingle"+kind+".json"), bp)
            if kind == "Scrapshot":
                localize(f"GAMEPLAY_DoubleBarrel_Ammo{form}ScrapshotName", "Scrapshot Shell" if form == "Single" else "Scrapshot Shells")
                localize(f"GAMEPLAY_DoubleBarrel_Ammo{form}ScrapshotDescription", "Improvised shotgun ammunition loaded with prepared scrap-metal shot.")
            definitions.append({"name": "GEAR_"+name, "material": False, "components": components, "recipe": bp,
                                "template": "GEAR_DoubleBarrelAmmo"+form+"Buckshot",
                                "model": "ShotgunScrapshot"+("Shell" if form == "Single" else "Box")+".fbx" if kind == "Scrapshot" else "",
                                "icon": "ico_GearItem__"+name+".png"})
            # Existing source prefabs remain authoritative for SDK re-export.
            prefab = ROOT / "unity/Assets/Shotgun/Prefabs" / ("GEAR_"+name+".prefab")
            if prefab.exists():
                # This checkout historically ignored .meta files. Recover this known
                # prefab GUID from its existing Addressables entry, never invent one.
                group = (ROOT / "unity/Assets/AddressableAssetsData/AssetGroups/shotgun.asset").read_text(encoding="utf-8")
                match = re.search(r"m_GUID: ([a-f0-9]{32})\s+m_Address: GEAR_"+re.escape(name)+r"\s", group)
                meta = Path(str(prefab)+".meta")
                if match and not meta.exists():
                    meta.write_text("fileFormatVersion: 2\nguid: "+match[1]+"\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
                text = prefab.read_text(encoding="utf-8")
                text = re.sub(r"(?m)^  WeightKG: 0\.02$", "  WeightKG: 0.04", text)
                if kind in ("Buckshot", "Birdshot"):
                    replacement = reference_guids["Lead"+kind]
                    # Preserve every existing component/file identifier. Replace only
                    # the old vanilla Bullet reference in this load's source prefab.
                    text = text.replace("guid: 8da687a0ff9e9804da5b6bcfb18152cd", "guid: "+replacement)
                prefab.write_text(text, encoding="utf-8")

    localize("GAMEPLAY_DoubleBarrel_ShotPortionSingular", "portion")
    localize("GAMEPLAY_DoubleBarrel_ShotPortionPlural", "portions")
    write(DATA / "localizations/Localization.json", localization)
    write(AUTHORING, {"Items": definitions})
    alternatives = {"VanillaBulletForge": recipe("Shotgun_Alternative_VanillaBulletForge", [gear("GEAR_ScrapLead")], "GEAR_Bullet", 6, "Forge", 60, True)}
    # Vanilla bullet casting improves Gunsmithing but does not apply an ammo-quality
    # skill or require ambient light; its furnace is still required to be lit.
    alternatives["VanillaBulletForge"].update(AppliedSkill="None", RequiresLight=False)
    for kind in ("LeadBuckshot", "LeadBirdshot"):
        bp = read(DATA / "blueprints" / ("DoubleBarrel"+kind+".json"))
        bp.update(Name="Shotgun_Alternative_"+kind+"Forge", RequiredCraftingLocation="Forge")
        alternatives[kind+"Forge"] = bp
    for kind in ("Slug", "Buckshot", "Birdshot"):
        bp = read(DATA / "blueprints" / ("ShotgunAmmoSingle"+kind+".json"))
        bp.update(Name="Shotgun_Alternative_"+kind+"Workbench", RequiredCraftingLocation="Workbench")
        alternatives[kind+"Workbench"] = bp
    for name, bp in alternatives.items():
        write(ROOT / "Crafting/Alternatives" / (name+".json"), bp)
    print("Prepared 3 materials, 4 shell recipes, 6 alternatives, and the Unity authoring manifest.")


if __name__ == "__main__":
    main()
