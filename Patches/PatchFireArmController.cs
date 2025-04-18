using System;
using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using TimeStretch.Cache;
using TimeStretch.Utils;

namespace TimeStretch.Patches;

public class PatchFireArmController
{
    [HarmonyPatch(typeof(Player.FirearmController), "method_59")]
    public static class PatchFirearmControllerMethod59
    {
        private static readonly AccessTools.FieldRef<Player.FirearmController, Player> PlayerRef =
            AccessTools.FieldRefAccess<Player.FirearmController, Player>("_player");

        // Dictionary to cache FirearmController → Player (thread-safe)
        private static readonly Dictionary<Player.FirearmController, Player> PlayerByController = new();
        private static readonly object Lock = new();

        [HarmonyPrefix]
        public static void Prefix(Player.FirearmController __instance)
        {
            BatchLogger.Log($"✅ [PatchFireArmController] Prefix called");
            BatchLogger.Log(
                $"[PatchFireArmController] LocalPlayerReference.IsInitialized: {LocalPlayerReference.IsInitialized}");

            if (!LocalPlayerReference.IsInitialized)
            {
                BatchLogger.Log($"❌ [PatchFireArmController] LocalPlayerReference not initialized, skipping.");
                return;
            }

            Player player = null;
            lock (Lock)
            {
                if (PlayerByController.TryGetValue(__instance, out var cached))
                {
                    player = cached;
                    BatchLogger.Log($"[PatchFireArmController] ✅ Player found in cache: {player.ProfileId}");
                }
                else
                {
                    try
                    {
                        player = PlayerRef(__instance);
                        if (player != null)
                        {
                            PlayerByController[__instance] = player;
                            BatchLogger.Log(
                                $"[PatchFireArmController] 🧠 Player resolved via FieldRef: {player.ProfileId}");
                        }
                        else
                        {
                            BatchLogger.Log($"❌ [PatchFireArmController] Failed to resolve Player via FieldRef.");
                        }
                    }
                    catch (Exception ex)
                    {
                        BatchLogger.Log($"❌ [PatchFireArmController] Exception accessing PlayerRef: {ex}");
                        return;
                    }
                }
            }

            if (player == null)
            {
                BatchLogger.Log($"❌ [PatchFireArmController] Player is null, aborting.");
                return;
            }

            if (!LocalPlayerReference.IsLocalPlayer(player))
            {
                BatchLogger.Log($"❌ [PatchFireArmController] Player is NOT local ({player.ProfileId}), skipping.");
                return;
            }

            if (!Plugin.EnableAudioMod.Value)
            {
                BatchLogger.Log($"[PatchFireArmController] Audio mod disabled, skipping.");
                return;
            }

            if (__instance.Item is not Weapon weapon)
            {
                BatchLogger.Log($"❌ [PatchFireArmController] No weapon equipped.");
                return;
            }

            // Prepare fire context
            PatchPickByDistance.InWeaponFire = false;
            PatchPickByDistance.CurrentWeapon = null;

            try
            {
                PatchPickByDistance.InWeaponFire = true;
                PatchPickByDistance.CurrentWeapon = weapon;

                CacheObject.TryLogOnce(
                    $"[PatchFireArmController] Local fire: [method_59] WeaponId={weapon.TemplateId}, {weapon.LocalizedName()}");
            }
            catch (Exception ex)
            {
                BatchLogger.Error($"[PatchFireArmController] Exception during fire context setup: {ex}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            PatchPickByDistance.InWeaponFire = false;
            PatchPickByDistance.CurrentWeapon = null;
        }
    }
}