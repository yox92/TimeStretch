using System;
using BepInEx.Logging;
using HarmonyLib;
using TimeStretch.Cache;
using TimeStretch.Utils;
using Logger = BepInEx.Logging.Logger;

namespace TimeStretch.Boot
{
    
    [HarmonyPatch(typeof(MainMenuControllerClass), nameof(MainMenuControllerClass.Execute))]
    public class PatchMainMenuReady
    {
        private static readonly ManualLogSource Log = new ManualLogSource("PatchMainMenuReady");

        static PatchMainMenuReady()
        {
            Logger.Sources.Add(Log);
        }
 
        [HarmonyPostfix]
        public static void Postfix()
        {
            Log.LogInfo("[PatchMainMenuReady] Main menu detected — initializing components");
            BatchLogger.Info("✅ [PatchMainMenuReady] Main menu detected — initializing components");
            try
            {
                JsonCache.LoadJsonFireRate();
            }
            catch (Exception ex)
            {
                BatchLogger.Error($"[PatchMainMenuReady]🔴 Error read EmbeddedFireRateData: {ex}");
            }
           
        }
    }
}
