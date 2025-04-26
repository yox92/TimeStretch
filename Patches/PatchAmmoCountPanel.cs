using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;

namespace TimeStretch.Patches;


public abstract class PatchAmmoCountPanel 
{
    /// <summary>
    /// Handles the HUD interface for displaying weapon fire mode in the ammo count panel.
    /// Intercepts and modifies the display when fire mode is set to "OverClock" mode (8).
    /// </summary>
    [HarmonyPatch(typeof(AmmoCountPanel), nameof(AmmoCountPanel.ShowFireMode))]
    public static class PatchHUDShowFireMode
    {
        static bool Prefix(ref Weapon.EFireMode fireMode, AmmoCountPanel __instance)
        {
            if ((int)fireMode != 8) 
                return true;
            __instance.Show("OverClock");
            return false;
        }
    }}