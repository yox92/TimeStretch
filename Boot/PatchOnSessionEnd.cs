using EFT;
using HarmonyLib;
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
        }
    }
}