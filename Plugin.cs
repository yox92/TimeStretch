using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TimeStretch.Boot;
using TimeStretch.Entity;
using TimeStretch.Patches;
using TimeStretch.Utils;
using UnityEngine;


namespace TimeStretch
{
    [BepInPlugin("com.spt.TimeStretch", "TimeStretch", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LOGSource;
        public static ConfigEntry<bool> EnableAudioMod;
        public static ConfigEntry<float> TempoMin;
        public static ConfigEntry<float> TempoMax;
        public static ConfigEntry<int> WalkVolume;
        public static ConfigEntry<int> TurnVolume;
        public static ConfigEntry<int> GearVolume;
        public static ConfigEntry<int> StopVolume;
        public static ConfigEntry<int> SprintVolume;
        public static ConfigEntry<int> JumpVolume;
        public static volatile bool ShouldStopThreads = false;
        
        public static Harmony HarmonyInstance { get; private set; }
        private void Awake()
        {
           
            Console.Title = "BatchLogger Console";
            LOGSource = Logger;
            EnableAudioMod = Config.Bind(
                "Mod TimeStretch",
                "Activer le mod audio",
                true,
                "Active ou désactive les remplacements de sons des armes (via Hook Harmony)"
            );
            EnableAudioMod.SettingChanged += (_, _) =>
            {
                ShouldStopThreads = !EnableAudioMod.Value;

                if (ShouldStopThreads)
                {
                    LOGSource.LogWarning("🛑 Le mod TimeStretch disable.");
                    BatchLogger.Log("[Plugin] 🛑 Le mod TimeStretch disable.");
                    CacheObject.ClearAllCache();
                }
                else
                {
                    LOGSource.LogWarning("✅ mod TimeStretch enable.");
                    BatchLogger.Log("[Plugin] ✅ mod TimeStretch enable.");
                }
            };            
            TempoMin = Config.Bind(
                "TimeStretch",
                "Tempo Min (%)",
                -50f,
                new ConfigDescription(
                    "Minimum tempo variation applied to audio (-50% = very slow, 0% = no change)",
                    new AcceptableValueRange<float>(-50f, 0f)
                )
            );
            TempoMax = Config.Bind(
                "TimeStretch",
                "Tempo Max (%)",
                150f,
                new ConfigDescription(
                    "Maximum tempo variation applied to audio (0% = no change, 150% = very fast)",
                    new AcceptableValueRange<float>(0f, 150f)
                )
            );
            TempoMin.SettingChanged += (_, _) => OnTempoChanged();
            TempoMax.SettingChanged += (_, _) => OnTempoChanged();
            WalkVolume = Config.Bind(
                "Body Audio",
                "Walk",
                100,
                new ConfigDescription("", new AcceptableValueRange<int>(1, 100))
            );
            TurnVolume = Config.Bind(
                "Body Audio",
                "Turn",
                100,
                new ConfigDescription("", new AcceptableValueRange<int>(1, 100))
            );
            GearVolume = Config.Bind(
                "Body Audio",
                "Gear",
                100,
                new ConfigDescription("", new AcceptableValueRange<int>(1, 100))
            );
            StopVolume = Config.Bind(
                "Body Audio",
                "Stop",
                100,
                new ConfigDescription("", new AcceptableValueRange<int>(1, 100))
            );
            SprintVolume = Config.Bind(
                "Body Audio",
                "Sprint",
                100,
                new ConfigDescription("", new AcceptableValueRange<int>(1, 100))
            );
            JumpVolume = Config.Bind(
                "Body Audio",
                "Jump",
                100,
                new ConfigDescription("", new AcceptableValueRange<int>(1, 100))
            );
         
            if (!File.Exists(PathsFile.DebugPath))
            {
                File.WriteAllText(PathsFile.DebugPath, "false");
            }
            
            if (!File.Exists(PathsFile.LogFilePath))
            {
                File.WriteAllText(PathsFile.LogFilePath, "");
            }
            
            Logger.LogInfo("Log dans le fichier :" + PathsFile.LogFilePath);
            
            BatchLogger.Init(EnumLoggerMode.DirectWrite);
           
            Application.quitting += BatchLogger.OnApplicationQuit;
            
            HarmonyInstance = new Harmony("com.spt.timestretch");
            HarmonyInstance.PatchAll();
            
        }
        
        private void OnTempoChanged()
        {
            LOGSource.LogWarning("🎛️ Tempo modifiy !");
            BatchLogger.Log("[Plugin] 🎛️ Tempo modifiy !");

            // On ne vide pas tout le cache : seulement les clips transformés
            CacheObject.ClearAllCache();
    
            // Et on relance le traitement de l'arme équipée
            if (!ShouldStopThreads)
            {
                CacheObject.ResetWeaponTracking();
            }
        }
        
    }
}

// var scanner = new GenerateJsonFullAudioClip(Logger);
// scanner.ScanBundlesAndGenerateJson();
//
// Logger.LogInfo("✅ Scan terminé. JSON généré.");

// try
// {
//     Logger.LogInfo("⏳ Patch des bundles audio en cours...");
//     var modifier = new BatchBundleModifier();
//     modifier.ReadApplyChange();
//     Logger.LogInfo("✅ Patch BatchBundleModifier appliqué.");
//     BatchLogger.Info("✅ Patch BatchBundleModifier appliqué.");
// }
// catch (Exception ex)
// {
//     Logger.LogError($"🤮 Erreur dans ApplyFireRateChanges: {ex}");
// }