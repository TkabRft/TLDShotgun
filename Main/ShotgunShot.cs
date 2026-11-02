using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.AI;
using MelonLoader;

namespace Shotgun;

internal static partial class ShotgunPellets
{
    // Existing nested type name and saved numeric ammunition identifiers remain unchanged.
    internal enum ShotgunAmmo { None = 0, Slug = 1, Buckshot = 2, Birdshot = 3, Scrapshot = 4 }

    internal sealed class Shot
    {
        internal readonly ShotgunAmmo Ammo;
        internal readonly Shot? Previous;
        internal bool HitAnimal, Failed, Closed;
        internal Shot(ShotgunAmmo ammo, Shot? previous) { Ammo = ammo; Previous = previous; }
    }

    private static Shot? mShot;
    internal static bool InShotgunShot => mShot != null;
    internal static ShotgunAmmo CurrentAmmo => mShot?.Ammo ?? ShotgunAmmo.None;

    internal static Shot BeginShot(ShotgunAmmo ammo) => mShot = new Shot(ammo, mShot);

    internal static void EndShot(Shot? shot, bool failed = false)
    {
        if (shot == null || shot.Closed) return;
        shot.Closed = true;
        mShot = shot.Previous;
        // Restore context before calling integrations, which can reenter or throw.
        if (!failed && !shot.Failed && shot.HitAnimal && shot.Ammo != ShotgunAmmo.None)
            ShotgunSkill.AwardHit();
    }

    private static bool QualifyingHit(BaseAi ai, float damage, DamageSource source) => mShot != null
        && source == DamageSource.Player && damage > 0f && ai.GetAiMode() != AiMode.Dead;

    private static void RecordHit(bool qualified)
    {
        if (qualified && mShot != null) mShot.HitAnimal = true;
    }

    // Slugs use native resolution; pellet loads also reach these native damage methods.
    // A pre-damage snapshot counts a killing hit but excludes an already dead animal.
    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new Type[] { typeof(float), typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class AnimalHitWithBleeding
    {
        internal static void Prefix(BaseAi __instance, float __0, DamageSource __2, out bool __state) =>
            __state = QualifyingHit(__instance, __0, __2);
        internal static void Postfix(bool __state) => RecordHit(__state);
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new Type[] { typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class AnimalHit
    {
        internal static void Prefix(BaseAi __instance, float __0, DamageSource __1, out bool __state) =>
            __state = QualifyingHit(__instance, __0, __1);
        internal static void Postfix(bool __state) => RecordHit(__state);
    }

    [HarmonyPatch(typeof(vp_Bullet), nameof(vp_Bullet.Start))]
    internal static class vp_Bullet_ResolveShotgunShot
    {
        internal static bool Prefix(vp_Bullet __instance, out Shot? __state)
        {
            __state = null;
            if (__instance.m_GunType != GunTypeCustom) return true;
            ShotgunAmmo ammo = GetLoadedAmmo();
            __state = BeginShot(ammo);
            __instance.DamageFalloffPerMeterBeyondEffectiveRange = GetFalloffPerMeter(ammo);
            __instance.MinimumDamageFalloffBeyondEffectiveRange = GetMinimumDamage(ammo);
            if (!IsPelletAmmo(ammo)) return true;
            try { ResolvePellets(__instance, ammo); }
            catch (Exception e)
            {
                __state.Failed = true;
                MelonLogger.Error($"Pellet resolution failed: {e}");
            }
            return false;
        }

        internal static void Postfix(Shot? __state) => EndShot(__state);

        internal static Exception? Finalizer(Shot? __state, Exception? __exception)
        {
            EndShot(__state, __exception != null);
            return __exception;
        }
    }
}
