using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TimeStretch.Cache;
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
        public static ConfigEntry<int> FireRateRange;
        public static ConfigEntry<KeyboardShortcut> KeyboardBindingUp;
        public static ConfigEntry<KeyboardShortcut> KeyboardBindingDown;
        public static volatile bool ShouldStopThreads;

        // [DllImport("TimeStretchNative.dll")]
        // public static extern void TimeStretchCpp();

        public static Harmony HarmonyInstance { get; private set; }

        private void Awake()
        {
            InitializeLogger();
            SetupAudioModConfig();
            SetupTempoConfigs();
            SetupFireRateConfigs();
            SetupKeyboardBindings();
            InitializeFiles();
            InitializeBatchLogger();
            SetupHarmonyPatches();
        }
        
        private void InitializeLogger()
        {
            LOGSource = Logger;
        }
        
        private void SetupAudioModConfig()
        {
            EnableAudioMod = Config.Bind(
                "Mod TimeStretch",
                "Enable audio mod", 
                true,
                "Enables or disables weapon sound replacements"
            );
        
            EnableAudioMod.SettingChanged += (_, _) =>
            {
                ShouldStopThreads = !EnableAudioMod.Value;
        
                if (ShouldStopThreads)
                {
                    LOGSource.LogWarning("🛑 mod TimeStretch disable.");
                    BatchLogger.Log("[Plugin] 🛑 mod TimeStretch disable.");
                    CacheObject.ClearAllCache();
                }
                else
                {
                    LOGSource.LogWarning("✅ mod TimeStretch enable.");
                    BatchLogger.Log("[Plugin] ✅ mod TimeStretch enable.");
                }
            };
        }
        
        private void SetupTempoConfigs()
        {
            TempoMin = Config.Bind(
                "TimeStretch",
                "Tempo Min (%)",
                -20f,
                new ConfigDescription(
                    "Minimum tempo variation applied to audio (-20% = very slow, 0% = no change)",
                    new AcceptableValueRange<float>(-20f, 0f)
                )
            );
        
            TempoMax = Config.Bind(
                "TimeStretch",
                "Tempo Max (%)",
                150f,
                new ConfigDescription(
                    "Maximum tempo variation applied to audio (0% = no change, 150% = very fast)", 
                    new AcceptableValueRange<float>(0f, 150f)
                ));
        
            TempoMin.SettingChanged += (_, _) => OnTempoChanged();
            TempoMax.SettingChanged += (_, _) => OnTempoChanged();
        }
        
        private void SetupFireRateConfigs()
        {
            FireRateRange = Config.Bind(
                "FireRateRange",
                "fireRate range",
                25,
                new ConfigDescription(
                    "overClock Fire rate increment (25 or 50)",
                    new AcceptableValueList<int>(25, 50)
                ));
        }
        
        private void SetupKeyboardBindings()
        {
            KeyboardBindingUp = Config.Bind(
                "FireRateRange",
                "RPM Up",
                new KeyboardShortcut(KeyCode.UpArrow),
                "Shortcut to increase FireRateRange RPM (UpArrow)"
            );
        
            KeyboardBindingDown = Config.Bind(
                "FireRateRange",
                "RPM Down", 
                new KeyboardShortcut(KeyCode.DownArrow),
                "Shortcut to decrease FireRateRange RPM (DownArrow)"
            );
        }
        
        private void InitializeFiles()
        {
            if (!File.Exists(PathsFile.DebugPath))
            {
                File.WriteAllText(PathsFile.DebugPath, "false");
            }
        
            if (!File.Exists(PathsFile.LogFilePath))
            {
                File.WriteAllText(PathsFile.LogFilePath, "");
            }
        
            Logger.LogInfo("Log dans le fichier :" + PathsFile.LogFilePath);
        }
        
        private static void InitializeBatchLogger()
        {
            BatchLogger.Init(EnumLoggerMode.DirectWrite);
            Application.quitting += BatchLogger.OnApplicationQuit;
        }
        
        private static void SetupHarmonyPatches()
        {
            HarmonyInstance = new Harmony("com.spt.timestretch");
            HarmonyInstance.PatchAll();
        }
        private static void OnTempoChanged()
        {
            LOGSource.LogWarning("🎛️ Tempo modifiy !");
            BatchLogger.Log("[Plugin] 🎛️ Tempo modifiy !");

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