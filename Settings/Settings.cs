using ModSettings;
using Il2Cpp;
using UnityEngine;
using System.Reflection;


namespace Shotgun
{
    internal enum ShotgunResourceLevel { Low, Medium, High, VeryHigh }

    internal class Settings : JsonModSettings
    {
        internal static Settings Instance;

        [Section("General Settings")]
        [Name("Ammo Switch Hotkey")]
        [Description("Hotkey for swapping ammo types. Vanilla does not handle this, instead using single ammo types for guns and using the reload key for cameras. Default: T")]
        public KeyCode AmmoSwapKeyCode = KeyCode.T;

        [Name("Ammo Wheel Hold Time")]
        [Slider(0.1f, 1.0f, 10)]
        [Description("Seconds to hold the ammo switch hotkey before the ammo wheel opens. A quick tap still switches to the next ammo type. Default: 0.3")]
        public float AmmoRadialHoldSeconds = 0.3f;

        [Name("Fire Both Barrels Modifier")]
        [Description("Hold this key while firing to discharge both barrels at once. Default: Left Alt")]
        public KeyCode DoubleFireKeyCode = KeyCode.LeftAlt;

        [Name("Aim Diagnostics")]
        [Description("Logs shotgun aim requests and transition completion/recovery for troubleshooting. Default: Off")]
        public bool AimDiagnostics = false;

        [Name("Use Rifle Skill Without Shotgun Skill")]
        [Description("Without the optional Shotgun skill, use Rifle for shotgun hit experience and skill benefits. When off, Rifle level has no effect and hits grant no Rifle experience. The separate Shotgun skill takes priority. Default: On")]
        public bool UseRifleSkillWithoutShotgunSkill = true;


        [Section("Crafting Locations")]
        [Name("Allow Vanilla Bullets At Ordinary Forges")]
        [Description("Adds a forge alternative without changing vanilla bullet casting. Takes effect on the next game load. Default: Off")]
        public bool AllowVanillaBulletsAtForge = false;

        [Name("Allow Lead Shot At Ordinary Forges")]
        [Description("Adds forge alternatives for lead buckshot and birdshot portions. Takes effect on the next game load. Default: Off")]
        public bool AllowLeadShotAtForge = false;

        [Name("Allow Lead Shotgun Shells At Workbenches")]
        [Description("Adds workbench alternatives for slugs, buckshot, and birdshot. Vanilla rifle/revolver cartridges are unchanged. Takes effect on the next game load. Default: Off")]
        public bool AllowLeadShellsAtWorkbench = false;

        [Section("Shotgun Finds")]
        [Name("Spawn Usable Shotguns")]
        public bool SpawnUsableShotguns = true;

        [Name("Usable Shotgun Chance")]
        [Description("Absolute percentage at each selected gun location. Disabled or resource-ineligible chances become empty slots. Changes affect unvisited areas only.")]
        [Slider(0, 100)]
        public int UsableShotgunChance = 50;

        [Name("Minimum Resources For Usable Shotguns")]
        public ShotgunResourceLevel UsableShotgunMinimumResources = ShotgunResourceLevel.High;

        [Name("Spawn Ruined Shotguns")]
        public bool SpawnRuinedShotguns = true;

        [Name("Ruined Shotgun Chance")]
        [Description("Absolute percentage at each selected gun location. Restore at a milling machine. Combined usable/ruined chances cannot exceed 100%. Changes affect unvisited areas only.")]
        [Slider(0, 100)]
        public int RuinedShotgunChance = 50;

        [Name("Minimum Resources For Ruined Shotguns")]
        public ShotgunResourceLevel RuinedShotgunMinimumResources = ShotgunResourceLevel.Medium;

        [Section("Slug Settings")]
        [Name("Slug Base Damage")]
        [Description("Base damage dealt by slugs within accuracy range. Default: 120")]
        [Slider(50, 150)]
        public int SlugBaseDamage = 120;

        [Name("Slug Accuracy Range")]
        [Description("Distance before damage begins to drop per meter. Default: 30m")]
        [Slider(10, 50)]
        public int SlugAccuracyRange = 30;

        [Name("Slug Damage Falloff Rate")]
        [Description("Damage reduction per meter beyond accuracy range. Default: 5")]
        [Slider(1f, 10f)]
        public float SlugDamageFalloff = 5f;

        [Name("Slug Minimum Damage")]
        [Description("Minimum damage dealt as long as a hit is registered. Default: 20")]
        [Slider(0f, 50f)]
        public float SlugMinimumDamage = 20f;


        [Section("Buckshot Settings")]
        [Name("Buckshot Base Damage")]
        [Description("Base damage dealt by buckshot within accuracy range. Default: 15")]
        [Slider(5, 25)]
        public int BuckshotBaseDamage = 15;

        [Name("Buckshot Accuracy Range")]
        [Description("Distance before damage begins to drop per meter. Default: 25m")]
        [Slider(10, 40)]
        public int BuckshotAccuracyRange = 25;

        [Name("Buckshot Damage Falloff Rate")]
        [Description("Damage reduction per meter beyond accuracy range. Default: 1")]
        [Slider(0.1f, 10f)]
        public float BuckshotDamageFalloff = 1f;

        [Name("Buckshot Minimum Damage")]
        [Description("Minimum damage dealt as long as a hit is registered. Default: 5")]
        [Slider(0f, 15f)]
        public float BuckshotMinimumDamage = 5f;

        [Name("Buckshot Pellet Spread")]
        [Description("Conical spread (total included angle) of pellets. Default: 2 degrees")]
        [Slider(0.2f, 5f)]
        public float BuckshotPelletSpread = 2f;

        [Name("Buckshot Pellet Count")]
        [Description("Pellet Count. Default: 9")]
        [Slider(6f, 12f)]
        public int BuckshotPelletCount = 9;

        [Name("Buckshot Pellets To Bleed")]
        [Description("Pellets that must hit one animal in a single shot to start a bleed. 0 = never. Default: 3")]
        [Slider(0, 12)]
        public int BuckshotBleedPellets = 3;

        [Name("Buckshot Pellets For Critical Roll")]
        [Description("Pellets within accuracy range that must hit one animal for one critical-hit roll. 0 = never. Default: 6")]
        [Slider(0, 12)]
        public int BuckshotCriticalPellets = 6;


        [Section("Birdshot Settings")]
        [Name("Birdshot Base Damage")]
        [Description("Base damage dealt by Birdshot within accuracy range. Default: 3")]
        [Slider(1f, 5f)]
        public float BirdshotBaseDamage = 3f;

        [Name("Birdshot Accuracy Range")]
        [Description("Distance before damage begins to drop per meter. Default: 20m")]
        [Slider(10, 30)]
        public int BirdshotAccuracyRange = 20;

        [Name("Birdshot Damage Falloff Rate")]
        [Description("Damage reduction per meter beyond accuracy range. Default: 0.2")]
        [Slider(0.01f, 1f)]
        public float BirdshotDamageFalloff = 0.2f;

        [Name("Birdshot Minimum Damage")]
        [Description("Minimum damage dealt as long as a hit is registered. Default: 1")]
        [Slider(0f, 3f)]
        public float BirdshotMinimumDamage = 1f;

        [Name("Birdshot Pellet Spread")]
        [Description("Conical spread (total included angle) of pellets. Default: 4 degrees")]
        [Slider(0.2f, 8f)]
        public float BirdshotPelletSpread = 4f;

        [Name("Birdshot Pellet Count")]
        [Description("Pellet Count. Default: 30")]
        [Slider(20, 40f)]
        public int BirdshotPelletCount = 30;

        [Name("Birdshot Pellets To Bleed")]
        [Description("Pellets that must hit one animal in a single shot to start a bleed. 0 = never. Default: 15")]
        [Slider(0, 40)]
        public int BirdshotBleedPellets = 15;

        [Name("Birdshot Pellets For Critical Roll")]
        [Description("Pellets within accuracy range that must hit one animal for one critical-hit roll. 0 = never. Default: 0")]
        [Slider(0, 40)]
        public int BirdshotCriticalPellets = 0;


        [Section("Scrapshot Settings")]
        [Name("Scrapshot Base Damage Per Pellet")]
        [Slider(1, 25)]
        public int ScrapshotBaseDamage = 13;
        [Name("Scrapshot Accuracy Range")]
        [Slider(5, 40)]
        public int ScrapshotAccuracyRange = 25;
        [Name("Scrapshot Damage Falloff Per Meter")]
        [Slider(0.1f, 10f)]
        public float ScrapshotDamageFalloff = 1f;
        [Name("Scrapshot Minimum Damage Per Pellet")]
        [Slider(0f, 15f)]
        public float ScrapshotMinimumDamage = 5f;
        [Name("Scrapshot Full Cone Spread")]
        [Slider(0.2f, 8f)]
        public float ScrapshotPelletSpread = 2.2f;
        [Name("Scrapshot Pellet Count")]
        [Slider(1, 12)]
        public int ScrapshotPelletCount = 6;
        [Name("Scrapshot Pellets To Bleed")]
        [Description("Hits on one animal per shell. 0 disables bleeding eligibility.")]
        [Slider(0, 12)]
        public int ScrapshotBleedPellets = 3;
        [Name("Scrapshot Pellets For Critical Roll")]
        [Description("In-range hits on one animal per shell. 0 disables critical rolls.")]
        [Slider(0, 12)]
        public int ScrapshotCriticalPellets = 6;

        [Section("Recoil Settings")]
        [Name("Recoil Pitch Kick")]
        [Description("Strength of the upward camera jerk when the shotgun fires, at active firearm skill level 1. Scales down linearly to zero at level 5. Set to 0 to disable. Default: 12")]
        [Slider(0f, 20f)]
        public float RecoilMaxPitchKick = 12f;

        [Name("Recoil Yaw Kick")]
        [Description("Strength of the random sideways camera jerk when the shotgun fires, at active firearm skill level 1. Scales down to zero at level 5. Set to 0 to disable. Default: 4")]
        [Slider(0f, 8f)]
        public float RecoilMaxYawKick = 4f;

        [Name("Recoil Rise Time")]
        [Description("Seconds over which the upward kick is applied. Higher = a slower, smoother rise. Default: 0.12")]
        [Slider(0.02f, 0.5f)]
        public float RecoilRiseTime = 0.12f;

        [Name("Recoil Recovery Fraction")]
        [Description("Fraction of the kick that smoothly glides back toward center after the rise. 0 = stays fully kicked up; 1 = returns all the way to your original aim. Default: 0.25")]
        [Slider(0f, 1f)]
        public float RecoilRecoveryFraction = 0.25f;

        [Name("Recoil Recovery Time")]
        [Description("Seconds over which the recovery glide happens, after the rise. Higher = a slower, lazier settle. Default: 0.25")]
        [Slider(0.02f, 1f)]
        public float RecoilRecoveryTime = 0.25f;


        [Section("(DEBUG) Idle Variation Generator")]
        [Name("Idle Variation Speed")]
        [Description("Fine-tunes how fast the smooth random 'Idle_Variation' blend (0..1) drifts, relative to in-game time. 1.0 = baseline, 0.1 = 10x slower, 100 = 100x faster. Default: 10")]
        [Slider(0.1f, 100.0f)]
        public float IdleVariationSpeed = 10.0f;

        [Name("Idle Variation Min Interval")]
        [Description("Shortest interval between targets, in scaled time units. Lower = more frequent direction changes. Default: 0.5")]
        [Slider(0.05f, 5.0f)]
        public float IdleVariationMinInterval = 0.5f;

        [Name("Idle Variation Max Interval")]
        [Description("Longest interval between targets, in scaled time units. Higher = longer lazy drifts. Default: 2.0")]
        [Slider(0.1f, 10.0f)]
        public float IdleVariationMaxInterval = 2.0f;

        [Name("Idle Variation Min Target Distance")]
        [Description("Smallest jump (0..1) a new target may sit from the current value. Higher = always-large swings. Default: 0.2")]
        [Slider(0.0f, 1.0f)]
        public float IdleVariationMinTargetDistance = 0.2f;

        [Name("Idle Variation Max Target Distance")]
        [Description("Largest jump (0..1) a new target may sit from the current value. Lower = gentler, smaller swings. Default: 0.8")]
        [Slider(0.0f, 1.0f)]
        public float IdleVariationMaxTargetDistance = 0.8f;

        [Name("Idle Variation Output Min")]
        [Description("Lower bound of the blend output fed to the animator. Default: 0.0")]
        [Slider(0.0f, 1.0f)]
        public float IdleVariationOutputMin = 0.0f;

        [Name("Idle Variation Output Max")]
        [Description("Upper bound of the blend output fed to the animator. Default: 1.0")]
        [Slider(0.0f, 1.0f)]
        public float IdleVariationOutputMax = 1.0f;

        [Name("Rare Idle Min Delay")]
        [Description("Shortest time standing still before the rare idle can play, in seconds. Default: 25")]
        [Slider(5f, 120f)]
        public float RareIdleMinSeconds = 25f;

        [Name("Rare Idle Max Delay")]
        [Description("Longest time standing still before the rare idle plays, in seconds. Default: 60")]
        [Slider(5f, 120f)]
        public float RareIdleMaxSeconds = 60f;



        [Section("(DEBUG) Animation Settings")]
        [Name("Idle Animation Speed Multiplier")]
        [Description("Multiplier to idle animation speed. Base: 0.25")]
        [Slider(0.05f, 2.0f)]
        public float IdleSpeedMulitplier = 0.25f;

        [Name("Base Walking Speed Multiplier")]
        [Description("Multipler to base walking animation speed. Base: 1.0")]
        [Slider(0.05f, 2.0f)]
        public float BaseWalkingSpeedMulitplier = 1.0f;

        [Name("Max Walking Speed Multiplier")]
        [Description("Multipler to max walking animation speed. Applied gradually as actual walking speed increases, reaching this multiplier just before transitioning to sprinting. Default: 1.0")]
        [Slider(1.0f, 2.0f)]
        public float MaxWalkingSpeedMulitplier = 1.0f;

        [Name("Max Walking Speed")]
        [Description("Sets the max speed that is considered 'walking' by the animation system. Transitioning to 'sprint' still requires holding shift, this will simply adjust at what point the animation system transitions in conjunction with holding sprint. Default: 5.0")]
        [Slider(3.0f, 7.0f)]
        public float MaxWalkingSpeed = 5.0f;

        [Name("Base Sprinting Speed Multiplier")]
        [Description("Multipler to base sprinting animation speed. Base: 0.5")]
        [Slider(0.05f, 2.0f)]
        public float BaseSprintingSpeedMulitplier = 0.5f;

        [Name("Max Sprinting Speed Multiplier")]
        [Description("Multipler to max sprinting animation speed. Applied gradually as actual sprinting speed increases, reaching this multiplier just before transitioning to sprinting. Default: 1.0")]
        [Slider(1.0f, 2.0f)]
        public float MaxSprintingSpeedMulitplier = 1.0f;

        [Name("Max Sprinting Speed")]
        [Description("Sets the max speed that is considered 'sprinting' by the animation system. In-game speed can still surpass this, but speed multipliers stop having any effect once speed is above this threshold. Default: 10.0")]
        [Slider(8.0f, 12.0f)]
        public float MaxSprintingSpeed = 10.0f;

        [Name("Base Reload Speed Multiplier")]
        [Description("Sets the base reload speed multiplier applied regardless of active firearm skill level.")]
        [Slider(0.1f, 10f)]
        public float BaseReloadSpeedMultiplier = 1.0f;

        [Name("Per-Level Reload Speed Multiplier")]
        [Description("Sets the reload speed increase per active firearm skill level, measured in additional percentage.")]
        [Slider(0f, 20f)]
        public float AdditionalReloadSpeedMultiplier = 10.0f;

        [Name("Base Aim Speed Multiplier")]
        [Description("Sets the base aim speed multiplier applied regardless of active firearm skill level.")]
        [Slider(0.1f, 10f)]
        public float BaseAimSpeedMultiplier = 1.0f;

        [Name("Per-Level Aim Speed Multiplier")]
        [Description("Sets the aim speed increase per active firearm skill level, measured in additional percentage.")]
        [Slider(0f, 20f)]
        public float AdditionalAimSpeedMultiplier = 10.0f;

        [Name("Base Equip Speed Multiplier")]
        [Description("Sets the base equip speed multiplier applied regardless of active firearm skill level.")]
        [Slider(0.1f, 10f)]
        public float BaseEquipSpeedMultiplier = 1.0f;

        [Name("Per-Level Equip Speed Multiplier")]
        [Description("Sets the equip speed increase per active firearm skill level, measured in additional percentage.")]
        [Slider(0f, 20f)]
        public float AdditionalEquipSpeedMultiplier = 10.0f;

        internal Settings() : base(Path.Combine(DataFolderPath, DataFolderPath))
        {
            Initialize();
        }

        protected void Initialize()
        {
            Instance = this;
            ValidateSpawnChances();
            AddToModSettings("Shotgun");
            RefreshGUI();
        }

        protected override void OnConfirm()
        {
            ValidateSpawnChances();
            base.OnConfirm();
            ShotgunSkill.ApplyAnimatorParams(Main.Instance?.Configuration?.Refs?.ModdedAnimator);
            ShotgunSkill.RefreshCurrentWeapon();
        }

        internal void ValidateSpawnChances()
        {
            UsableShotgunChance = Math.Clamp(UsableShotgunChance, 0, 100);
            RuinedShotgunChance = Math.Clamp(RuinedShotgunChance, 0, 100 - UsableShotgunChance);
        }

        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            base.OnChange(field, oldValue, newValue);
            if (field.Name == nameof(UsableShotgunChance))
                RuinedShotgunChance = Math.Min(RuinedShotgunChance, 100 - UsableShotgunChance);
            else if (field.Name == nameof(RuinedShotgunChance))
                UsableShotgunChance = Math.Min(UsableShotgunChance, 100 - RuinedShotgunChance);
            else return;
            RefreshGUI();
        }
    }
}
