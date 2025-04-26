using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using TimeStretch.Animation;
using TimeStretch.Cache;
using TimeStretch.Utils;

namespace TimeStretch.Patches
{
    [HarmonyPatch]
    public class PatchChangeFireModeDynamicRedirect
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("EFT.Player+FirearmController+GClass1806");
            BatchLogger.Info($" TargetMethod: Resolved type: {type?.FullName}");
            var method = AccessTools.Method(type, "ChangeFireMode");
            BatchLogger.Info($" TargetMethod: Resolved method: {method?.Name}");
            return method;
        }

        static bool Prefix(object __instance, ref bool __result, ref Weapon.EFireMode fireMode)
        {
            var log = new List<string>();
            log.Add(" Prefix: Invoked for ChangeFireMode.");
        
            var weapon = InitializeWeapon(__instance, log);
            if (weapon == null)
            {
                BatchLogger.Block(log);
                return true;
            }
        
            var modes = weapon.Template.weapFireType.ToList();
            const Weapon.EFireMode overclock = (Weapon.EFireMode)0x08;
        
            if (!modes.Contains(overclock))
            {
                modes.Add(overclock);
                weapon.Template.weapFireType = modes.ToArray();
                log.Add("⚙️ Overclock mode injected into weaponFireType.");
            }
        
            var weaponId = weapon.Template._id;
            var fireRateOriginal = JsonCache.GetOriginalFireRate(weaponId);
            var fireRateOverClock = CacheObject.TryGetFireRate(weaponId,
                out var fireRate) ?
                fireRate : fireRateOriginal;
        
            switch (weapon.SelectedFireMode)
            {
                case Weapon.EFireMode.fullauto:
                    fireMode = overclock;
                    weapon.Template.bFirerate = fireRateOverClock;
                    AnimationOverClock.Initialize();
                    HandleFireModeChange(weaponId, overclock.ToString(), $"🚀 Overclock mode activated for {weaponId} with fire rate: {fireRateOverClock} RPM.", log);
                    break;
        
                case overclock:
                    fireMode = Weapon.EFireMode.single;
                    weapon.Template.bFirerate = fireRateOriginal;
                    HandleFireModeChange(weaponId, nameof(Weapon.EFireMode.single), "🔄 Overclock mode deactivated, returning to Single mode.", log);
                    break;
        
                case Weapon.EFireMode.single:
                    HandleFireModeChange(weaponId, nameof(Weapon.EFireMode.single), "Current fire mode: Single.", log);
                    break;
        
                case Weapon.EFireMode.doublet:
                    HandleFireModeChange(weaponId, nameof(Weapon.EFireMode.doublet), "Current fire mode: Doublet.", log);
                    break;
        
                case Weapon.EFireMode.burst:
                    HandleFireModeChange(weaponId, nameof(Weapon.EFireMode.burst), "Current fire mode: Burst.", log);
                    break;
        
                case Weapon.EFireMode.doubleaction:
                    HandleFireModeChange(weaponId, nameof(Weapon.EFireMode.doubleaction), "Current fire mode: Double Action.", log);
                    break;
        
                case Weapon.EFireMode.semiauto:
                    HandleFireModeChange(weaponId, nameof(Weapon.EFireMode.semiauto), "Current fire mode: Semi-auto.", log);
                    break;
        
                default:
                    HandleFireModeChange(weaponId, weapon.SelectedFireMode.ToString(), $"⚠️ Unknown fire mode detected: {weapon.SelectedFireMode}", log);
                    break;
            }
            BatchLogger.Block(log);
        
            return true;
        }
        
        private static Weapon InitializeWeapon(object instance, List<string> log)
        {
            var weaponField = AccessTools.Field(instance.GetType(), "weapon_0");
            if (weaponField == null)
            {
                log.Add("⚠️ weaponField is null. Skipping logic.");
                return null;
            }
        
            if (weaponField.GetValue(instance) is not Weapon weapon)
            {
                log.Add("⚠️ Weapon instance not found or null. Skipping logic.");
                return null;
            }
        
            if (weapon.Template == null)
            {
                log.Add("⚠️ Weapon template is null. Skipping logic.");
                return null;
            }
        
            return weapon;
        }
        
        private static void HandleFireModeChange(string weaponId, string fireMode, string logMessage, List<string> log)
        {
            CacheObject.RegisterFireMode(weaponId, fireMode);
            log.Add(logMessage);
        }
    }
}