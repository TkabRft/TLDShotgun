using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.AI;
using Il2CppTLD.Gear;
using MelonLoader;
using UnityEngine;

namespace Shotgun
{
    internal static partial class ShotgunPellets
    {

        internal const int BulletLayerMask = 0x180e1b41;
        internal const int GearLayer = 17;
        internal const int MaxImpactSoundsPerShot = 3;


        private static readonly Dictionary<IntPtr, PelletTarget> mTargets = new Dictionary<IntPtr, PelletTarget>();
        private static readonly HashSet<IntPtr> mSoundedSurfaces = new HashSet<IntPtr>();

        internal static bool MuteImpactAudio { get; private set; }


        internal sealed class PelletTarget
        {
            internal BaseAi Ai;
            internal int Pellets;
            internal int PelletsInRange;
            internal float Damage;
            internal float BleedMinutes;
            internal LocalizedDamage HeaviestPart;
            internal float HeaviestPartDamage;
            internal string ColliderName = string.Empty;
        }


        internal static ShotgunAmmo GetLoadedAmmo()
        {
            PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
            if (playerManager == null) return ShotgunAmmo.None;
            if (playerManager.m_ItemInHands == null) return ShotgunAmmo.None;
            GunItem gun = playerManager.m_ItemInHands.m_GunItem;
            if (gun == null) return ShotgunAmmo.None;
            if (gun.m_GunType != GunTypeCustom) return ShotgunAmmo.None;

            GearItemData ammoData;
            if (!gun.TryGetSelectedAmmo(out ammoData)) return ShotgunAmmo.None;
            if (ammoData == null) return ShotgunAmmo.None;

            return ShotgunAmmoCatalog.Find(ammoData.name)?.Kind ?? ShotgunAmmo.None;
        }

        internal static bool IsPelletAmmo(ShotgunAmmo ammo) =>
            ammo == ShotgunAmmo.Buckshot || ammo == ShotgunAmmo.Birdshot || ammo == ShotgunAmmo.Scrapshot;

        internal static int GetPelletCount(ShotgunAmmo ammo) => ShotgunAmmoCatalog.Read(ammo).Pellets;
        internal static float GetPelletSpread(ShotgunAmmo ammo) => ShotgunAmmoCatalog.Read(ammo).Spread / 2f;
        internal static float GetFalloffPerMeter(ShotgunAmmo ammo) => ShotgunAmmoCatalog.Read(ammo).Falloff;
        internal static float GetMinimumDamage(ShotgunAmmo ammo) => ShotgunAmmoCatalog.Read(ammo).MinimumDamage;
        internal static int GetBleedPelletThreshold(ShotgunAmmo ammo) => ShotgunAmmoCatalog.Read(ammo).BleedPellets;
        internal static int GetCriticalPelletThreshold(ShotgunAmmo ammo) => ShotgunAmmoCatalog.Read(ammo).CriticalPellets;


        private static bool MeetsPelletThreshold(int pellets, int threshold) => threshold > 0 && pellets >= threshold;


        internal static void ResolvePellets(vp_Bullet bullet, ShotgunAmmo ammo)
        {
            Transform bulletTransform = bullet.transform;
            Vector3 origin = bulletTransform.position;
            Quaternion aim = bulletTransform.rotation;
            Transform playerTransform = GameManager.GetPlayerTransform();
            if (playerTransform == null)
            {
                UnityEngine.Object.Destroy(bullet.gameObject);
                return;
            }
            Vector3 playerPosition = playerTransform.position;
            int pelletCount = GetPelletCount(ammo);
            float spread = GetPelletSpread(ammo);
            float falloffPerMeter = GetFalloffPerMeter(ammo);
            float minimumDamage = GetMinimumDamage(ammo);
            bool anyHit = false;
            RaycastHit firstHit = default;

            CougarManager cougarManager = GameManager.GetCougarManager();
            if (cougarManager != null) cougarManager.MaybeAimNearMiss(bulletTransform.forward);

            mTargets.Clear();
            mSoundedSurfaces.Clear();
            MatchTransform.EnableCollidersForAllActive(true);
            try
            {
                for (int i = 0; i < pelletCount; i++)
                {
                    float roll = UnityEngine.Random.Range(0, 360);
                    float yaw = UnityEngine.Random.Range(-spread, spread);
                    Vector3 direction = aim * Quaternion.Euler(0f, 0f, roll) * Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

                    RaycastHit hit;
                    if (!AiUtils.RaycastWithAimAssist(origin, direction, out hit, bullet.Range, bullet.MinDistanceForAimAssist, bullet.Accuracy, 0f, BulletLayerMask)) continue;
                    if (hit.collider == null) continue;

                    if (!anyHit)
                    {
                        anyHit = true;
                        firstHit = hit;
                    }
                    BaseAi ai = AiUtils.GetBaseAiFromObject(hit.collider.gameObject);
                    SpawnPelletImpact(bullet, hit, ai != null ? ai.Pointer : hit.collider.gameObject.Pointer);

                    if (ai == null || ai.GetAiMode() == AiMode.Dead)
                    {
                        MaybePushGear(bullet, hit, pelletCount);
                        continue;
                    }

                    LocalizedDamage part = hit.collider.GetComponent<LocalizedDamage>();
                    if (part == null) continue;

                    AddPelletHit(bullet, ai, part, hit, playerPosition, falloffPerMeter, minimumDamage);
                }

                foreach (PelletTarget target in mTargets.Values)
                {
                    ApplyToTarget(target, playerPosition, ammo);
                }
            }
            finally
            {
                mTargets.Clear();
                mSoundedSurfaces.Clear();
                MatchTransform.EnableCollidersForAllActive(false);
                FinishBullet(bullet, anyHit, firstHit);
            }
        }


        private static void SpawnPelletImpact(vp_Bullet bullet, RaycastHit hit, IntPtr surface)
        {
            bullet.transform.position = hit.point;
            MuteImpactAudio = mSoundedSurfaces.Contains(surface) || mSoundedSurfaces.Count >= MaxImpactSoundsPerShot;
            mSoundedSurfaces.Add(surface);
            try
            {
                bullet.SpawnImpactEffects(hit);
            }
            finally
            {
                MuteImpactAudio = false;
            }
        }


        private static void AddPelletHit(vp_Bullet bullet, BaseAi ai, LocalizedDamage part, RaycastHit hit, Vector3 playerPosition, float falloffPerMeter, float minimumDamage)
        {
            float bleed = part.GetBleedOutMinutes(WeaponSource.Unspecified);
            float damage = bullet.Damage * part.GetDamageScale(WeaponSource.Unspecified);
            float distance = Vector3.Distance(playerPosition, hit.collider.transform.position);
            bool inRange = distance < bullet.Accuracy;
            if (!inRange)
            {
                damage -= (distance - bullet.Accuracy) * falloffPerMeter;
                if (damage < minimumDamage) damage = minimumDamage;
            }

            PelletTarget target;
            if (!mTargets.TryGetValue(ai.Pointer, out target))
            {
                target = new PelletTarget { Ai = ai };
                mTargets[ai.Pointer] = target;
            }
            target.Pellets++;
            if (inRange) target.PelletsInRange++;
            target.Damage += damage;
            if (bleed > 0f && (target.BleedMinutes <= 0f || bleed < target.BleedMinutes)) target.BleedMinutes = bleed;
            if (target.HeaviestPart == null || damage > target.HeaviestPartDamage)
            {
                target.HeaviestPart = part;
                target.HeaviestPartDamage = damage;
                target.ColliderName = hit.collider.name;
            }
        }


        private static void ApplyToTarget(PelletTarget target, Vector3 playerPosition, ShotgunAmmo ammo)
        {
            BaseAi ai = target.Ai;
            if (ai == null) return;
            if (ai.GetAiMode() == AiMode.Dead) return;
            if (target.HeaviestPart == null) return;
            if (Mathf.Abs(target.Damage) <= 0.0001f && ai.m_HuntedPart2WaypointController == null) return;

            float bleedMinutes = MeetsPelletThreshold(target.Pellets, GetBleedPelletThreshold(ammo)) ? target.BleedMinutes : 0f;
            float damage = target.Damage;
            if (!ai.m_IgnoreCriticalHits
                && MeetsPelletThreshold(target.PelletsInRange, GetCriticalPelletThreshold(ammo))
                && target.HeaviestPart.RollChanceToKill(WeaponSource.Unspecified))
            {
                damage = float.MaxValue;
            }

            ai.SetupDamageForAnim(target.HeaviestPart.transform.position, playerPosition, target.HeaviestPart);
            ai.ApplyDamage(damage, bleedMinutes, DamageSource.Player, target.ColliderName);
            ai.MaybeFleeOrAttackFromProjectileHit(WeaponSource.Unspecified);
        }


        private static void MaybePushGear(vp_Bullet bullet, RaycastHit hit, int pelletCount)
        {
            GameObject hitObject = hit.collider.gameObject;
            if (hitObject.layer != GearLayer) return;
            GearItem gearItem = hitObject.GetComponent<GearItem>();
            if (gearItem == null) return;

            float scale = 1f / Mathf.Max(1, pelletCount);
            Vector3 force = (-hit.normal * bullet.m_GearImpactForce + Vector3.up * UnityEngine.Random.Range(0f, bullet.m_GearImpactUpwardForce)) * scale;
            float torque = bullet.m_GearImpactTorqueForce;
            Vector3 torqueVector = new Vector3(UnityEngine.Random.Range(-torque, torque), UnityEngine.Random.Range(-torque, torque), UnityEngine.Random.Range(-torque, torque)) * scale;
            gearItem.ApplyForce(force, torqueVector);
        }


        private static void FinishBullet(vp_Bullet bullet, bool anyHit, RaycastHit firstHit)
        {
            GameObject bulletObject = bullet.gameObject;
            if (!anyHit || firstHit.collider == null)
            {
                UnityEngine.Object.Destroy(bulletObject);
                return;
            }

            bulletObject.transform.position = firstHit.point;
            bulletObject.transform.SetParent(firstHit.transform, true);
            Renderer renderer = bulletObject.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled)
            {
                vp_DecalManager.Add(bulletObject);
                return;
            }
            UnityEngine.Object.Destroy(bulletObject);
        }


        [HarmonyPatch(typeof(AkSoundEngine), nameof(AkSoundEngine.PostEvent), new Type[] { typeof(string), typeof(GameObject) })]
        internal static class AkSoundEngine_MuteRepeatedPelletImpacts
        {
            internal static bool Prefix(ref uint __result)
            {
                if (!MuteImpactAudio) return true;
                __result = 0;
                return false;
            }
        }


        [HarmonyPatch(typeof(LocalizedDamage), nameof(LocalizedDamage.RollChanceToKill), new Type[] { typeof(WeaponSource) })]
        internal static class LocalizedDamage_ShotgunCriticals
        {
            internal static void Postfix(LocalizedDamage __instance, ref bool __result)
            {
                if (!InShotgunShot) return;
                if (CurrentAmmo == ShotgunAmmo.None) return;

                float shotgunTierCritMultiplier = ShotgunSkill.GetCurrentTier() * 0.25f;
                float adjustedChance = __instance.GetChanceKill(WeaponSource.Rifle) * shotgunTierCritMultiplier * 1.25f;
                __result = Utils.RollChance(adjustedChance);
            }
        }
    }
}
