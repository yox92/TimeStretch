using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using TimeStretch.AudioClipTools;
using TimeStretch.Cache;
using TimeStretch.Utils;
using UnityEngine;

namespace TimeStretch.Patches
{
    [HarmonyPatch(typeof(Player), "set_HandsController")]
    public class PatchTrackEquippedWeapon
    {
        private static Thread _workerThread;
        private static bool _isRunning;
        private static string _lastWeaponId;
        private static readonly Dictionary<int, object> LastHandsControllers = new();

        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            // 🔐 Define local player at start
            if (!__instance.IsYourPlayer)
            {
                BatchLogger.Log($"🛑 Skip not local player {__instance.IsYourPlayer}");
                return;
            }

            var hash = __instance.GetHashCode();
            var currentHands = __instance.HandsController;
            if (currentHands == null || !Plugin.EnableAudioMod.Value)
            {
                BatchLogger.Log($"🛑 Hand null {currentHands}");
                return;
            }

            lock (LastHandsControllers)
            {
                if (LastHandsControllers.TryGetValue(hash, out var last) && last == currentHands)
                {
                    BatchLogger.Log($"🛑 Skip This Instance already run{__instance.HandsController?.GetHashCode()}");
                    return;
                }

                LastHandsControllers[hash] = currentHands;
            }

            // Define local player at start 
            if (!__instance.IsYourPlayer)
                return;

            LocalPlayerReference.TryInitialize(__instance);
            // 🔐
            if (__instance?.HandsController == null || !Plugin.EnableAudioMod.Value)
                return;
            // 🔐
            if (!CacheObject.ActiveObservers.Add(__instance))
                return;
            // 🔐
            if (!Plugin.ShouldStopThreads)
                CoroutineRunner.Run(ObserveItemField(__instance));
        }

        private static IEnumerator ObserveItemField(Player player)
        {
            string id = null;
            try
            {
                var attempts = 0;
                var elapsed = 0f;
                const float timeoutPhase1 = 2f;
                const float timeoutPhase2 = 10f;

                while (elapsed < timeoutPhase1 + timeoutPhase2)
                {
                    // 🔐 Security
                    if (!player.HealthController.IsAlive)
                        yield break;

                    var hands = player.HandsController;
                    if (hands != null)
                    {
                        var handsType = hands.GetType();
                        var item0Field = AccessTools.Field(handsType, "item_0");

                        if (item0Field?.GetValue(hands) is Item item and Weapon weapon)
                        {
                            id = weapon.TemplateId.ToString();
                            if (id is not { Length: 24 })
                            {
                                BatchLogger.Error("⚠️ WeaponId invalide");
                                yield break;
                            }

                            // 🔐 Nouveau verrou ici
                            if (!CacheObject.ProcessingWeapons.Add(id))
                            {
                                BatchLogger.Warn($"🛑 Weapon '{id}' already being processed. Skip coroutine.");
                                yield break;
                            }

                            var alreadyProcessed = CacheObject.ProcessedWeapons.Contains(id);

                            if (id != _lastWeaponId)
                            {
                                _lastWeaponId = id;
                                BatchLogger.Log(
                                    $"🧹 [PatchTrackEquippedWeapon] Réinitialisation du cache AudioClip pour l'arme : {id}");
                                CacheObject.ClearLocalMappingsIfNewWeapon(id);
                            }

                            CacheObject.SetHookPermission(id, JsonCache.TryGetEntry(id, out var entry) && entry.Mod);

                            if (!CacheObject.IsHookAllowedForWeapon(id))
                            {
                                BatchLogger.Log(
                                    $"⛔ [PatchTrackEquippedWeapon] Hooks disabled for {id} (mod == false)");
                                yield break;
                            }

                            if (alreadyProcessed)
                                yield break;

                            BatchLogger.Log($"🧷 [PatchTrackEquippedWeapon] Weapon detected (delayed) : {id} ({weapon.LocalizedName()})");
                            EnqueueWeapon(id);
                            StartWorkerThreadIfNeeded();
                            yield break;
                        }
                    }

                    float waitTime = elapsed < timeoutPhase1 ? 0.1f : 1f;
                    yield return new WaitForSeconds(waitTime);
                    elapsed += waitTime;
                    attempts++;
                }

                BatchLogger.Warn("⚠️ Failed to detect weapon after several attempts.");
            }
            finally
            {
                CacheObject.ActiveObservers.Remove(player);
                if (id != null)
                    CacheObject.ProcessingWeapons.Remove(id);
            }
        }

        private static void EnqueueWeapon(string templateId)
        {
            lock (CacheObject.EnqueuedWeapons)
            {
                if (!CacheObject.EnqueuedWeapons.Add(templateId)) return;
            }

            if (!CacheObject.ProcessedWeapons.Contains(templateId))
            {
                CacheObject.WeaponQueue.Enqueue(templateId);
                BatchLogger.Log($"📥 [PatchTrackEquippedWeapon] WeaponId enqueued : {templateId}");
            }
        }

        private static void StartWorkerThreadIfNeeded()
        {
            if (_isRunning) return;
            _isRunning = true;

            _workerThread = new Thread(ObserveAndTransform)
            {
                IsBackground = true
            };
            _workerThread.Start();
        }

        private static void ObserveAndTransform()
        {
            if (Plugin.ShouldStopThreads)
            {
                BatchLogger.Log("🛑 [PatchTrackEquippedWeapon] Cancelled because EnableAudioMod = false");
                _isRunning = false;
                return;
            }

            try
            {
                while (CacheObject.WeaponQueue.TryDequeue(out var weaponId))
                {
                    if (!JsonCache.IsInitialized)
                    {
                        BatchLogger.Warn("[PatchTrackEquippedWeapon] ⚠️ FireRateDataStore not ready");
                        continue;
                    }

                    if (!CacheObject.ProcessedWeapons.Add(weaponId)) continue;

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var log = new List<string>();
                    var clipsToTransform = AudioClipModifier.GetTransformableClips(weaponId);
                    if (clipsToTransform.Count == 0)
                    {
                        BatchLogger.Log(
                            $"⛔ No transformable clips found for {weaponId}. Audio hooks disabled for this weapon.");
                        CacheObject.SetHookPermission(weaponId, false);
                        _isRunning = false;
                        return;
                    }

                    var tasks = new List<Task>();
                    var clipNames = new List<string>();

                    foreach (var (clip, tempo) in clipsToTransform)
                    {
                        clipNames.Add(clip.name);
                        var task = AudioClipModifier.TransformClip(clip, tempo, log, weaponId);
                        tasks.Add(task);
                    }

                    Task.WhenAll(tasks).ContinueWith(_ =>
                    {
                        stopwatch.Stop();

                        if (clipNames.Count > 0)
                        {
                            log.Add(
                                $"🧾 Summary: {clipNames.Count} clip(s) transformed for {weaponId} in {stopwatch.ElapsedMilliseconds} ms:");
                            foreach (var name in clipNames)
                                log.Add($"    🔊 {name}");
                        }
                        else
                        {
                            log.Add($"🧾 No clips were transformed for {weaponId}.");
                        }

                        BatchLogger.FlushReplacementLog(log, $"[PatchTrackEquippedWeapon] Final result: {weaponId}");
                        CoroutineRunner.Run(AudioClipModifier.OneShotClipAvailability(clipNames));
                        _isRunning = false;
                    });

                    return;
                }
            }
            catch (Exception ex)
            {
                BatchLogger.Log($"❌ Exception dans thread audio : {ex}");
                _isRunning = false;
            }
        }
    }
}