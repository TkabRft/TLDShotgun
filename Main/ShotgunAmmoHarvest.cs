using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.AddressableAssets;

namespace Shotgun;

internal static class ShotgunAmmoHarvest
{
    internal const float PowderKilogramsPerRound = 0.01f;

    internal static void Configure(Harvest harvest)
    {
        PowderType powder = Addressables.LoadAssetAsync<PowderType>("POWDER_Gunpowder").WaitForCompletion();
        if (powder == null) throw new System.InvalidOperationException("Missing POWDER_Gunpowder for shotgun harvesting");
        // Native harvesting multiplies per-unit yields by the quantity harvested.
        // A box is a stack of rounds, not a separate six-round powder yield.
        harvest.m_YieldPowder = new Il2CppReferenceArray<PowderType>(new[] { powder });
        harvest.m_YieldPowderAmount = new Il2CppStructArray<ItemWeight>(new[] { ItemWeight.FromKilograms(PowderKilogramsPerRound) });
    }
}
