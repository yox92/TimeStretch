using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using TimeStretch.Entity;
using TimeStretch.Utils;

namespace TimeStretch.Boot;

[HarmonyPatch(typeof(GClass1337), nameof(GClass1337.Init))]
public class PatchGClass1337
{
    [HarmonyPostfix]
    public static void Postfix(GClass1337 __instance)
    {
        foreach (var item in __instance.Values)
        {
            // Only 🔫
            if (item is not WeaponTemplate weapon)
                continue;

            if (weapon.bFirerate <= 300)
                continue;

            if (!CacheObject.WeaponFireRates.TryAdd(weapon._id, weapon.bFirerate))
                continue;

            BatchLogger.Log($"📦 [Init] Weapons catch : {weapon._id} → {weapon.bFirerate} RPM ({weapon._name})");
        }
    }
}