using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeStretch.Cache;
using TimeStretch.Entity;
using TimeStretch.Utils;
using UnityEngine;

namespace TimeStretch.AudioClipTools
{
    public static class AudioClipModifier
    {
        /// Registers a virtual replacement of an AudioClip, without memory overwrite.
        public static void RegisterReplacement(
            AudioClip original,
            AudioClip newClip,
            List<string> log,
            string weaponId,
            CallerType callerType)
        {
            if (original == null || newClip == null)
            {
                log.Add("[AudioClipModifier] ⚠️ Null clip — replacement not registered.");
                return;
            }

            CacheObject.Register(weaponId, original, newClip, callerType);
            log.Add($"[AudioClipModifier] 🔁 Clip '{original.name}' virtually replaced by '{newClip.name}'.");
        }

        /// <summary>
        /// Transforms an audio clip by applying a tempo modification asynchronously.
        /// </summary>
        /// <param name="clip">The audio clip to transform</param>
        /// <param name="tempo">The tempo modification percentage to apply</param>
        /// <param name="log">List to store log messages</param> 
        /// <param name="weaponId">ID of the associated weapon</param>
        /// <param name="callerType">Type of caller requesting the transform</param>
        /// <returns>Task representing the asynchronous transform operation</returns>
        public static Task TransformClip(AudioClip clip, float tempo, List<string> log, string weaponId,
            CallerType callerType)
        {
            if (Plugin.ShouldStopThreads)
            {
                log.Add($"[AudioClipModifier]⛔ Transform canceled for {clip.name} (mod disable)");
                return Task.CompletedTask;
            }

            log.Add($"[AudioClipModifier]🌀 Transform async : {clip.name} tempo {tempo:+0.0;-0.0}%");
            return Task.Run(async () =>
            {
                try
                {
                    var newClip = await AudioClipTransformer.TransformAsync(clip, tempo, log, weaponId, callerType);
                    log.Add($"[AudioClipModifier]✅ Transformed clip : {newClip.name}");
                }
                catch (Exception ex)
                {
                    log.Add($"[AudioClipModifier]❌ TransformAsync failed: {ex.Message}");
                }
            });
        }
        /// <summary>
        /// Gets a list of audio clips that can be transformed with their associated tempo modification.
        /// </summary>
        /// <param name="weaponId">The ID of the weapon to get clips for</param>
        /// <param name="callerType">The type of caller requesting the clips</param>
        /// <returns>A list of tuples containing the audio clip and its calculated tempo modification</returns>
        public static List<(AudioClip clip, float tempo)> GetTransformableClips(string weaponId, CallerType callerType)
        {
            var result = new List<(AudioClip, float)>();
            if (!JsonCache.TryGetEntry(weaponId, out var entry) || entry.Audio?.Clips == null)
            {
                BatchLogger.Log($"[AudioClipModifier]⚠️ No audio clips found for weapon {weaponId} in JSON.");
                return result;
            }
            CacheObject.ClearOverClockClipCacheForWeapon(weaponId);
            CacheObject.ClearAllClipsByName();

            var matchingAudioClips = GetMatchingAudioClips(entry.Audio.Clips.Keys);
            BatchLogger.Log(
                $"[AudioClipModifier] Analyzing {matchingAudioClips.Count} clip matching for weapon {weaponId}");

            foreach (var clip in matchingAudioClips)
            {
                float tempo;
                var baseName = CacheObject.RemoveSuffix(clip.name);
                if (!entry.Audio.Clips.Keys.Select(CacheObject.RemoveSuffix).Contains(baseName)) continue;
                
                if (callerType == CallerType.WeaponTrack)
                {
                    BatchLogger.Log($"case : {CallerType.WeaponTrack}");
                    tempo = JsonCache.GetTempoModifier(weaponId, baseName);
                }
                else
                {
                    BatchLogger.Log($"case : {CallerType.Overclock}");
                    tempo = OverClockUtils.CalculateOverClockTempo(weaponId);
                }

                BatchLogger.Log($"tempo calculation: {tempo}%");
                var clampedTempo = Mathf.Clamp(tempo, Plugin.TempoMin.Value, Plugin.TempoMax.Value);
                BatchLogger.Log($"clampedTempo: {clampedTempo}%");

                if (Mathf.Approximately(clampedTempo, 0f))
                {
                    BatchLogger.Log(
                        $"[AudioClipModifier]⚠️ Ignored clip (tempo clamped to 0%): {clip.name} (raw={tempo:+0.0;-0.0}%)");
                    continue;
                }

                BatchLogger.Log(
                    $"[AudioClipModifier]✅ Clips to transform {clip.name} | raw={tempo:+0.0;-0.0}%, clamped={clampedTempo:+0.0;-0.0}%");
                result.Add((clip, clampedTempo));
            }

            BatchLogger.Log($"[AudioClipModifier]🎧 ✅ Transformed clip status {weaponId} : {result.Count}");
            return result;
        }

        /// <summary>
        /// Finds all AudioClips in memory that match the provided clip names after removing suffixes.
        /// </summary>
        /// <param name="clipKeys">Collection of clip names to match against</param>
        /// <returns>List of matching AudioClips found in memory</returns>
        private static List<AudioClip> GetMatchingAudioClips(IEnumerable<string> clipKeys)
        {
            var clipNames = clipKeys
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(CacheObject.RemoveSuffix)
                .ToHashSet();
            BatchLogger.Log($"🎧 [AudioClipModifier] Analyzing {clipNames.Count} clip name(s)");
            
            var matchingAudioClips = new List<AudioClip>();
            var allAudioClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            BatchLogger.Log($"🔍 [AudioClipModifier] Found {allAudioClips.Length} total AudioClips in memory");
        
            foreach (var clip in allAudioClips)
            {
                try
                {
                    if (clip == null) continue;
                    
                    if (clip.name.EndsWith("_mod", StringComparison.OrdinalIgnoreCase)) continue;
                    
                    var baseName = CacheObject.RemoveSuffix(clip.name);
                    
                    if (clipNames.Contains(baseName))
                    {
                        BatchLogger.Log($"[DEBUG] 🔊 AudioClip found: {clip.name}");
                        matchingAudioClips.Add(clip);
                    }
                }
                catch (Exception e)
                {
                    BatchLogger.Error($"[AudioClipModifier]❌ Error processing AudioClip: {e.Message}");
                }
            }
        
            BatchLogger.Log($"✅ [AudioClipModifier] Matched {matchingAudioClips.Count} AudioClips from {clipNames.Count} names");
            return matchingAudioClips;
        }

        /// <summary>
        /// Coroutine that checks the availability of transformed audio clips in memory.
        /// </summary>
        /// <param name="clipNames">List of original clip names to check for their transformed versions</param>
        /// <returns>IEnumerator for coroutine execution</returns>
        public static IEnumerator OneShotClipAvailability(List<string> clipNames)
        {
            if (Plugin.ShouldStopThreads)
                yield break;
            var wait = new WaitForSeconds(0.5f);
            yield return wait;

            var log = new List<string> { "🎧 [AudioClipModifier] Status of transformed clips (one-shot):" };
            var found = 0;

            foreach (var baseName in clipNames)
            {
                var transformedName = baseName + "_mod";
                var exists = CacheObject.TryResolveFromName(transformedName, out var clip);

                if (!exists)
                {
                    clip = CacheObject.GetAllTransformedClips()
                        .FirstOrDefault(c => c?.name == transformedName);
                }

                if (clip != null)
                {
                    found++;
                    log.Add($"    ✅ Clip in cache: {transformedName}");
                }
                else
                {
                    log.Add($"    ❌ Missing clip: {transformedName}");
                }
            }

            log.Add(found == clipNames.Count
                ? $"🎧 ✅ All {found} clips are visible in memory"
                : $"🎧 ⚠️ Only {found}/{clipNames.Count} clips are visible.");

            foreach (var entry in log)
                BatchLogger.Log(entry);
        }
    }
}