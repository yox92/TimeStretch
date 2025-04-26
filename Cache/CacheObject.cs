using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EFT;
using TimeStretch.Entity;
using TimeStretch.Utils;
using UnityEngine;

namespace TimeStretch.Cache
{
    /// Manages the global cache of objects used by the mod (AudioClips, Weapons, etc.).
    public static class CacheObject
    {
        /// Singleton coroutine to continuously monitor transformed AudioClips (debug use).
        private static Coroutine _trackingCoroutine;

        /// Starts the clip-tracking coroutine (only once).
        public static void StartTrackingCoroutine(MonoBehaviour context)
        {
            if (_trackingCoroutine != null)
                return;

            if (context != null)
            {
                _trackingCoroutine = context.StartCoroutine(TrackTransformedClipsCoroutine());
                BatchLogger.Log("🌀 Coroutine de suivi des clips transformés démarrée.");
            }
        }

        /// Coroutine that logs the number of transformed and visible clips in memory every few seconds.
        /// Useful for debugging (not required for core functionality).
        private static IEnumerator TrackTransformedClipsCoroutine()
        {
            var wait = new WaitForSeconds(1f);

            while (true)
            {
                int total = 0, visible = 0;

                foreach (var kvp in Transformed)
                {
                    var transformed = kvp.Value;
                    if (transformed == null) continue;

                    total++;
                    if (transformed.hideFlags == HideFlags.None)
                        visible++;
                }

                BatchLogger.Log(
                    $"🎧 [CacheObject] Suivi clips transformés : {visible}/{total} visibles en mémoire (multi-armes)");
                yield return wait;
            }
        }

        /// Suffix added to the name of a transformed AudioClip.
        /// Example: "ak47_fire" → "ak47_fire_mod"
        private const string Suffix = "_mod";
        
        /// <summary>
        /// Dictionary storing transformed audio clips for normal weapon firing.
        /// Key: Tuple of weapon ID and original audio clip.
        /// Value: Transformed audio clip with modified pitch/duration.
        /// </summary>
        private static readonly ConcurrentDictionary<(string weaponId, AudioClip), AudioClip> Transformed = new();

        /// <summary>
        /// Dictionary storing transformed audio clips for overclock weapon firing mode.
        /// Key: Tuple of weapon ID and original audio clip.
        /// Value: Transformed audio clip with modified pitch/duration for increased fire rate.
        /// </summary>
        private static readonly ConcurrentDictionary<(string weaponId, AudioClip), AudioClip> TransformedOverclock = new();
        /// Acts as a mapping between original AudioClip names and their transformed counterparts.
        /// Used to efficiently resolve connections between original and processed clips within the local scope.
        private static readonly Dictionary<string, string> LocalClipMap = new();
        
        /// Fast cache Overclock only
        private static readonly Dictionary<string, string> LocalClipMapOverClok = new();

        /// Global thread-safe dictionary mapping clip names to AudioClips in memory.
        private static readonly ConcurrentDictionary<string, AudioClip> AllClipsByName = new();

        /// Global thread-safe dictionary mapping clip names to AudioClips in memory.
        private static readonly ConcurrentDictionary<string, AudioClip> AllClipsByNameOverClock = new();

        /// Dictionary mapping weaponId to a boolean indicating if audio hooks are allowed.
        private static readonly Dictionary<string, bool> HookPermissionByWeaponId = new();

        /// Allows logging a message only once per key to avoid console spam.
        private static readonly HashSet<string> LoggedKeys = [];

        /// Lock object used to guard access to LoggedKeys (HashSet).
        private static readonly object LogLock = new();

        /// Lock Processing by Weapons
        public static readonly HashSet<string> ProcessingWeapons = new();


        // =====================================================================
        // WEAPON TRACKING
        // =====================================================================
        /// Thread-safe FIFO queue of weaponIds to process for audio transformation.
        public static readonly ConcurrentQueue<string> WeaponQueue = new();
        
        public static readonly Queue<string> FireModeWeaponQueue = new();
        
        /// Set of weaponIds already processed to prevent redundant transformations.
        /// <summary>
        /// Armes déjà traitées par l'analyseur audio pour WeaponTrack
        /// </summary>
        public static readonly HashSet<string> ProcessedWeapons = new();
        
        /// <summary>
        /// Armes déjà traitées par la transformation audio (utilisé par AnimationOverClock)
        /// </summary>
        public static readonly HashSet<string> FireModeProcessedWeapons = new();
        
        /// <summary>
        /// Armes déjà ajoutées à la file d'attente de traitement pour le suivi des armes
        /// </summary>
        public static readonly HashSet<string> EnqueuedWeapons = new();
        
        /// <summary>
        /// Armes déjà ajoutées à la file d'attente pour transformation audio
        /// </summary>
        public static readonly HashSet<string> FireModeEnqueuedWeapons = new();
        
        /// <summary>
        /// Joueurs actuellement suivis (via coroutine) pour éviter les doublons
        /// </summary>
        public static readonly HashSet<Player> ActiveObservers = new();
        
        /// <summary>
        /// FireRate / IDTemplate récupérés au démarrage
        /// </summary>
        public static readonly ConcurrentDictionary<string, float> WeaponFireRates = new();
        
        /// <summary>
        /// Dictionnaire associant un ID d'arme à son mode de tir actuel
        /// </summary>
        /// Key: weaponId, Value: fire mode.
        private static readonly Dictionary<string, string> FireModeWeapons = new();
        
        /// <summary>
        /// Dictionary storing weapon fire rates during Overclock mode.
        /// Key: weaponId, Value: rounds per minute (RPM) when overclocked
        /// </summary>
        private static readonly Dictionary<string, int> FireRateWeapons = new();
        
        /// <summary>
        /// Identifiant de l'arme actuellement équipée par le joueur. 
        /// Cette valeur est mise à jour à chaque changement d'arme via SetWeaponIdOnHand
        /// et peut être consultée via GetWeaponIdOnHand.
        /// </summary>
        private static string _weaponIdOnHand;
        
        // =====================================================================
        // INIT & RESET
        // =====================================================================
        public static string GetWeaponIdOnHand()
        {
            return _weaponIdOnHand;
        }

        public static void SetWeaponIdOnHand(string id)
        {
            _weaponIdOnHand = id; 
            BatchLogger.Info($" WeaponIdOnHand update on cache : {id}");
        }
        
        /// Scans all loaded AudioClips in Unity and populates AllClipsByName.
        public static void InitGlobalClipCache()
        {
            AllClipsByName.Clear();
            foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                AllClipsByName.TryAdd(clip.name, clip);
            }
        }

        /// Clears LocalClipMap when a new weapon is equipped to avoid cross-weapon mixup.
        public static void ClearLocalMappingsIfNewWeapon(string weaponId)
        {
            LocalClipMap.Clear();
        }

        /// Registers a transformed AudioClip to prevent duplicate processing.
        public static void Register(string weaponId, AudioClip original, AudioClip transformed, CallerType callerType)
        {
           
            if (callerType.Equals(CallerType.WeaponTrack))
            {
                Transformed[(weaponId, original)] = transformed;

                if (transformed == null) return;
                AllClipsByName.TryAdd(transformed.name, transformed);
            }
            // case CallerType.Overclock
            else
            {
                TransformedOverclock[(weaponId, original)] = transformed;

                if (transformed == null) return;
                AllClipsByNameOverClock.TryAdd(transformed.name, transformed);
            }
        }
        
        public static void RegisterFireMode(string weaponId, string fireMode)
        {
            if (string.IsNullOrEmpty(weaponId))
                throw new ArgumentException("WeaponId ne peut pas être nul ou vide.", nameof(weaponId));

            lock (FireModeWeapons)
            {
                FireModeWeapons[weaponId] = fireMode;
            }
            BatchLogger.Info($" Fire Mode mis à jour dans le cache pour l'arme {weaponId} : {fireMode}");
        }

        /// Définit la cadence de tir pour une arme spécifique.
        public static void RegisterFireRate(string weaponId, int fireRate)
        {
            if (string.IsNullOrEmpty(weaponId))
                throw new ArgumentException("WeaponId ne peut pas être nul ou vide.", nameof(weaponId));
        
            lock (FireRateWeapons)
            {
                FireRateWeapons[weaponId] = fireRate;
            }
            BatchLogger.Info($" FireRate updated in cache for weapon {weaponId}: {fireRate}");
        }
        
        /// Returns true if the clip has already been transformed.
        public static bool TryGetTransformed(string weaponId, AudioClip original,
            out AudioClip transformed)
        {
                return Transformed.TryGetValue((weaponId, original), out transformed);
           
        }

        /// Returns true if the clip has already been transformed.
        public static bool TryGetTransformedOverclock(string weaponId, AudioClip original,
            out AudioClip transformed)
        {
            lock (TransformedOverclock)
            {
                return TransformedOverclock.TryGetValue((weaponId, original), out transformed);
            }
        }

        /// Checks if the original AudioClip has a transformed version.
        public static AudioClip GetOrOriginal(string weaponId, AudioClip original)
        {
            return Transformed.TryGetValue((weaponId, original), out var transformed) ? transformed : original;
        }

        /// Returns transformed clip if available, otherwise returns original.
        public static bool IsTransformed(AudioClip clip)
        {
            return clip != null && clip.name.EndsWith(Suffix, StringComparison.Ordinal);
        }

        /// Checks if a clip is considered transformed based on its name suffix.
        public static string GetTransformedName(AudioClip original)
        {
            return original.name + Suffix;
        }

        /// Removes the "_mod" suffix from a clip name to retrieve the base name.
        public static string RemoveSuffix(string clipName)
        {
            return clipName.EndsWith(Suffix, StringComparison.Ordinal)
                ? clipName.Substring(0, clipName.Length - Suffix.Length)
                : clipName;
        }

        /// Associates original clip name with transformed name (local cache, single weapon).
        public static void RegisterLocalName(string originalName, string transformedName)
        {
            if (!string.IsNullOrWhiteSpace(originalName) && !string.IsNullOrWhiteSpace(transformedName))
                LocalClipMap[originalName] = transformedName;
        }

        /// Associates original clip name with transformed name (local cache, single weapon).
        public static void RegisterLocalNameOverClok(string originalName, string transformedName)
        {
            if (!string.IsNullOrWhiteSpace(originalName) && !string.IsNullOrWhiteSpace(transformedName))
                LocalClipMapOverClok[originalName] = transformedName;
        }

        /// Tries to get the known transformed name from an original name.
        public static bool TryGetLocalName(string originalName, out string transformedName)
        {
            transformedName = null;
            if (string.IsNullOrWhiteSpace(originalName)) return false;
            return LocalClipMap.TryGetValue(originalName, out transformedName);
        }

        /// Tries to get the known transformed name from an original name.
        public static bool TryGetLocalNameOverClok(string originalName, out string transformedName)
        {
            transformedName = null;
            if (string.IsNullOrWhiteSpace(originalName)) return false;
            return LocalClipMapOverClok.TryGetValue(originalName, out transformedName);
        }

        /// Finds an AudioClip in the global cache based on its name.
        public static bool TryResolveFromName(string name, out AudioClip clip)
        {
            return AllClipsByName.TryGetValue(name, out clip);
        }

        /// Finds an AudioClip in the global cache based on its name.
        public static bool TryResolveFromNameOverClock(string name, out AudioClip clip)
        {
            lock (AllClipsByNameOverClock)
            {
                return AllClipsByNameOverClock.TryGetValue(name, out clip);
            }
        }

        /// Finds an FireRate in the global cache based on its weaponId.
        public static bool TryGetFireMode(string weaponId, out string fireMode)
        {
            lock (FireModeWeapons)
            {
                return FireModeWeapons.TryGetValue(weaponId, out fireMode);
            }
        }

        /// Récupère la cadence de tir pour une arme spécifique.
        public static bool TryGetFireRate(string weaponId, out int fireRate)
        {
            lock (FireRateWeapons)
            {
                return FireRateWeapons.TryGetValue(weaponId, out fireRate);
            }
        }
        
        /// Returns all currently cached transformed AudioClips.
        public static IEnumerable<AudioClip> GetAllTransformedClips()
        {
            return Transformed.Values;
        }

        /// Logs a message only once per key; returns true if first time, false otherwise.
        public static bool TryLogOnce(string key)
        {
            lock (LogLock)
            {
                return LoggedKeys.Add(key); // true si jamais loggé, false sinon
            }
        }


        /// Sets whether a weaponId is allowed to use audio hooks (based on mod == true).
        public static void SetHookPermission(string weaponId, bool allowed)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
                return;

            lock (HookPermissionByWeaponId)
            {
                HookPermissionByWeaponId[weaponId] = allowed;
            }
        }

        /// Checks whether a weapon is allowed to use audio hooks.
        public static bool IsHookAllowedForWeapon(string weaponId)
        {
            lock (HookPermissionByWeaponId)
            {
                return HookPermissionByWeaponId.TryGetValue(weaponId, out var allowed) && allowed;
            }
        }

        /// Fully resets weapon tracking: clears ProcessedWeapons, EnqueuedWeapons, WeaponQueue, and ActiveObservers.
        public static void ResetWeaponTracking()
        {
            lock (ProcessedWeapons) ProcessedWeapons.Clear();
            lock (EnqueuedWeapons) EnqueuedWeapons.Clear();
            lock (WeaponQueue)
            {
                while (WeaponQueue.TryDequeue(out _))
                {
                }
            }

            lock (ActiveObservers) ActiveObservers.Clear();

            BatchLogger.Log("[CacheObject] 🧹 Full weapon tracking reset.");
        }

        /// Completely clears the audio cache:
        private static void ClearAllAudiClipCache()
        {
            lock (Transformed)
            {
                Transformed.Clear();
            }
            
            lock (TransformedOverclock)
            {
                TransformedOverclock.Clear();
            }

            lock (AllClipsByName)
            {
                AllClipsByName.Clear();
            }

            lock (AllClipsByNameOverClock)
            {
                AllClipsByNameOverClock.Clear();
            }

            lock (HookPermissionByWeaponId)
            {
                HookPermissionByWeaponId.Clear();
            }

            lock (LocalClipMap)
            {
                LocalClipMap.Clear();
            }

            lock (LocalClipMapOverClok)
            {
                LocalClipMapOverClok.Clear();
            }

            BatchLogger.Log("[CacheObject] 🧹 Global cache cleared (transformed clips, mappings, and permissions).");
        }

        /// Clears EVERYTHING (weapons + audio caches). Used when disabling mod or leaving map.
        public static void ClearAllCache()
        {
            ResetWeaponTracking();
            ClearAllAudiClipCache();
            ClearFireModeCache();
            ClearAllClipsByNameOverClock();
        }

        public static void ClearAllClipsByNameOverClock()
        {
            lock (AllClipsByNameOverClock) AllClipsByNameOverClock.Clear();
            
        }

        public static void ClearAllClipsByName()
        {
            lock (AllClipsByName) AllClipsByName.Clear();
            
        }

        public static void ClearFireModeCache()
        {
            lock (FireModeProcessedWeapons) FireModeProcessedWeapons.Clear();
            lock (FireModeEnqueuedWeapons) FireModeEnqueuedWeapons.Clear();
            lock (FireModeWeaponQueue)
            {while (FireModeWeaponQueue.TryDequeue(out _)) { } }

            BatchLogger.Log("[FireMode] 🧹 Cache cleared.");
        }    
    }
}