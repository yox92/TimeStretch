using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeStretch.Entity;
using TimeStretch.Utils;
using UnityEngine;

namespace TimeStretch.AudioClipTools
{
    public static class AudioClipModifier
    {
        /// NOT USE
        public static void Replace(AudioClip original, AudioClip newwClip, List<string> log)
        {
            if (original == null || newwClip == null)
            {
                log.Add($"[AudioClipModifier] ⚠️ Clip null — remplacement impossible.");
                return;
            }

            if (original.channels != newwClip.channels || original.frequency != newwClip.frequency)
            {
                log.Add(
                    $"[AudioClipModifier] ⚠️ Incompatibilité entre original '{original.name}' et replacement '{newwClip.name}' (channels or frequency mismatch)");
                return;
            }

            if (original.loadType == AudioClipLoadType.DecompressOnLoad &&
                original.loadState == AudioDataLoadState.Loaded)
            {
                int channels = original.channels;
                int newSampleCount = newwClip.samples;
                float[] processedData = new float[newSampleCount * channels];

                if (!newwClip.GetData(processedData, 0))
                {
                    log.Add(
                        $"[AudioClipModifier] 🤮 Impossible de lire les données de '{newwClip.name}' pour écrasement.");
                    return;
                }

                bool ok = original.SetData(processedData, 0);
                if (ok)
                {
                    log.Add($"[AudioClipModifier] ✅ Clip '{original.name}' écrasé avec succès en mémoire.");
                }
                else
                {
                    log.Add($"[AudioClipModifier] 🤮 Échec de l'écrasement en mémoire de '{original.name}'.");
                }

                return;
            }

            log.Add(
                $"[AudioClipModifier] ⛔ Clip non écrasable : '{original.name}' (loadType={original.loadType}, state={original.loadState})");
        }

        /// Registers a virtual replacement of an AudioClip, without memory overwrite.
        public static void RegisterReplacement(AudioClip original, AudioClip newClip, List<string> log, string weaponId)
        {
            if (original == null || newClip == null)
            {
                log.Add("[AudioClipModifier] ⚠️ Null clip — replacement not registered.");
                return;
            }

            CacheObject.Register(weaponId, original, newClip);
            log.Add($"[AudioClipModifier] 🔁 Clip '{original.name}' virtually replaced by '{newClip.name}'.");
        }
        
        public static Task TransformClip(AudioClip clip, float tempo, List<string> log, string weaponId)
        {
            if (Plugin.ShouldStopThreads)
            {
                log.Add($"[AudioClipModifier]⛔ Transform canceled for {clip.name} (mod disable)");
                return Task.CompletedTask;
            }
            log.Add($"[AudioClipModifier]🌀 Transform async : {clip.name} tempo {tempo:+0.0;-0.0}%");
            return AudioClipTransformer.TransformAsync(clip, tempo, log, weaponId)
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                        log.Add($"[AudioClipModifier]❌ TransformAsync failed: {task.Exception.Flatten().Message}");
                    else
                        log.Add($"[AudioClipModifier]✅ Transformed clip : {task.Result.name}");
                });
        }
        
        public static List<(AudioClip clip, float tempo)> GetTransformableClips(string weaponId)
        {
            var result = new List<(AudioClip, float)>();
            if (!JsonCache.TryGetEntry(weaponId, out var entry) || entry.Audio?.Clips == null)
            {
                BatchLogger.Log($"[AudioClipModifier]⚠️ No audio clips found for weapon {weaponId} in JSON.");
                return result;
            }

            var clipNames = entry.Audio.Clips.Keys
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(CacheObject.RemoveSuffix)
                .ToHashSet();

            BatchLogger.Log($"🎧 [AudioClipModifier] Analyzing {clipNames.Count} clip name(s) for weapon {weaponId}...");

            foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (clip == null) continue;
                if (clip.name.EndsWith("_mod", StringComparison.OrdinalIgnoreCase)) continue;

                var baseName = CacheObject.RemoveSuffix(clip.name);
                if (!clipNames.Contains(baseName)) continue;

                if (CacheObject.TryGetTransformed(weaponId, clip, out _))
                {
                    BatchLogger.Log($"[AudioClipModifier]🔁 Already transformed: : {clip.name}");
                    continue;
                }

                var tempo = JsonCache.GetTempoModifier(weaponId, baseName);
                BatchLogger.Log($"tempo calculation: {tempo}%");
                float clampedTempo = Mathf.Clamp(tempo, Plugin.TempoMin.Value, Plugin.TempoMax.Value);
                BatchLogger.Log($"clampedTempo: {clampedTempo}%");

                if (Mathf.Approximately(clampedTempo, 0f))
                {
                    BatchLogger.Log($"[AudioClipModifier]⚠️ Ignored clip (tempo clampé à 0%) : {clip.name} (raw={tempo:+0.0;-0.0}%)");
                    continue;
                }

                BatchLogger.Log($"[AudioClipModifier]✅ Clips to transform {clip.name} | raw={tempo:+0.0;-0.0}%, clamped={clampedTempo:+0.0;-0.0}%");
                result.Add((clip, clampedTempo));
            }

            BatchLogger.Log($"[AudioClipModifier]🎧 ✅ Transformed clip status {weaponId} : {result.Count}");
            return result;
        }
        
        public static IEnumerator OneShotClipAvailability(List<string> clipNames)
        {
            if (Plugin.ShouldStopThreads)
                yield break;
            var wait = new WaitForSeconds(0.5f);
            yield return wait;

            var log = new List<string> { "🎧 [AudioClipModifier] État des clips transformés (one-shot):" };
            var found = 0;

            foreach (string baseName in clipNames)
            {
                string transformedName = baseName + "_mod";
                bool exists = CacheObject.TryResolveFromName(transformedName, out var clip);

                if (!exists)
                {
                    clip = CacheObject.GetAllTransformedClips()
                        .FirstOrDefault(c => c?.name == transformedName);
                }

                if (clip != null)
                {
                    found++;
                    log.Add($"    ✅ Clip en cache : {transformedName}");
                }
                else
                {
                    log.Add($"    ❌ Clip manquant : {transformedName}");
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