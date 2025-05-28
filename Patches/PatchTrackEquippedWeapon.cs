using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using TimeStretch.Animation;
using TimeStretch.AudioClipTools;
using TimeStretch.Cache;
using TimeStretch.Entity;
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
            CoroutineRunner.Run(DelayedTryInitializeOverClock(__instance));

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
            string currentWeaponId = null;
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
                    {
                        AnimationOverClock.Stop();
                        yield break;
                    }

                    var hands = player.HandsController;
                    if (hands != null)
                    {
                        var handsType = hands.GetType();
                        var item0Field = AccessTools.Field(handsType, "item_0");

                        if (item0Field?.GetValue(hands) is Item item and Weapon weapon)
                        {
                            currentWeaponId = weapon.TemplateId.ToString();
                            if (currentWeaponId is not { Length: 24 })
                            {
                                BatchLogger.Error("⚠️ WeaponId invalide");
                                yield break;
                            }
                            CacheObject.SetWeaponIdOnHand(weapon.TemplateId);
                            CacheObject.SetWeaponFireModeOnHand(weapon.Template.weapFireType);
                            
                            // 🔐 Nouveau verrou
                            if (!CacheObject.ProcessingWeapons.Add(currentWeaponId))
                            {
                                BatchLogger.Warn($"🛑 Weapon '{currentWeaponId}' already being processed. Skip coroutine.");
                                yield break;
                            }

                            var alreadyProcessed = CacheObject.ProcessedWeapons.Contains(currentWeaponId);

                            if (currentWeaponId != _lastWeaponId)
                            {
                                _lastWeaponId = currentWeaponId;
                                BatchLogger.Log(
                                    $"🧹 [PatchTrackEquippedWeapon] Réinitialisation du cache AudioClip pour l'arme : {currentWeaponId}");
                                CacheObject.ClearLocalMappingsIfNewWeapon(currentWeaponId);
                            }

                            CacheObject.SetHookPermission(currentWeaponId, JsonCache.TryGetEntry(currentWeaponId, out var entry) && entry.Mod);

                            if (!CacheObject.IsHookAllowedForWeapon(currentWeaponId))
                            {
                                BatchLogger.Log(
                                    $"⛔ [PatchTrackEquippedWeapon] Hooks disabled for {currentWeaponId} (mod == false)");
                                yield break;
                            }

                            if (alreadyProcessed)
                                yield break;

                            BatchLogger.Log($"🧷 [PatchTrackEquippedWeapon] Weapon detected (delayed) : {currentWeaponId} ({weapon.LocalizedName()})");
                            EnqueueWeapon(currentWeaponId);
                            StartWorkerThreadIfNeeded();
                            yield break;
                        }
                    }

                    var waitTime = elapsed < timeoutPhase1 ? 0.1f : 1f;
                    yield return new WaitForSeconds(waitTime);
                    elapsed += waitTime;
                    attempts++;
                }

                BatchLogger.Warn("⚠️ Failed to detect weapon after several attempts.");
            }
            finally
            {
                CacheObject.ActiveObservers.Remove(player);
                if (currentWeaponId != null)
                    CacheObject.ProcessingWeapons.Remove(currentWeaponId);
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
                    var clipsToTransform = AudioClipModifier.GetTransformableClips(weaponId, CallerType.WeaponTrack);
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
                        var task = AudioClipModifier.TransformClip(clip, tempo, log, weaponId, CallerType.WeaponTrack);
                        tasks.Add(task);
                    }

                    Task.WhenAll(tasks).ContinueWith(_ =>
                    {
                        stopwatch.Stop();

                        if (clipNames.Count > 0)
                        {
                            log.Add($"🧾 Summary: {clipNames.Count} clip(s) transformed for {weaponId} in {stopwatch.ElapsedMilliseconds} ms:");
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

        private static IEnumerator DelayedTryInitializeOverClock(Player player)
        {
            yield return new WaitForSeconds(1.0f);
            if (player == null || player.HandsController == null || !player.HealthController.IsAlive)
            {
                BatchLogger.Log("❌ TryInitializeOverClock: Player, HandsController is null or player is not alive");
                yield break;
            }

            if (player.HandsController.Item is Weapon weapon)
            {
                if (string.IsNullOrEmpty(weapon.TemplateId) || weapon.TemplateId.ToString() is not { Length: 24 })
                {
                    BatchLogger.Log("❌ TryInitializeOverClock: Invalid TemplateId");
                    yield break;
                }

                var originaleFireRateMod = JsonCache.GetModOriginalFireRate(weapon.TemplateId);
                if (originaleFireRateMod is > 300 and < 1500)
                {
                    if (!CacheObject.TryGetFireRate(weapon.TemplateId, out _))
                    {
                        CacheObject.RegisterFireRate(weapon.TemplateId, originaleFireRateMod);
                        BatchLogger.Log($"✅ [TryInitializeOverClock] First time: FireRate cached {weapon.TemplateId} = {originaleFireRateMod} RPM");
                    }
                    else
                    {
                        BatchLogger.Log($"ℹ️ [TryInitializeOverClock] FireRate already known for {weapon.TemplateId}, no overwrite.");
                    }

                    AnimationOverClock.Initialize();
                    BatchLogger.Log($"✅ [TryInitializeOverClock] AnimationOverClock initialized for {weapon.TemplateId} with bFirerate {originaleFireRateMod}");
                }
                else
                {
                    BatchLogger.Log($"❌ TryInitializeOverClock: Invalid bFirerate {originaleFireRateMod}");
                }
            }
            else
            {
                BatchLogger.Log("❌ TryInitializeOverClock: Item is not a Weapon");
            }
        }
    }
}