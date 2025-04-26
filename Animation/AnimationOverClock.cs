using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeStretch.AudioClipTools;
using UnityEngine;
using TimeStretch.Utils;
using TimeStretch.Cache;
using TimeStretch.Entity;

namespace TimeStretch.Animation
{
    public class AnimationOverClock : MonoBehaviour
    {
        private static int _overClockFireRate = 500;
        private static bool _isActive;
        private static readonly object Lock = new object();
        
        private static AnimationOverClock _instance;
        private static Coroutine _delayCoroutine;
        private const float DelayBeforeTransform = 1f;

        private static bool _isRunning;

        public static void Initialize()
        {
            if (_isActive) return;

            lock (Lock)
            {
                if (_isActive) return;

                if (_instance == null)
                {
                    var obj = new GameObject("AnimationOverClock");
                    DontDestroyOnLoad(obj);
                    _instance = obj.AddComponent<AnimationOverClock>();
                }

                _isActive = true;

                BatchLogger.Info("[AnimationOverClock] Instance successfully initialized.");
            }
        }

        public static void Stop()
        {
            lock (Lock)
            {
                if (!_isActive || _instance == null)
                    return;
                if (_delayCoroutine != null)
                {
                    _instance.StopCoroutine(_delayCoroutine);
                    _delayCoroutine = null;
                }

                Destroy(_instance.gameObject);
                _instance = null;

                _isActive = false;
                BatchLogger.Info("[AnimationOverClock] Component correctly stopped.");
            }
        }

        /// <summary>
        /// Initializes the AnimationOverClock component on awakening.
        /// Sets up instance reference and checks for equipped weapon.
        /// If a weapon is equipped and has a cached fire rate, initializes
        /// with that rate.
        /// </summary>
        private void Awake()
        {
            // Ensure that the instance is correctly defined
            _instance = this;
            
            // Register component initialization
            BatchLogger.Info("[AnimationOverClock] Component initialized");
            BatchLogger.Info("[AnimationOverClock] Searching for equipped weapon...");

            // Check if a weapon is equipped
            var currentWeaponId = CacheObject.GetWeaponIdOnHand();
            BatchLogger.Info(string.IsNullOrEmpty(currentWeaponId)
                ? "[AnimationOverClock] No equipped weapon detected."
                : $"[AnimationOverClock] Equipped weapon found: {currentWeaponId}");

            // If a weapon is equipped and has a cached fire rate, use it
            if (string.IsNullOrEmpty(currentWeaponId) || !CacheObject.TryGetFireRate(currentWeaponId,
                    out var cachedFireRate))
                return;

            _overClockFireRate = cachedFireRate;
            BatchLogger.Info(
                $"[AnimationOverClock] Initialization with cached fire rate: {_overClockFireRate} RPM for {currentWeaponId}");
        }
        /// <summary>
        /// Updates the fire rate based on keyboard input.
        /// Checks for increase/decrease via configurable bindings and alternative shortcut.
        /// </summary>
        private void Update()
        {
            var weaponId = CacheObject.GetWeaponIdOnHand();
            if (string.IsNullOrEmpty(weaponId))
                return;

            if (Plugin.KeyboardBindingUp.Value.IsDown())
            {
                OverClockUtils.IncreaseFireRate(weaponId, ref _overClockFireRate);
                ApplyFireModeChange(weaponId, _overClockFireRate);
                BatchLogger.Info($"[AnimationOverClock] Fire rate increased: {_overClockFireRate} RPM");
                return;
            }

            if (Plugin.KeyboardBindingDown.Value.IsDown())
            {
                OverClockUtils.DecreaseFireRate(weaponId, ref _overClockFireRate);
                ApplyFireModeChange(weaponId, _overClockFireRate);
                BatchLogger.Info($"[AnimationOverClock] Fire rate decreased: {_overClockFireRate} RPM");
                return;
            }

            // Alternative shortcut that uses Ctrl+B
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.B))
            {
                OverClockUtils.IncreaseFireRate(weaponId, ref _overClockFireRate);
                ApplyFireModeChange(weaponId, _overClockFireRate);
                BatchLogger.Info($"[AnimationOverClock] New fire rate: {_overClockFireRate} RPM");
            }
        }
        /// <summary>
        /// Applies the fire rate change to the current weapon
        /// </summary>
        private static void ApplyFireModeChange(string weaponId, int overClockFireRate)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                BatchLogger.Warn("[AnimationOverClock] No equipped weapon, unable to apply change");
                return;
            }

            if (!OverClockUtils.ApplyFirearmAnimationMode())
                return;

            // Enregistre la nouvelle cadence de tir dans le cache
            CacheObject.RegisterFireRate(weaponId, overClockFireRate);
            BatchLogger.Info($"[AnimationOverClock] Fire rate updated: {overClockFireRate} RPM for {weaponId}");

            // Nettoie le cache pour s'assurer que les transformations précédentes ne causent pas de problèmes
            CacheObject.ClearFireModeCache();
            BatchLogger.Info($"[AnimationOverClock] Cleaning FireModeCache");

            CleanupPreviousCoroutine();
            if (!_instance)
                return;

            StartDelayedAudioTransformation(weaponId);
        }

        /// <summary>
        /// Starts a coroutine that will transform weapon audio clips after a delay
        /// </summary>
        /// <param name="weaponId">ID of the weapon whose sounds need to be transformed</param>
        private static void StartDelayedAudioTransformation(string weaponId)
        {
            _delayCoroutine = _instance.StartCoroutine(DelayedAudioTransformCoroutine(weaponId));
            BatchLogger.Info($"[AnimationOverClock] Starting transformation delay for {weaponId}");
        }

        /// <summary>
        /// Stops and cleans up the previous coroutine if it exists.
        /// Helps avoid conflicts when changing fire rate.
        /// </summary>
        private static void CleanupPreviousCoroutine()
        {
            if (_delayCoroutine == null || !_instance)
                return;
            _instance.StopCoroutine(_delayCoroutine);
            _delayCoroutine = null;
        }

        /// <summary>
        /// Coroutine that waits for a certain time before transforming audio clips
        /// </summary>
        private static IEnumerator DelayedAudioTransformCoroutine(string weaponId)
        {
            yield return new WaitForSeconds(DelayBeforeTransform);

            BatchLogger.Info($"[AnimationOverClock] Delay elapsed, starting transformation for {weaponId}");
            EnqueueForAudioTransformation(weaponId);
            StartWorkerThreadIfNeeded();

            _delayCoroutine = null;
        }

        /// <summary>
        /// Starts the audio clip transformation thread if needed
        /// </summary>
        private static void StartWorkerThreadIfNeeded()
        {
            if (_isRunning) return;

            _isRunning = true;
            Task.Run(AudioTransformWorker);
        }

        /// <summary>
        /// Transforms audio clips in a background thread
        /// </summary>
        private static async Task AudioTransformWorker()
        {
            try
            {
                while (true)
                {
                    string weaponId;
                    lock (CacheObject.FireModeWeaponQueue)
                    {
                        if (CacheObject.FireModeWeaponQueue.Count == 0) break;
                        weaponId = CacheObject.FireModeWeaponQueue.Dequeue();
                    }

                    // Ignore already processed weapons
                    lock (CacheObject.FireModeProcessedWeapons)
                    {
                        if (!CacheObject.FireModeProcessedWeapons.Add(weaponId))
                            continue;
                    }

                    if (!JsonCache.IsInitialized)
                    {
                        BatchLogger.Warn("[AnimationOverClock] ⚠️ JSON Cache not initialized.");
                        continue;
                    }

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var log = new List<string>();

                    var clipsToTransform =
                        AudioClipModifier.GetTransformableClips(weaponId, CallerType.Overclock);
                    if (clipsToTransform.Count == 0)
                    {
                        BatchLogger.Warn(
                            $"[AnimationOverClock] No transformable clips found for {weaponId}. Skipping.");
                        continue;
                    }

                    var transformTasks = clipsToTransform
                        .Select(pair =>
                            AudioClipModifier.TransformClip(pair.clip, pair.tempo, log, weaponId, CallerType.Overclock))
                        .ToArray();

                    await Task.WhenAll(transformTasks);

                    stopwatch.Stop();
                    log.Add(
                        $"[AnimationOverClock] {clipsToTransform.Count} clip(s) transformed for {weaponId} in {stopwatch.ElapsedMilliseconds} ms."
                    );
                    BatchLogger.Block(log);
                }
            }
            catch (Exception ex)
            {
                BatchLogger.Error($"[AnimationOverClock] ❌ Exception in audio worker thread: {ex}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Adds a weapon to the queue for audio transformation
        /// </summary>
        private static void EnqueueForAudioTransformation(string weaponId)
        {
            lock (CacheObject.FireModeEnqueuedWeapons)
            {
                if (!CacheObject.FireModeEnqueuedWeapons.Add(weaponId))
                {
                    BatchLogger.Warn(
                        $"[AnimationOverClock] Weapon '{weaponId}' already enqueued for processing."
                    );
                    return;
                }
            }

            lock (CacheObject.FireModeWeaponQueue)
            {
                CacheObject.FireModeWeaponQueue.Enqueue(weaponId);
            }

            BatchLogger.Info($"[AnimationOverClock] Weapon enqueued for transformation: {weaponId}");
        }
    }
}