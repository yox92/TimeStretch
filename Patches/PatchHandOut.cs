using System.Collections.Generic;
using System.Reflection;
using AnimationEventSystem;
using EFT;
using HarmonyLib;
using TimeStretch.Animation;
using TimeStretch.Utils;

namespace TimeStretch.Patches;

public abstract class PatchHandOut
{
    [HarmonyPatch(typeof(GClass734), "smethod_38")]
    public static class WeaponOut
    {
        /// <summary>
        /// Handles weapon unequip event by stopping animations and clearing caches
        /// </summary>
        private static readonly FieldInfo BridgeField =
            typeof(WeaponSoundPlayer).GetField("playersBridge", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void Postfix(List<IActorEvents> eventsConsumers, AnimationEventParameter parameter)
        {
            foreach (var consumer in eventsConsumers)
            {
                BatchLogger.Log($"[WeaponOut] Checking consumer: {consumer} ({consumer.GetType().Name})");

                if (consumer is not WeaponSoundPlayer weaponSoundPlayer)
                    continue;
                var bridge = BridgeField?.GetValue(weaponSoundPlayer) as BaseSoundPlayer.IObserverToPlayerBridge;
                var player = bridge?.iPlayer;
                BatchLogger.Log($"[WeaponOut] Player retrieved: {player != null}");

                if (player is Player { IsYourPlayer: true })
                {
                    BatchLogger.Log("[WeaponOut] 🖐 Weapon OUT (local player) disabled overclock");
                    AnimationOverClock.Stop();
                    break;
                }
                else
                {
                    BatchLogger.Log("[WeaponOut] Not local player, skipping");
                }
            }
        }
    }
}