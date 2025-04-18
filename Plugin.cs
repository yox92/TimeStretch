using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TimeStretch.Cache;
using UnityEngine;


namespace TimeStretch.Utils
{
    [BepInPlugin("com.spt.TimeStretch", "TimeStretch", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        
        public static ManualLogSource LOGSource;
        public static ConfigEntry<bool> EnableAudioMod;
        public static ConfigEntry<float> TempoMin;
        public static ConfigEntry<float> TempoMax;
        public static volatile bool ShouldStopThreads = false;
        
        // [DllImport("TimeStretchNative.dll")]
        // public static extern void TimeStretchCpp();
        
        public static Harmony HarmonyInstance { get; private set; }
        private void Awake()
        {
            // TimeStretchCpp(); //
            // BatchLogger.Log("🟢 C++ natif chargé");
            LOGSource = Logger;
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
        
        private static void OnTempoChanged()
        {
            LOGSource.LogWarning("🎛️ Tempo modifiy !");
            BatchLogger.Log("[Plugin] 🎛️ Tempo modifiy !");

            CacheObject.ClearAllCache();
    
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