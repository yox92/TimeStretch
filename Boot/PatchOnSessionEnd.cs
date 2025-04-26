using EFT;
using HarmonyLib;
using TimeStretch.Animation;
using TimeStretch.Cache;

namespace TimeStretch.Boot
{
    [HarmonyPatch(typeof(Player), "OnGameSessionEnd")]
    public class PatchClearCacheOnSessionEnd
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            CacheObject.ClearAllCache();
            AnimationOverClock.Stop();
        }
    }
}