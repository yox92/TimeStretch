using HarmonyLib;
using TimeStretch.Animation;
using TimeStretch.Cache;
using TimeStretch.Utils;

namespace TimeStretch.Patches;

public abstract class PatchHandOut
{
    [HarmonyPatch(typeof(GClass734),"smethod_38")]
    public static class WeaponOut
    {
        /// <summary>
        /// Handles weapon unequip event by stopping animations and clearing caches
        /// </summary>
        private static void Postfix()
        {
            BatchLogger.Log("[WeaponOut] 🖐 Weapon OUT");
            AnimationOverClock.Stop();
            CacheObject.ClearFireModeCache();
            CacheObject.ClearAllClipsByNameOverClock(); 
        }
    }
}