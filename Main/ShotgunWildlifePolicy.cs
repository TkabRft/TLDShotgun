using Il2Cpp;
using Il2CppTLD.AI;
using UnityEngine;

namespace Shotgun;

/// <summary>Immutable shell policy and shared target accounting for hitscan and optional flying projectiles.</summary>
internal sealed class ShotgunWildlifePolicy
{
    internal const int ContractVersion = 1;
    [ThreadStatic] private static int m_NativeQueryDepth;
    internal static bool QueryingNative => m_NativeQueryDepth != 0;

    private readonly ShotgunPellets.ShotgunAmmo m_Ammo;
    private readonly float m_CriticalMultiplier;
    private readonly int m_BleedThreshold, m_CriticalThreshold;

    private enum Animal { Other, Small, Deer, Wolf, Bear, Moose, Cougar }

    private ShotgunWildlifePolicy(ShotgunPellets.ShotgunAmmo ammo, int tier, int bleed, int critical)
    {
        m_Ammo = ammo;
        m_CriticalMultiplier = Math.Clamp(tier, 0, 4) * 0.3125f;
        m_BleedThreshold = ammo == ShotgunPellets.ShotgunAmmo.Slug ? 1 : bleed;
        m_CriticalThreshold = ammo == ShotgunPellets.ShotgunAmmo.Slug ? 1 : critical;
    }

    // Cached by the optional adapter. BCL delegate/result types keep it independent of this assembly's types.
    internal static ShotgunWildlifePolicy Capture(string ammo, int tier, int bleed, int critical)
    {
        if (!Enum.TryParse(ammo, out ShotgunPellets.ShotgunAmmo kind))
            throw new ArgumentException("Unrecognized shotgun load", nameof(ammo));
        return new(kind, tier, bleed, critical);
    }

    internal Target CreateTarget(BaseAi ai) => new(this, ai);

    internal static bool IsSpecial(BaseAi ai) => ai.m_BearHuntAiRedux != null || ai.m_HuntedPart2WaypointController != null;
    internal static bool HasWildlifeProfile(BaseAi ai) => Classify(ai) != Animal.Other;

    private static Animal Classify(BaseAi ai)
    {
        if (IsSpecial(ai)) return Animal.Other;
        // Ptarmigan has no dedicated AiSubType in the pinned API; use its typed component.
        if (ai.Ptarmigan != null || ai.Rabbit != null || ai.m_AiSubType == AiSubType.Rabbit) return Animal.Small;
        return ai.m_AiSubType switch
        {
            AiSubType.Stag => Animal.Deer,
            AiSubType.Wolf => Animal.Wolf,
            AiSubType.Bear => Animal.Bear,
            AiSubType.Moose => Animal.Moose,
            AiSubType.Cougar => Animal.Cougar,
            _ => Animal.Other
        };
    }

    internal static float NativeScale(LocalizedDamage part)
    {
        m_NativeQueryDepth++;
        try { return part.GetDamageScale(WeaponSource.Unspecified); }
        finally { m_NativeQueryDepth--; }
    }

    internal static float NativeBleed(LocalizedDamage part)
    {
        m_NativeQueryDepth++;
        try { return part.GetBleedOutMinutes(WeaponSource.Unspecified); }
        finally { m_NativeQueryDepth--; }
    }

    internal float SlugCriticalChance(LocalizedDamage part) => part.GetChanceKill(WeaponSource.Rifle) * m_CriticalMultiplier;

    internal sealed class Target
    {
        internal readonly BaseAi Ai;
        private readonly ShotgunWildlifePolicy m_Policy;
        private readonly Animal m_Animal;
        private int m_Hits, m_InRangeHits, m_BatchHits;
        private float m_BatchDamage, m_Bleed, m_BestDamage;
        private bool m_CriticalConsumed, m_BleedAttempted, m_BleedAllowed;
        private LocalizedDamage? m_Part;
        private Vector3 m_Point;
        private string m_Collider = string.Empty;

        internal Target(ShotgunWildlifePolicy policy, BaseAi ai)
        {
            m_Policy = policy;
            Ai = ai;
            m_Animal = Classify(ai);
        }

        internal void BeginBatch() { m_BatchDamage = 0f; m_BatchHits = 0; }

        internal void Add(LocalizedDamage part, float damage, bool inRange, Vector3 point, string collider)
        {
            var ammo = m_Policy.m_Ammo;
            bool pellet = ShotgunPellets.IsPelletAmmo(ammo);
            if (pellet && m_Animal == Animal.Small && Ai.m_CurrentHP > 0f && float.IsFinite(Ai.m_CurrentHP))
                damage = Math.Max(damage, Ai.m_CurrentHP);
            else if (ammo == ShotgunPellets.ShotgunAmmo.Birdshot)
            {
                if (m_Animal is Animal.Deer or Animal.Wolf)
                    damage *= part.m_BodyPart == BodyPart.head ? 0.225f : 0.1f;
                else if (m_Animal is Animal.Bear or Animal.Moose or Animal.Cougar)
                    damage *= 0.02f;
            }
            else if (pellet && m_Animal == Animal.Moose)
                damage *= 0.4f;

            m_Hits++;
            if (inRange) m_InRangeHits++;
            m_BatchHits++;
            m_BatchDamage += damage;
            if (m_Part == null || damage > m_BestDamage)
            {
                m_Part = part;
                m_BestDamage = damage;
                m_Point = point;
                m_Collider = collider;
            }

            float bleed = NativeBleed(part);
            if (m_Animal is Animal.Moose or Animal.Cougar || pellet && m_Animal == Animal.Small)
                bleed = 0f;
            else if (m_Animal != Animal.Other && bleed > 0f)
            {
                if (ammo == ShotgunPellets.ShotgunAmmo.Slug) bleed *= 0.5f;
                else if (ammo == ShotgunPellets.ShotgunAmmo.Scrapshot) bleed *= 1.5f;
                else if (ammo == ShotgunPellets.ShotgunAmmo.Birdshot)
                    bleed = m_Animal is Animal.Deer or Animal.Wolf ? Math.Max(240f, bleed * 4f) : 0f;
            }
            if (bleed > 0f && (m_Bleed <= 0f || bleed < m_Bleed)) m_Bleed = bleed;
        }

        // One result for only new damage; strongest region and eligibility remain cumulative across batches.
        internal (float Damage, float Bleed, bool Critical, LocalizedDamage? Part, Vector3 Point, string Collider, int Hits) Resolve()
        {
            if (m_BatchHits == 0 || Ai == null || Ai.GetAiMode() == AiMode.Dead || m_Part == null)
                return default;
            if (Math.Abs(m_BatchDamage) <= 0.0001f && Ai.m_HuntedPart2WaypointController == null)
                return (m_BatchDamage, 0f, false, m_Part, m_Point, m_Collider, m_BatchHits);
            bool bird = m_Policy.m_Ammo == ShotgunPellets.ShotgunAmmo.Birdshot && m_Animal != Animal.Other;
            bool small = m_Animal == Animal.Small && ShotgunPellets.IsPelletAmmo(m_Policy.m_Ammo);
            bool critical = false;
            if (!m_CriticalConsumed && m_Policy.m_CriticalThreshold > 0 && m_InRangeHits >= m_Policy.m_CriticalThreshold)
            {
                m_CriticalConsumed = true; // Immunity and zero chances also consume the attempt.
                float chance = m_Policy.SlugCriticalChance(m_Part);
                if (ShotgunPellets.IsPelletAmmo(m_Policy.m_Ammo))
                {
                    if (m_Animal == Animal.Moose) chance *= 0.1f;
                    else if (m_Animal == Animal.Cougar) chance *= 0.25f;
                }
                critical = !bird && !small && !Ai.m_IgnoreCriticalHits && chance > 0f && Utils.RollChance(chance);
            }
            float bleed = 0f;
            if (m_Policy.m_BleedThreshold > 0 && m_Hits >= m_Policy.m_BleedThreshold && m_Bleed > 0f)
            {
                if (bird && !m_BleedAttempted)
                {
                    m_BleedAttempted = true;
                    m_BleedAllowed = Utils.RollChance(15f);
                }
                if (!bird || m_BleedAllowed) bleed = m_Bleed;
            }
            return (critical ? float.MaxValue : m_BatchDamage, bleed, critical, m_Part, m_Point, m_Collider, m_BatchHits);
        }
    }
}
