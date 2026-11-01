using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Shotgun;

internal static class ShotgunMaintenance
{
    internal const string ScrapId = "GEAR_ScrapMetal";
    internal const string FirId = "GEAR_Hardwood";

    internal sealed class Inputs
    {
        internal Harvest Harvest { get; }
        internal Cleanable Cleanable { get; }
        internal Millable Millable { get; }
        internal GearItem Scrap { get; }
        internal GearItem Fir { get; }

        internal Inputs(Harvest harvest, Cleanable cleanable, Millable millable, GearItem scrap, GearItem fir)
        {
            Harvest = harvest;
            Cleanable = cleanable;
            Millable = millable;
            Scrap = scrap;
            Fir = fir;
        }
    }

    internal static Inputs Resolve(GameObject rifle)
    {
        Harvest harvest = RequireComponent<Harvest>(rifle, "GEAR_Rifle");
        Cleanable cleanable = RequireComponent<Cleanable>(rifle, "GEAR_Rifle");
        Millable millable = RequireComponent<Millable>(rifle, "GEAR_Rifle");
        ValidateReferences(harvest.m_RequiredTools, "GEAR_Rifle.Harvest.m_RequiredTools");
        ValidatePairs(harvest.m_YieldPowder, harvest.m_YieldPowderAmount?.Length ?? 0, "GEAR_Rifle.Harvest powder yields");
        ValidatePairs(cleanable.m_RequiredGear, cleanable.m_RequiredGearUnits?.Length ?? 0, "GEAR_Rifle.Cleanable materials");
        ValidateReferences(cleanable.m_CleanToolChoices, "GEAR_Rifle.Cleanable.m_CleanToolChoices");
        if (cleanable.m_RequiresToolToClean && (cleanable.m_CleanToolChoices?.Length ?? 0) == 0)
            throw new InvalidOperationException("GEAR_Rifle.Cleanable requires a tool but has no cleaning tool choices");
        ValidatePairs(millable.m_RepairRequiredGear, millable.m_RepairRequiredGearUnits?.Length ?? 0, "GEAR_Rifle.Millable repair materials");
        return new Inputs(harvest, cleanable, millable, LoadGear(ScrapId), LoadGear(FirId));
    }

    private static GearItem LoadGear(string address)
    {
        GameObject prefab = Addressables.LoadAssetAsync<GameObject>(address).WaitForCompletion();
        return RequireComponent<GearItem>(prefab, address);
    }

    private static T RequireComponent<T>(GameObject prefab, string address) where T : Component
    {
        if (prefab == null) throw new InvalidOperationException($"Missing required GameObject: {address}");
        T component = prefab.GetComponent<T>();
        if (component == null) throw new InvalidOperationException($"Missing required {typeof(T).Name}: {address}");
        return component;
    }

    private static void ValidateReferences<T>(Il2CppReferenceArray<T>? values, string label) where T : UnityEngine.Object
    {
        if (values != null && values.Any(value => value == null))
            throw new InvalidOperationException($"Null item reference in {label}");
    }

    private static void ValidatePairs<T>(Il2CppReferenceArray<T>? items, int amountCount, string label) where T : UnityEngine.Object
    {
        ValidateReferences(items, label);
        if ((items?.Length ?? 0) != amountCount)
            throw new InvalidOperationException($"Mismatched item/amount arrays in {label}");
    }

    private static Il2CppReferenceArray<T> Copy<T>(Il2CppReferenceArray<T>? values) where T : UnityEngine.Object =>
        new(values?.ToArray() ?? Array.Empty<T>());

    private static Il2CppStructArray<int> Copy(Il2CppStructArray<int>? values) =>
        new(values?.ToArray() ?? Array.Empty<int>());

    private static Il2CppStructArray<Il2CppTLD.IntBackedUnit.ItemWeight> Copy(Il2CppStructArray<Il2CppTLD.IntBackedUnit.ItemWeight>? values) =>
        new(values?.ToArray() ?? Array.Empty<Il2CppTLD.IntBackedUnit.ItemWeight>());

    private static T GetOrAdd<T>(GameObject prefab) where T : Component =>
        prefab.GetComponent<T>() ?? prefab.AddComponent<T>()
            ?? throw new InvalidOperationException($"Could not add shotgun {typeof(T).Name}");

    internal static void Configure(GameObject prefab, GearItem gear, Inputs inputs)
    {
        if (prefab == null || gear == null) throw new InvalidOperationException("Missing shotgun prefab/GearItem for maintenance");
        if (inputs == null) throw new InvalidOperationException("Shotgun maintenance inputs were not resolved");

        Harvest harvest = GetOrAdd<Harvest>(prefab);
        Cleanable cleanable = GetOrAdd<Cleanable>(prefab);
        Millable millable = GetOrAdd<Millable>(prefab);

        harvest.m_AppliedSkillType = inputs.Harvest.m_AppliedSkillType;
        harvest.m_DurationMinutes = inputs.Harvest.m_DurationMinutes;
        harvest.m_Audio = inputs.Harvest.m_Audio;
        harvest.m_RequiredTools = Copy(inputs.Harvest.m_RequiredTools);
        harvest.m_YieldPowder = Copy(inputs.Harvest.m_YieldPowder);
        harvest.m_YieldPowderAmount = Copy(inputs.Harvest.m_YieldPowderAmount);
        harvest.m_YieldGear = new Il2CppReferenceArray<GearItem>(new[] { inputs.Scrap, inputs.Fir });
        harvest.m_YieldGearUnits = new Il2CppStructArray<int>(new[] { 2, 1 });

        cleanable.m_RequiredGear = Copy(inputs.Cleanable.m_RequiredGear);
        cleanable.m_RequiredGearUnits = Copy(inputs.Cleanable.m_RequiredGearUnits);
        cleanable.m_ConditionIncreaseMin = inputs.Cleanable.m_ConditionIncreaseMin;
        cleanable.m_ConditionIncreaseMax = inputs.Cleanable.m_ConditionIncreaseMax;
        cleanable.m_DurationMinutesMin = inputs.Cleanable.m_DurationMinutesMin;
        cleanable.m_DurationMinutesMax = inputs.Cleanable.m_DurationMinutesMax;
        cleanable.m_CleanAudio = inputs.Cleanable.m_CleanAudio;
        cleanable.m_CleanToolChoices = Copy(inputs.Cleanable.m_CleanToolChoices);
        cleanable.m_RequiresToolToClean = inputs.Cleanable.m_RequiresToolToClean;

        millable.m_RepairRequiredGear = Copy(inputs.Millable.m_RepairRequiredGear);
        millable.m_RepairRequiredGearUnits = Copy(inputs.Millable.m_RepairRequiredGearUnits);
        millable.m_RepairDurationMinutes = inputs.Millable.m_RepairDurationMinutes;
        millable.m_CanRestoreFromWornOut = true;
        millable.m_RecoveryDurationMinutes = 180;
        millable.m_Skill = SkillType.Gunsmithing;
        millable.m_RestoreRequiredGear = new Il2CppReferenceArray<GearItem>(new[] { inputs.Scrap, inputs.Fir });
        millable.m_RestoreRequiredGearUnits = new Il2CppStructArray<int>(new[] { 4, 1 });

        gear.m_Harvest = harvest;
        gear.m_Cleanable = cleanable;
        gear.m_Millable = millable;
    }
}
