// using System;
// using System.Collections;
// using System.Collections.Concurrent;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Linq;
// using System.Threading.Tasks;
// using TimeStretch;
// using TimeStretch.Hooks;
// using TimeStretch.Patches;
// using TimeStretch.Utils;
// using UnityEngine;
// using Debug = UnityEngine.Debug;
//
// public class ClipObserver : MonoBehaviour
// {
//     private readonly Queue<AudioClip> _clipsToProcess = new();
//     private readonly ConcurrentQueue<string> _clipNamesQueue = new();
//
//     private bool isScanning;
//     private Coroutine scanCoroutine;
//     private static bool isTransformingClip = false;
//
//     private void Start()
//     {
//         // Rien ici — déclenché par le hook AssetBundle
//     }
//
//     public void StartTimedScan()
//     {
//         if (isScanning) return;
//
//         isScanning = true;
//         PatchAssetBundleAllMethods.Disable();
//         scanCoroutine = StartCoroutine(RunScanQueueForDuration(20f));
//     }
//
//     private IEnumerator RunScanQueueForDuration(float duration)
//     {
//         var globalTimer = Stopwatch.StartNew();
//         int transformedCount = 0;
//         HashSet<string> bundlesUsed = new();
//         BatchLogger.Info("[ClipObserver] 🔎 Début du scan (20s)");
//         
//         float elapsed = 0f;
//
//         // Démarre le scan des noms (ne fait rien si déjà transformés)
//         yield return StartCoroutine(ScanClipNamesInBackground());
//
//         // Puis traite 1 clip à la fois
//         while (elapsed < duration)
//         {
//             // On stoppe le scan si un clip est en cours de transformation
//             if (isTransformingClip)
//             {
//                 yield return new WaitForSecondsRealtime(0.1f);
//                 elapsed += 0.1f;
//                 continue;
//             }
//
//             if (_clipsToProcess.Count > 0)
//             {
//                 var clip = _clipsToProcess.Dequeue();
//                 yield return StartCoroutine(ProcessClipCoroutine(clip));
//             }
//
//             yield return new WaitForSecondsRealtime(0.1f);
//             elapsed += 0.1f;
//         }
//
//         isScanning = false;
//
//         PatchAssetBundleAllMethods.Enable();
//         globalTimer.Stop();
//
//         var summaryLog = new List<string>
//         {
//             $"📋 Bilan de transformation — {transformedCount} clips traités",
//             $"⏱️ Durée totale : {globalTimer.ElapsedMilliseconds} ms",
//             $"📁 Bundles impliqués : {string.Join(", ", bundlesUsed)}"
//         };
//         BatchLogger.FlushReplacementLog(summaryLog, "[ClipObserver] 🔚 Résumé du scan");
//     }
//
//     private IEnumerator ScanClipNamesInBackground()
//     {
//       
//         if (!FireRateDataStore.IsInitialized)
//         {
//             BatchLogger.Warn("[ClipObserver] ⚠️ FireRateDataStore non prêt — scan ignoré");
//             yield break;
//         }
//
//         string[] names = null;
//
//         yield return TaskToCoroutine(Task.Run(() => { names = FireRateDataStore.GetAllTrackedClipNames(); }));
//
//         var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
//         var log = new List<string>();
//         int total = 0;
//         int alreadyModded = 0;
//         int alreadyCached = 0;
//         int notTracked = 0;
//         int enqueued = 0;
//
//         foreach (var clip in allClips)
//         {
//             if (clip == null)
//                 continue;
//
//             total++;
//             var baseName = CacheObject.RemoveSuffix(clip.name);
//
//             if (clip.name.EndsWith("_mod", StringComparison.OrdinalIgnoreCase))
//             {
//                 alreadyModded++;
//                 continue;
//             }
//
//             if (CacheObject.TryGetTransformed(clip, out _))
//             {
//                 alreadyCached++;
//                 continue;
//             }
//
//             if (!names.Contains(baseName))
//             {
//                 notTracked++;
//                 continue;
//             }
//
//             if (!_clipsToProcess.Contains(clip))
//             {
//                 _clipsToProcess.Enqueue(clip);
//                 enqueued++;
//             }
//         }
//
//         log.Add($"🎧 Scan async terminé — {enqueued} clips TRACKÉS enqueue");
//         log.Add($"📊 Statistiques : total={total}, _mod={alreadyModded}, cache={alreadyCached}, ignorés={notTracked}");
//         BatchLogger.FlushReplacementLog(log,"[ClipObserver] 🔍 Résultat du scan");
//     }
//
//     private IEnumerator ProcessClipCoroutine(AudioClip clip)
//     {
//         if (CacheObject.TryGetTransformed(clip, out _))
//             yield break;
//
//         var baseName = CacheObject.RemoveSuffix(clip.name);
//         var entry = FireRateDataStore.GetEntryByClip(baseName);
//         if (entry == null) yield break;
//
//         var bundleName = entry.audio.clips.TryGetValue(baseName, out var clipInfo)
//             ? clipInfo.bundle?.FirstOrDefault()
//             : null;
//
//         var tempoMod = FireRateDataStore.GetTempoModifier(baseName, bundleName);
//         if (Mathf.Approximately(tempoMod, 0f)) yield break;
//
//         isTransformingClip = true;
//         var log = new List<string>();
//         var tag = "🎧 Transform";
//
//         yield return WaitUntilReady(clip, log);
//         if (clip.loadState != AudioDataLoadState.Loaded)
//         {
//             isTransformingClip = false;
//             yield break;
//         }
//
//         // Inspect avant transformation
//         AudioClipInspector.Inspect(clip, $"{tag} 🔍 Avant :", log);
//
//         var sw = System.Diagnostics.Stopwatch.StartNew();
//
//         // var task = AudioClipTransformer.TransformAsync(clip, tempoMod, log);
//         // var task = new Task<AudioClip>;
//         // while (!task.IsCompleted)
//         //     yield return null;
//         //
//         // sw.Stop();
//         //
//         // if (task.Exception != null)
//         // {
//         //     log.Add($"[ClipObserver] ❌ Erreur dans TransformAsync : {task.Exception.Flatten().Message}");
//         // }
//         // else
//         // {
//         //     var result = task.Result;
//         //     if (result != clip)
//         //     {
//         //         log.Add($"{tag} 🎵 nouveau Clip : {result.name}");
//         //         AudioClipInspector.Inspect(result, $"{tag} 🔍 Après :", log);
//         //     }
//         // }
//
//         // Création du label de log enrichi
//         string finalLabel = $"AutoTransform: {clip.name} ({bundleName ?? "unknown"}) — {tempoMod:+0.0;-0.0}% en {sw.ElapsedMilliseconds}ms";
//
//         // Flush avec label enrichi
//         BatchLogger.FlushReplacementLog(log, finalLabel);
//         isTransformingClip = false;
//     }
//
//     private static IEnumerator WaitUntilReady(AudioClip clip, List<string> log)
//     {
//         const float timeout = 2f;
//         float elapsed = 0f;
//
//         while (clip.loadState == AudioDataLoadState.Loading && elapsed < timeout)
//         {
//             elapsed += Time.unscaledDeltaTime;
//             yield return null;
//         }
//
//         if (clip.loadState != AudioDataLoadState.Loaded)
//         {
//             log.Add($"⏳ Timeout — clip '{clip.name}' pas chargé (state = {clip.loadState})");
//             BatchLogger.FlushClipLog(log, clip.name);
//         }
//     }
//
//
//     private static IEnumerator TaskToCoroutine(Task task)
//     {
//         while (!task.IsCompleted)
//             yield return null;
//
//         if (task.Exception != null)
//             Debug.LogException(task.Exception);
//     }
// }