// using System;
// using System.Collections;
// using System.Collections.Concurrent;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;
// using HarmonyLib;
// using TimeStretch.Utils;
// using UnityEngine;
//
// namespace TimeStretch.Patches
// {
//     [HarmonyPatch]
//     public class PatchAudioClip
//     {
//         [HarmonyTargetMethods]
//         static IEnumerable<MethodBase> TargetMethods()
//         {
//             var type = typeof(AudioClip);
//             return new List<MethodBase?>
//             {
//                 AccessTools.Method(type, nameof(AudioClip.GetData), new[] { typeof(float[]), typeof(int) }),
//                 AccessTools.Method(type, nameof(AudioClip.SetData), new[] { typeof(float[]), typeof(int) }),
//                 AccessTools.Method(type, nameof(AudioClip.LoadAudioData)),
//                 AccessTools.Method(type, nameof(AudioClip.UnloadAudioData)),
//             }.Where(m => m != null)!;
//         }
//
//         private static readonly ConcurrentQueue<string> LogBuffer = new();
//         private static readonly object LogLock = new();
//         private static bool flushing = false;
//
//         private static readonly HashSet<string> AlreadyInspected = new();
//
//         [HarmonyPostfix]
//         public static void Postfix(AudioClip __instance, MethodBase __originalMethod)
//         {
//             if (__instance == null)
//                 return;
//
//             var name = __instance.name;
//             string methodName = __originalMethod.Name;
//             
//             if (!AlreadyInspected.Add(name))
//                 return;
//
//             if (!FireRateDataStore.ContainsClip(name))
//                 return;
//
//             if (__instance.loadState == AudioDataLoadState.Loaded)
//             {
//                 LogBuffer.Enqueue($"[PatchAudioClip] Méthode appelée : {methodName} Clip déjà chargé : {name} (LoadType={__instance.loadType})");
//                 FlushLogs();
//             }
//             else if (__instance.loadType == AudioClipLoadType.DecompressOnLoad)
//             {
//                 if (__instance.LoadAudioData())
//                 {
//                     LogBuffer.Enqueue($"[PatchAudioClip] Méthode appelée : {methodName} ⏳ Chargement manuel déclenché pour : {name}");
//                     CoroutineRunner.Run(WaitUntilReady(__instance, ready =>
//                     {
//                         LogBuffer.Enqueue($"[PatchAudioClip] Méthode appelée : {methodName} Clip chargé manuellement : {ready.name}");
//                         FlushLogs();
//                     }));
//                 }
//                 else
//                 {
//                     LogBuffer.Enqueue($"[PatchAudioClip] Méthode appelée : {methodName} LoadAudioData() a échoué pour : {name}");
//                     FlushLogs();
//                 }
//             }
//         }
//         
//         private static IEnumerator WaitUntilReady(AudioClip clip, Action<AudioClip> callback)
//         {
//             float timeout = 10f;
//             float elapsed = 0f;
//
//             while (clip.loadState == AudioDataLoadState.Loading && elapsed < timeout)
//             {
//                 elapsed += Time.unscaledDeltaTime;
//                 yield return null;
//             }
//
//             if (clip.loadState == AudioDataLoadState.Loaded)
//                 callback(clip);
//             else
//             {
//                 LogBuffer.Enqueue($"[PatchAudioClip] ⏳Méthode Timeout — clip '{clip.name}' pas chargé (state={clip.loadState})");
//                 FlushLogs();
//             }
//         }
//
//
//         private static void FlushLogs()
//         {
//             lock (LogLock)
//             {
//                 if (flushing) return;
//                 flushing = true;
//
//                 while (LogBuffer.TryDequeue(out var line))
//                 {
//                     BatchLogger.Info(line);
//                 }
//
//                 flushing = false;
//             }
//         }
//     }
// }