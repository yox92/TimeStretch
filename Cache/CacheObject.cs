using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EFT;
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

        /// Thread-safe dictionary mapping original AudioClip → to its transformed version (time-stretched).
        /// Bound to a weapon with a UNIQUE ID.
        private static readonly ConcurrentDictionary<(string weaponId, AudioClip), AudioClip> Transformed = new();

        /// Local mapping for the currently equipped weapon: (original name → transformed name).
        /// Example: "ak47_fire" → "ak47_fire_mod".
        /// Not thread-safe; should only be used from Unity's main thread.
        private static readonly Dictionary<string, string> LocalClipMap = new();

        /// Global thread-safe dictionary mapping clip names to AudioClips in memory.
        private static readonly ConcurrentDictionary<string, AudioClip> AllClipsByName = new();

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

        /// Set of weaponIds already processed to prevent redundant transformations.
        public static readonly HashSet<string> ProcessedWeapons = new();

        /// Set of weaponIds already enqueued in WeaponQueue to avoid duplicates.
        public static readonly HashSet<string> EnqueuedWeapons = new();

        /// Set of Players currently being observed (via coroutine) to avoid duplicates.
        public static readonly HashSet<Player> ActiveObservers = new();
   
        /// FireRate / IDTemplate scrap at start.
        public static readonly ConcurrentDictionary<string, float> WeaponFireRates = new();
        


        // =====================================================================
        // INIT & RESET
        // =====================================================================
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
        public static void Register(string weaponId, AudioClip original, AudioClip transformed)
        {
            Transformed[(weaponId, original)] = transformed;

            if (transformed != null)
            {
                AllClipsByName.TryAdd(transformed.name, transformed);
            }
        }

        /// Returns true if the clip has already been transformed.
        public static bool TryGetTransformed(string weaponId, AudioClip original, out AudioClip transformed)
        {
            return Transformed.TryGetValue((weaponId, original), out transformed);
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

        /// Tries to get the known transformed name from an original name.
        public static bool TryGetLocalName(string originalName, out string transformedName)
        {
            transformedName = null;
            if (string.IsNullOrWhiteSpace(originalName)) return false;
            return LocalClipMap.TryGetValue(originalName, out transformedName);
        }

        /// Finds an AudioClip in the global cache based on its name.
        public static bool TryResolveFromName(string name, out AudioClip clip)
        {
            return AllClipsByName.TryGetValue(name, out clip);
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
        public static void ClearAllAudiClipCache()
        {
            lock (Transformed)
            {
                Transformed.Clear();
            }

            lock (AllClipsByName)
            {
                AllClipsByName.Clear();
            }

            lock (HookPermissionByWeaponId)
            {
                HookPermissionByWeaponId.Clear();
            }

            lock (LocalClipMap)
            {
                LocalClipMap.Clear();
            }

            BatchLogger.Log("[CacheObject] 🧹 Global cache cleared (transformed clips, mappings, and permissions).");
        }

        /// Clears EVERYTHING (weapons + audio caches). Used when disabling mod or leaving map.
        public static void ClearAllCache()
        {
            ResetWeaponTracking();
            ClearAllAudiClipCache();
        }
    }
}