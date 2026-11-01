using MelonLoader;
using UnityEngine;
using System.Reflection;
using Il2Cpp;
using MelonLoader.Utils;
using Shotgun.Utility;
using UnityEngine.AddressableAssets;
using Il2CppVLB;
using HarmonyLib;
using Il2CppTLD.Gameplay;
using static Il2Cpp.Utils;
using Il2CppInterop.Runtime.Injection;
using UnityEngine.AddressableAssets.ResourceLocators;
using Il2CppAtlas.Unity;


namespace Shotgun
{
    public class Main : MelonMod
    {
        public const string Version = "0.13.5";

        internal static Main? Instance { get; private set; }
        internal Configuration Configuration { get; private set; } = new();
        internal Settings? Settings { get; private set; }
        internal bool LateInitialized { get; private set; } = false;
        internal Vector3 LastPosition { get; set; }

        private bool mResourceLocatorInitialized = false;

        internal readonly SmoothRandomBlend IdleVariation = new();


        public override void OnEarlyInitializeMelon()
        {
            EarlyInitialize();
        }

        internal void EarlyInitialize()
        {
            try
            {
                MelonLogger.Msg("EarlyInitialize");
                Instance = this;
                string modsPath = MelonEnvironment.ModsDirectory;
                string basePath = Path.Combine(modsPath, DataFolderPath);
                Directory.CreateDirectory(basePath);
                Settings = new();
                Versioning.IsVersionCorrect(basePath, Version);
                EmbeddedAssetPackage.Install(modsPath);
                MelonLogger.Msg("EarlyInitialize Complete");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"EarlyInitialize Failed: {e}");
            }

        }

        public override void OnInitializeMelon()
        {
            ClassInjector.RegisterTypeInIl2Cpp<ModdedResourceLocator>(new RegisterTypeOptions()
            {
                Interfaces = new Il2CppInterfaceCollection(new[] { typeof(IResourceLocator) })
            });

            ClassInjector.RegisterTypeInIl2Cpp<AnimationEventProxy>();

            ShotgunSkill.Init();
        }

        public override void OnUpdate()
        {
            EmbeddedAssetPackage.Update();
            ShotgunAudio.Tick();
        }

        public override void OnLateUpdate() => ShotgunAim.Tick();


        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName is "MainMenu" or "Boot")
            {
                ShotgunSpawnPolicy.Reset();
                ShotgunAim.Reset("menu");
                ShotgunAudio.Reset();
            }
            ShotgunAvailability.ApplyToCurrentMode();
            if (!mResourceLocatorInitialized)
            {
                Addressables.AddResourceLocator(new ModdedResourceLocator().Cast<IResourceLocator>());
                mResourceLocatorInitialized = true;
            }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            GroupSpawner.ForgetScene(sceneName);
        }

        internal void LateInitialize()
        {
            try
            {
                string sceneName = GameManager.m_ActiveScene;
                if (sceneName.Contains("MainMenu") || sceneName.Contains("Boot"))
                {
                    MelonLogger.Msg("Delaying late initialize until non-boot/main scene load");
                    LateInitialized = false;
                    return;
                }
                if (LateInitialized)
                {
                    MelonLogger.Msg("LateInitialization already performed, skipping until next boot/mainmenu scene load");
                    return;
                }
                MelonLogger.Msg("LateInitializing");
                LateInitialized = Configuration.PrepareShotgun();
                MelonLogger.Msg(LateInitialized ? "LateInitialize Complete" : "LateInitialize Failed!");
                MelonLogger.Msg("");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"LateInitialize Failed: {e}");
            }
        }
    }
}
