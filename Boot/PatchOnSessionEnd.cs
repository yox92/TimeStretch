using EFT;
using HarmonyLib;
using TimeStretch.Entity;

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