using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using HarmonyLib;
using TimeStretch.Utils;

namespace TimeStretch.Patches;

public class PatchFireBullet
{
    [HarmonyPatch(typeof(WeaponSoundPlayer), nameof(WeaponSoundPlayer.FireBullet))]
    public static class PatchFireBulletPlayerTracker
    {
        // Juste On Time 
        private static readonly FieldInfo BridgeField = typeof(WeaponSoundPlayer)
            .GetField("playersBridge", BindingFlags.NonPublic | BindingFlags.Instance);

        // Dictionnary to cache bind WeaponSoundPlayer -> IPlayer
        private static readonly Dictionary<WeaponSoundPlayer, IPlayer> PlayerBySoundPlayer = new();
        private static readonly object Lock = new();

        [HarmonyPrefix]
        public static void Prefix(WeaponSoundPlayer __instance)
        {
            BatchLogger.Log("🧩 [PatchFireBullet] Prefix called.");
            try
            {
                IPlayer player;
                // use cache
                lock (Lock)
                {
                    if (PlayerBySoundPlayer.TryGetValue(__instance, out var cachedPlayer))
                    {
                        player = cachedPlayer;
                        BatchLogger.Log($"📦 [PatchFireBullet] Player trouvé dans le cache: {player.ProfileId}");
                    }
                    else
                    {
                        // No cache : go reflection
                        var bridge = BridgeField?.GetValue(__instance) as BaseSoundPlayer.IObserverToPlayerBridge;
                        if (bridge == null)
                        {
                            BatchLogger.Log($"❌ [PatchFireBullet] bridge null (BridgeField={BridgeField != null})");
                        }

                        player = bridge?.iPlayer;

                        if (player != null)
                        {
                            PlayerBySoundPlayer[__instance] = player;
                            BatchLogger.Log($"🧠 [PatchFireBullet] Player récupéré via Bridge : {player.ProfileId}");
                        }
                        else
                        {
                            BatchLogger.Log("❌ [PatchFireBullet] Aucun player trouvé via Bridge.");
                        }
                    }
                }

                var isLocal = LocalPlayerReference.IsLocalPlayer(player);
                PatchPickByDistance.IsLocalPlayerSound = isLocal;
                BatchLogger.Log(
                    $"🎧 [PatchFireBullet] {(isLocal ? "✅ Joueur local détecté" : "❌ Pas un joueur local")} (player null = {player == null})");
            }
            catch (Exception ex)
            {
                PatchPickByDistance.IsLocalPlayerSound = false;
                BatchLogger.Warn($"[PatchFireBullet] Exception dans Prefix: {ex.Message}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            // Après FireBullet => on remet le flag à false
            PatchPickByDistance.IsLocalPlayerSound = false;
        }
    }
}