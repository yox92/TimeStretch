using HarmonyLib;

namespace TimeStretch.Patches
{
    /// <summary>
    /// Patch managing the weapon fire mode display in the HUD interface from the asset bundle
    /// Converts mode "8" to "OC" (OverClock) in the UI weapons
    /// </summary>
    [HarmonyPatch(typeof(EFT.InventoryLogic.Weapon), "method_31")]
    public class PatchFireModeTextDisplay
    {
        static void Postfix(ref string __result)
        {
            if (__result.Contains("8"))
            {
                __result = __result.Replace("8", "OC");
            }
        }
    }
}