// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;
// using EFT;
// using UnityEngine;
// using HarmonyLib;
// using TimeStretch.Utils;
// using Object = object;
//
// namespace TimeStretch.Patches
// {
//     [HarmonyPatch]
//     public class PatchAssetBundle
//     {
//         [HarmonyTargetMethods]
//         static IEnumerable<MethodBase> TargetMethods()
//         {
//             var type = typeof(AssetBundle);
//             var methods = new List<MethodBase?>
//             {
//                 AccessTools.Method(type, "LoadAssetWithSubAssetsAsync_Internal",
//                     [typeof(string), typeof(Type)]),
//                 AccessTools.Method(type, "LoadAssetAsync_Internal", [typeof(string), typeof(Type)]),
//                 AccessTools.Method(type, "LoadAsset_Internal", [typeof(string), typeof(Type)]),
//                 AccessTools.Method(type, "LoadAssetWithSubAssets_Internal", [typeof(string), typeof(Type)]),
//                 AccessTools.Method(type, "LoadAllAssetsAsync_Internal", [typeof(Type)]),
//                 AccessTools.Method(type, "LoadAllAssets_Internal", [typeof(Type)]),
//                 AccessTools.Method(type, "LoadAsset", [typeof(string), typeof(Type)]),
//                 AccessTools.Method(type, "LoadAllAssets", [typeof(Type)])
//             };
//
//             return methods.Where(m => m != null)!;
//         }
//
//         [ThreadStatic] private static string? _currentBundleName;
//
//         [HarmonyPrefix]
//         public static void Prefix(AssetBundle __instance)
//         {
//             try
//             {
//                 var name = __instance?.name;
//                 _currentBundleName = string.IsNullOrWhiteSpace(name) ? null : name;
//             }
//             catch (Exception ex)
//             {
//                 // Option 1 : ignorer silencieusement
//                 _currentBundleName = null;
//
//                 // Option 2 : log dans un contexte global (exception système)
//                 BatchLogger.LogException(ex, "PatchAssetBundle.Prefix");
//             }
//         }
//
//         [HarmonyPostfix]
//         public static void Postfix(Object __result)
//         {
//             var bundleName = _currentBundleName ?? "<unknown>";
//             var normalizedBundle = bundleName.Replace('\\', '/');
//
//             var isValidPath =
//                 normalizedBundle.StartsWith("assets/content/audio/weapons") ||
//                 normalizedBundle.StartsWith("assets/content/audio/banks") ||
//                 normalizedBundle.StartsWith("assets/content/weapons");
//
//             if (!isValidPath) return;
//
//             void ProcessAudioClip(AudioClip clip, string sourceTag)
//             {
//                 var log = new List<string>
//                 {
//                     $"📦 Bundle : {bundleName}",
//                     $"📥 Source : {sourceTag}"
//                 };
//
//                 HandleAudioClip(clip, bundleName, log);
//             }
//
//             void ProcessSoundBank(SoundBank bank, string sourceTag)
//             {
//                 foreach (var innerClip in ExtractAudioClips(bank))
//                 {
//                     var log = new List<string>
//                     {
//                         $"📦 Bundle : {bundleName}",
//                         $"📥 Source : {sourceTag}",
//                         $"🎛️ SoundBank détecté : {bank.name}"
//                     };
//
//                     HandleAudioClip(innerClip, bundleName, log, fromBank: true);
//                 }
//             }
//
//             switch (__result)
//             {
//                 case AssetBundleRequest req:
//                     foreach (var asset in req.allAssets ?? Array.Empty<Object>())
//                     {
//                         switch (asset)
//                         {
//                             case AudioClip clip:
//                                 ProcessAudioClip(clip, "AssetBundleRequest");
//                                 break;
//                             case SoundBank bank:
//                                 ProcessSoundBank(bank, "AssetBundleRequest");
//                                 break;
//                         }
//                     }
//
//                     break;
//
//                 case AudioClip singleClip:
//                     ProcessAudioClip(singleClip, "DirectClip");
//                     break;
//
//                 case SoundBank bank2:
//                     ProcessSoundBank(bank2, "DirectSoundBank");
//                     break;
//
//                 case Object[] array:
//                     foreach (var obj in array)
//                     {
//                         switch (obj)
//                         {
//                             case AudioClip clip:
//                                 ProcessAudioClip(clip, "ArrayClip");
//                                 break;
//                             case SoundBank bank:
//                                 ProcessSoundBank(bank, "ArraySoundBank");
//                                 break;
//                         }
//                     }
//
//                     break;
//             }
//         }
//
//         private static void HandleAudioClip(AudioClip clip, string bundleName, List<string> log, bool fromBank = false)
//         {
//             if (!FireRateDataStore.ContainsClip(clip.name)) return;
//             
//             if (IsTransformed(clip, log))
//             {
//                 BatchLogger.FlushClipLog(log, clip.name);
//                 return;
//             }
//
//
//             var tag = fromBank ? "[SoundBank]" : "[AudioClip]";
//             var tempoChange = FireRateDataStore.GetTempoModifier(clip.name, bundleName);
//
//             log.Add($"{tag} 🎯 Clip ciblé : {clip.name} — Changement de tempo : {tempoChange:+0.##;-0.##}%");
//
//
//             CoroutineRunner.Run(WaitUntilReady(clip, ready =>
//             {
//                 log.Add($"{tag} ✅ AudioClip prêt : {ready.name} depuis bundle : {bundleName}");
//
//                 AudioClipInspector.Inspect(ready, $"{tag} 🔍 Avant :", log);
//
//                 var transformed = AudioClipTransformer.Transform(ready, tempoChange, log);
//                 
//                 if (transformed != ready)
//                 {
//                     log.Add($"{tag} 🎵 nouveau Clip : {transformed.name}");
//                     AudioClipInspector.Inspect(transformed, $"{tag} 🔍 Après :", log);
//                 }
//
//                 BatchLogger.FlushClipLog(log, clip.name);
//             }, log));
//         }
//
//         public static IEnumerable<AudioClip> ExtractAudioClips(SoundBank bank)
//         {
//             foreach (var env in bank.Environments ?? [])
//             {
//                 foreach (var distVar in env?.Clips ?? [])
//                 {
//                     foreach (var clip in distVar?.Clips ?? [])
//                     {
//                         if (clip != null)
//                             yield return clip;
//                     }
//                 }
//             }
//         }
//
//         private static IEnumerator WaitUntilReady(AudioClip clip, Action<AudioClip> callback, List<string> log)
//         {
//             const float timeout = 10f;
//             var elapsed = 0f;
//
//             while (clip.loadState == AudioDataLoadState.Loading && elapsed < timeout)
//             {
//                 elapsed += Time.unscaledDeltaTime;
//                 yield return null;
//             }
//
//             if (clip.loadState == AudioDataLoadState.Loaded)
//             {
//                 callback(clip);
//             }
//             else
//             {
//                 log.Add($"⏳ Timeout — clip '{clip.name}' pas chargé (state = {clip.loadState})");
//                 BatchLogger.FlushClipLog(log, clip.name);
//             }
//         }
//
//         private static bool IsTransformed(AudioClip clip, List<string> log)
//         {
//             var transformed = CacheObject.TryGetTransformed(clip,out var audioClip);
//             if (transformed)
//             {
//                 log.Add($"[IsTransformed] ⏭️ Déjà transformé : {clip.name} SKIP");
//             }
//
//             return transformed;
//         }
//     }
// }
//
// // public static class BundleInterceptor
// // {
// //     public static string? CurrentBundleName;
// //     
// //     private static readonly Dictionary<string, HashSet<string>> bundleClipMap = new();
// //     
// //     public static void Prefix(AssetBundle __instance)
// //     {
// //         // Sauvegarde le nom du bundle Unity en cours de traitement
// //         CurrentBundleName = __instance.name;
// //     }
// //
// //     
// //      public static void OnLoadAssetWithSubAssetsAsync_Internal(string name, Type type, AssetBundleRequest __result)
// //     {
// //         if (__result == null)
// //             return;
// //     
// //         __result.completed += _ =>
// //         {
// //             if (__result.allAssets == null) return;
// //     
// //             foreach (var asset in __result.allAssets)
// //             {
// //                 if (asset is AudioClip clip && clip.name.Contains("m4a1"))
// //                 {
// //
// //                     if (IsTransformed(clip)) continue;
// //                    
// //                     CoroutineRunner.Run(WaitUntilReady(clip, ready =>
// //                     {
// //                         BatchLogger.Info($"✅ AudioClip prêt : {ready.name}");
// //                         AudioClipInspector.Inspect(ready);
// //     
// //                         var transformed = AudioClipTransformer.Transform(ready, 30f);
// //                         if (transformed != ready)
// //                         {
// //                             BatchLogger.Info($"🎵 Clip transformé : {transformed.name}");
// //                             AudioClipInspector.Inspect(transformed);
// //                         }
// //                     }));
// //                 }
// //             }
// //         };
// //     }
//
//
// // public static void OnBundleLoadAsync(string path, AssetBundleCreateRequest __result)
// // {
// //     if (__result == null)
// //     {
// //         return;
// //     }
// //
// //     if (path.Contains("/assets/content/audio/banks/") || path.Contains("/assets/content/audio/weapons/"))
// //         BatchLogger.Info($"📦 Demande de bundle async : {path}");
// // }
//
// // public static void OnAssetsLoadAsync(AssetBundle __instance, AssetBundleRequest __result)
// // {
// //     if (__instance == null || __result == null)
// //         return;
// //
// //     string bundleName = string.IsNullOrEmpty(__instance.name) ? "[bundle sans nom]" : __instance.name;
// //
// //     if (bundleName.Contains("content/audio/banks") || bundleName.Contains("content/audio/weapons"))
// //         BatchLogger.Info($"📂 LoadAllAssetsAsync sur : {bundleName}");
// //
// //     foreach (var asset in __result.allAssets)
// //     {
// //         if (asset is AudioClip clip)
// //         {
// //             if (bundleName.Contains("content/audio/weapons"))
// //             {
// //                 if (!bundleClipMap.ContainsKey(bundleName))
// //                     bundleClipMap[bundleName] = new HashSet<string>();
// //
// //                 bundleClipMap[bundleName].Add(clip.name);
// //                 BatchLogger.Info($"🎧 Ajout AudioClip : {clip.name} → {bundleName}");
// //             }
// //         }
// //         else if (asset is SoundBank sb)
// //         {
// //             if (bundleName.Contains("content/audio/banks"))
// //             {
// //                 BatchLogger.Info($"🔊 SoundBank dans {bundleName} : {asset.name}");
// //                 ExtractClipsFromSoundBank(sb, bundleName);
// //             }
// //         }
// //     }
// //
// //     DumpClipBundleMapToJson(Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch/bundle_clips_map.json"));
// // }
//
// // private static void ExtractClipsFromSoundBank(SoundBank soundBank, string bundleName)
// // {
// //     if (soundBank.Environments == null)
// //         return;
// //
// //     for (int envIndex = 0; envIndex < soundBank.Environments.Length; envIndex++)
// //     {
// //         var env = soundBank.Environments[envIndex];
// //         if (env?.Clips == null)
// //             continue;
// //
// //         for (int distIndex = 0; distIndex < env.Clips.Length; distIndex++)
// //         {
// //             var distanceVarity = env.Clips[distIndex];
// //             if (distanceVarity?.Clips == null)
// //                 continue;
// //
// //             foreach (var clip in distanceVarity.Clips)
// //             {
// //                 if (clip == null) continue;
// //                 if (clip != null)
// //                 {
// //                     BatchLogger.Info($"ajout dans le json de : {clip.name}");
// //                     if (!bundleClipMap.ContainsKey(bundleName))
// //                         bundleClipMap[bundleName] = new HashSet<string>();
// //                     bundleClipMap[bundleName].Add(clip.name);
// //                         
// //                 }
// //             }
// //         }
// //     }
// // }
//
// // public static void DumpClipBundleMapToJson(string path)
// // {
// //     JObject existingJson = File.Exists(path)
// //         ? JObject.Parse(File.ReadAllText(path))
// //         : new JObject();
// //
// //     foreach (var kvp in bundleClipMap)
// //     {
// //         string bundleName = kvp.Key;
// //         HashSet<string> clips = kvp.Value;
// //
// //         if (!existingJson.ContainsKey(bundleName))
// //         {
// //             existingJson[bundleName] = new JArray(clips.OrderBy(x => x));
// //         }
// //         else
// //         {
// //             JArray existing = (JArray)existingJson[bundleName];
// //             HashSet<string> merged = new HashSet<string>(existing.Select(c => c.ToString()));
// //
// //             foreach (var clip in clips)
// //                 merged.Add(clip);
// //           
// //             existingJson[bundleName] = new JArray(merged.OrderBy(x => x));
// //         }
// //     }
// //
// //     File.WriteAllText(path, existingJson.ToString(Formatting.Indented));
// //     BatchLogger.Info($" JSON mis à jour (merge) : {path}");
// // }
//
//
// // private static IEnumerator WaitForClipAndTransform(AudioClip clip, float tempoChangePercent)
// // {
// //     while (clip.loadState == AudioDataLoadState.Loading)
// //         yield return null;
// //
// //     if (clip.loadState != AudioDataLoadState.Loaded)
// //     {
// //         BatchLogger.Warn($"❌ Clip '{clip.name}' échoué à se charger (état: {clip.loadState})");
// //         yield break;
// //     }
// //
// //     BatchLogger.Info($"✅ Clip '{clip.name}' prêt, tentative de transformation...");
// //     AudioClipInspector.Inspect(clip);
// //
// //     // Lecture des données
// //     int channels = clip.channels;
// //     int sampleRate = clip.frequency;
// //     int sampleCount = clip.samples;
// //     float[] pcm = new float[sampleCount * channels];
// //
// //     if (!clip.GetData(pcm, 0))
// //     {
// //         BatchLogger.Warn($"⚠️ Impossible de lire les données PCM même après chargement complet.");
// //         yield break;
// //     }
// //
// //     // Créer une copie modifiable
// //     AudioClip copy = AudioClip.Create(clip.name + "_copy", sampleCount, channels, sampleRate, false);
// //     copy.SetData(pcm, 0);
// //
// //     AudioClip transformed = AudioClipTransformer.Transform(copy, tempoChangePercent);
// //     AudioClipInspector.Inspect(transformed);
// //     // AudioClipModifier.AudioClipRegistry.Register(clip.name, transformed);
// //
// //     BatchLogger.Info($"🎵 Audio transformé : {transformed.name}");
// // }
//
//
// //     }
// // }