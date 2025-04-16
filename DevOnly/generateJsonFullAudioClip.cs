// using System;
// using System.Collections.Concurrent;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Threading.Tasks;
// using AssetsTools.NET;
// using AssetsTools.NET.Extra;
// using BepInEx.Logging;
// using Newtonsoft.Json;
//
// namespace TimeStretch.Boot;
//
// public class GenerateJsonFullAudioClip
// {
//     private static ManualLogSource _log;
//     private static readonly List<string> InternalLogs = new();
//     private static readonly ConcurrentDictionary<string, object> FileLocks = new();
//
//     private static readonly string[] FilterKeywords =
//     {
//         "indoor", "close", "tail", "silenced", "loop", "outdoor", "fire", "shot"
//     };
//
//     private static readonly string[] BundleDirectoriesToInclude =
//     {
//         "/assets/content/audio/banks",
//         "/assets/content/audio/weapons",
//         "/assets/content/weapons"
//     };
//
//     public GenerateJsonFullAudioClip(ManualLogSource log)
//     {
//         _log = log;
//     }
//
//     public void ScanBundlesAndGenerateJson()
//     {
//         WriteLog("⏳ Lancement du scan des AudioClips (2 passes)...");
//
//         var gameFolder = Path.Combine(BepInEx.Paths.PluginPath, "..", "..");
//         var baseAssetPath = Path.Combine(gameFolder, "EscapeFromTarkov_Data", "StreamingAssets", "Windows", "assets");
//
//         var allBundlePaths = Directory.GetFiles(baseAssetPath, "*.bundle", SearchOption.AllDirectories).ToList();
//         WriteLog($"📁 Bundles trouvés avant filtre : {allBundlePaths.Count}");
//
//         var filteredBundles = allBundlePaths
//             .Where(path => BundleDirectoriesToInclude.Any(valid => path.Replace('\\', '/').ToLower().Contains(valid)))
//             .ToList();
//
//         WriteLog($"📁 Bundles retenus après filtre : {filteredBundles.Count}");
//
//         var clipBankRefs = new ConcurrentDictionary<long, List<string>>();
//         var audioClipMap = new ConcurrentDictionary<string, AudioClipEntry>();
//
//         int totalMonoBehaviours = 0;
//         int totalPathIdExtracted = 0;
//
//         // === Première passe : analyse récursive des MonoBehaviour ===
//         Parallel.ForEach(filteredBundles, bundlePath =>
//         {
//             var fileLock = FileLocks.GetOrAdd(bundlePath, _ => new object());
//             lock (fileLock)
//             {
//                 try
//                 {
//                     if (!TryGetRelativeAssetPath(bundlePath, baseAssetPath, out var bundleRelative)) return;
//
//                     var classdataPath = Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch", "classdata.tpk");
//                     if (!File.Exists(bundlePath) || !File.Exists(classdataPath)) return;
//
//                     var manager = new AssetsManager();
//                     manager.LoadClassPackage(classdataPath);
//                     var bundleInst = manager.LoadBundleFile(bundlePath);
//                     var bundleFile = bundleInst.file;
//
//                     foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
//                     {
//                         if (dirInfo.Name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
//
//                         var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
//                         if (assetsInst?.file == null) continue;
//
//                         foreach (var assetInfo in assetsInst.file.Metadata.AssetInfos)
//                         {
//                             if (assetInfo.TypeId != 114) continue;
//
//                             totalMonoBehaviours++;
//                             WriteLog(
//                                 $"🔍 Analyse récursive de MonoBehaviour dans {bundleRelative} (PathID: {assetInfo.PathId})");
//
//                             var baseField = manager.GetBaseField(assetsInst, assetInfo);
//                             if (baseField == null) continue;
//
//                             RecursivelyExtractPathIds(baseField, bundleRelative, clipBankRefs,
//                                 ref totalPathIdExtracted);
//                         }
//
//                         manager.UnloadAssetsFile(assetsInst);
//                     }
//
//                     manager.UnloadBundleFile(bundleInst);
//                 }
//                 catch (Exception ex)
//                 {
//                     WriteLog($"❌ Erreur lecture (1ère passe) {bundlePath} : {ex.Message}");
//                 }
//             }
//         });
//
//         WriteLog($"📊 MonoBehaviour (TypeId=114) total : {totalMonoBehaviours}");
//         WriteLog($"📊 PathID extraits depuis banques   : {totalPathIdExtracted}");
//
//         if (totalPathIdExtracted == 0) return;
//
//         // === Deuxième passe : extraction AudioClip + mapping ===
//         Parallel.ForEach(filteredBundles, bundlePath =>
//         {
//             var fileLock = FileLocks.GetOrAdd(bundlePath, _ => new object());
//             lock (fileLock)
//             {
//                 try
//                 {
//                     if (!TryGetRelativeAssetPath(bundlePath, baseAssetPath, out var bundleRelative)) return;
//
//                     var classdataPath = Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch", "classdata.tpk");
//                     if (!File.Exists(bundlePath) || !File.Exists(classdataPath)) return;
//
//                     var manager = new AssetsManager();
//                     manager.LoadClassPackage(classdataPath);
//                     var bundleInst = manager.LoadBundleFile(bundlePath);
//                     var bundleFile = bundleInst.file;
//
//                     foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
//                     {
//                         if (dirInfo.Name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
//
//                         var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
//                         if (assetsInst?.file == null) continue;
//
//                         foreach (var assetInfo in assetsInst.file.Metadata.AssetInfos)
//                         {
//                             if (assetInfo.TypeId != 83) continue;
//
//                             var baseField = manager.GetBaseField(assetsInst, assetInfo);
//                             if (baseField == null) continue;
//
//                             var name = baseField["m_Name"].AsString;
//                             if (string.IsNullOrWhiteSpace(name)) continue;
//                             if (!FilterKeywords.Any(k => name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
//                                 continue;
//
//                             long pathId = assetInfo.PathId;
//
//                             var entry = audioClipMap.GetOrAdd(pathId.ToString(), _ => new AudioClipEntry
//                             {
//                                 Clip = name,
//                                 PathID = pathId.ToString()
//                             });
//
//                             lock (entry)
//                             {
//                                 if (!entry.ClipsFromBundle.Contains(bundleRelative))
//                                     entry.ClipsFromBundle.Add(bundleRelative);
//
//                                 if (clipBankRefs.TryGetValue(pathId, out var banks))
//                                 {
//                                     foreach (var bank in banks)
//                                     {
//                                         if (!entry.ClipsFromBank.Contains(bank))
//                                             entry.ClipsFromBank.Add(bank);
//                                     }
//                                 }
//                             }
//                         }
//
//                         manager.UnloadAssetsFile(assetsInst);
//                     }
//
//                     manager.UnloadBundleFile(bundleInst);
//                 }
//                 catch (Exception ex)
//                 {
//                     WriteLog($"❌ Erreur lecture (2e passe) {bundlePath} : {ex.Message}");
//                 }
//             }
//         });
//
//         // Écriture du JSON
//         var outputPath = Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch", "scrapAudioClip.json");
//         var json = JsonConvert.SerializeObject(audioClipMap, Formatting.Indented);
//         File.WriteAllText(outputPath, json);
//         WriteLog($"📦 JSON généré : {outputPath}");
//
//         var logPath = Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch", "scan_log.txt");
//         File.WriteAllLines(logPath, InternalLogs);
//         WriteLog("✅ Scan terminé. JSON généré.");
//     }
//
//     private static void RecursivelyExtractPathIds(AssetTypeValueField field, string bundleRelative,
//         ConcurrentDictionary<long, List<string>> pathIdMap, ref int totalExtracted)
//     {
//         if (field == null) return;
//
//         if (field.FieldName == "m_PathID")
//         {
//             long pathId = field.AsLong;
//             if (pathId == 0)
//             {
//                 WriteLog($"⚠️ m_PathID trouvé mais = 0 (ignoré) depuis {bundleRelative}");
//                 return;
//             }
//
//             WriteLog($"✅ PathID trouvé récursivement : {pathId} depuis {bundleRelative}");
//
//             pathIdMap.AddOrUpdate(pathId,
//                 _ => new List<string> { bundleRelative },
//                 (_, list) =>
//                 {
//                     lock (list)
//                     {
//                         if (!list.Contains(bundleRelative))
//                             list.Add(bundleRelative);
//                     }
//
//                     return list;
//                 });
//
//             totalExtracted++;
//         }
//
//         foreach (var child in field.Children)
//         {
//             RecursivelyExtractPathIds(child, bundleRelative, pathIdMap, ref totalExtracted);
//         }
//     }
//
//     private static bool TryGetRelativeAssetPath(string fullPath, string baseAssetPath, out string relative)
//     {
//         fullPath = Path.GetFullPath(fullPath).Replace('\\', '/');
//         baseAssetPath = Path.GetFullPath(baseAssetPath).Replace('\\', '/');
//
//         if (!fullPath.StartsWith(baseAssetPath))
//         {
//             relative = "";
//             return false;
//         }
//
//         relative = "assets" + fullPath.Substring(baseAssetPath.Length).TrimStart('/');
//         return true;
//     }
//
//     private static void WriteLog(string message)
//     {
//         _log?.LogInfo(message);
//         InternalLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
//     }
// }
//
// public class AudioClipEntry
// {
//     public string Clip { get; set; }
//     public string PathID { get; set; }
//     public List<string> ClipsFromBundle { get; set; } = [];
//     public List<string> ClipsFromBank { get; set; } = [];
// }
//
//
// // using System;
// // using System.Collections.Concurrent;
// // using System.Collections.Generic;
// // using System.IO;
// // using System.Linq;
// // using System.Threading.Tasks;
// // using AssetsTools.NET;
// // using AssetsTools.NET.Extra;
// // using BepInEx.Logging;
// // using Newtonsoft.Json;
// //
// // public class GenerateJsonFullAudioClip
// // {
// //     private static ManualLogSource _log;
// //     private static readonly List<string> _internalLogs = new();
// //     private static readonly ConcurrentDictionary<string, object> _fileLocks = new();
// //
// //     private static readonly string[] FilterKeywords =
// //     {
// //         "indoor", "close", "tail", "silenced", "loop", "outdoor", "fire", "shot"
// //     };
// //
// //     private static readonly string[] BundleDirectoriesToInclude =
// //     {
// //         "/assets/content/audio/banks",
// //         "/assets/content/audio/weapons",
// //         "/assets/content/weapons"
// //     };
// //
// //     public GenerateJsonFullAudioClip(ManualLogSource log)
// //     {
// //         _log = log;
// //     }
// //
// //     public void ScanBundlesAndGenerateJson()
// //     {
// //         WriteLog("⏳ Lancement du scan des AudioClips (2 passes)...");
// //
// //         var gameFolder = Path.Combine(BepInEx.Paths.PluginPath, "..", "..");
// //         var baseAssetPath = Path.Combine(gameFolder, "EscapeFromTarkov_Data", "StreamingAssets", "Windows", "assets");
// //
// //         var allBundlePaths = Directory.GetFiles(baseAssetPath, "*.bundle", SearchOption.AllDirectories).ToList();
// //         WriteLog($"📁 Bundles trouvés avant filtre : {allBundlePaths.Count}");
// //
// //         var filteredBundles = allBundlePaths
// //             .Where(path => BundleDirectoriesToInclude.Any(valid => path.Replace('\\', '/').ToLower().Contains(valid)))
// //             .ToList();
// //
// //         WriteLog($"📁 Bundles retenus après filtre : {filteredBundles.Count}");
// //
// //         var clipBankRefs = new ConcurrentDictionary<long, List<string>>();
// //         var audioClipMap = new ConcurrentDictionary<string, AudioClipEntry>();
// //
// //         // === Première passe : collecter les PathID référencés dans les banques ===
// //
// //         int totalMonoBehaviours = 0;
// //         int totalWithScript = 0;
// //         int totalSoundBank = 0;
// //         int totalWithEnvironments = 0;
// //         int totalClipFields = 0;
// //         int totalPathIdNonZero = 0;
// //         int totalPathIdNull = 0;
// //         int totalClipsExtracted = 0;
// //
// //         Parallel.ForEach(filteredBundles, bundlePath =>
// //         {
// //             var fileLock = _fileLocks.GetOrAdd(bundlePath, _ => new object());
// //             lock (fileLock)
// //             {
// //                 try
// //                 {
// //                     if (!TryGetRelativeAssetPath(bundlePath, baseAssetPath, out var bundleRelative)) return;
// //
// //                     var classdataPath = Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch", "classdata.tpk");
// //                     if (!File.Exists(bundlePath) || !File.Exists(classdataPath)) return;
// //
// //                     var manager = new AssetsManager();
// //                     manager.LoadClassPackage(classdataPath);
// //                     var bundleInst = manager.LoadBundleFile(bundlePath);
// //                     var bundleFile = bundleInst.file;
// //
// //                     foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
// //                     {
// //                         if (dirInfo.Name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
// //
// //                         var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
// //                         if (assetsInst?.file == null) continue;
// //
// //                         foreach (var assetInfo in assetsInst.file.Metadata.AssetInfos)
// //                         {
// //                             if (assetInfo.TypeId != 114) continue;
// //
// //                             totalMonoBehaviours++;
// //
// //                             var baseField = manager.GetBaseField(assetsInst, assetInfo);
// //                             if (baseField == null) continue;
// //
// //                             var scriptField = baseField["m_Script"];
// //                             if (scriptField == null) continue;
// //                             totalWithScript++;
// //
// //                             var monoScriptBase = TryGetBaseFieldFromPPtr(manager, assetsInst, scriptField);
// //                             if (monoScriptBase == null) continue;
// //
// //                             var className = monoScriptBase["m_ClassName"]?.AsString;
// //                             if (className != "SoundBank") continue;
// //
// //                             totalSoundBank++;
// //
// //                             var envArray = baseField["Environments"];
// //                             if (envArray == null || envArray.Children.Count == 0) continue;
// //
// //                             totalWithEnvironments++;
// //
// //                             foreach (var envWrapper in envArray.Children)
// //                             {
// //                                 var envData = envWrapper["data"];
// //                                 if (envData == null) continue;
// //
// //                                 var distArray = envData["Clips"];
// //                                 if (distArray == null || distArray.Children.Count == 0) continue;
// //
// //                                 foreach (var distWrapper in distArray.Children)
// //                                 {
// //                                     var distData = distWrapper["data"];
// //                                     if (distData == null) continue;
// //
// //                                     var clipsVector = distData["Clips"];
// //                                     if (clipsVector == null || clipsVector.Children.Count == 0) continue;
// //
// //                                     foreach (var clipPtr in clipsVector.Children)
// //                                     {
// //                                         totalClipFields++;
// //
// //                                         var innerData = clipPtr["data"];
// //                                         if (innerData == null)
// //                                         {
// //                                             totalPathIdNull++;
// //                                             continue;
// //                                         }
// //
// //                                         AssetPPtr pptr = AssetPPtr.FromField(innerData);
// //                                         long pathId = pptr.PathId;
// //
// //                                         if (pathId == 0)
// //                                         {
// //                                             totalPathIdNull++;
// //                                             continue;
// //                                         }
// //
// //                                         totalPathIdNonZero++;
// //
// //                                         clipBankRefs.AddOrUpdate(pathId,
// //                                             _ => new List<string> { bundleRelative },
// //                                             (_, list) =>
// //                                             {
// //                                                 lock (list)
// //                                                 {
// //                                                     if (!list.Contains(bundleRelative))
// //                                                         list.Add(bundleRelative);
// //                                                 }
// //
// //                                                 return list;
// //                                             });
// //
// //                                         totalClipsExtracted++;
// //                                     }
// //                                 }
// //                             }
// //                         }
// //
// //                         manager.UnloadAssetsFile(assetsInst);
// //                     }
// //
// //                     manager.UnloadBundleFile(bundleInst);
// //                 }
// //                 catch (Exception ex)
// //                 {
// //                     WriteLog($"❌ Erreur lecture (1ère passe) {bundlePath} : {ex.Message}");
// //                 }
// //             }
// //         });
// //
// //         WriteLog($"📊 MonoBehaviour (TypeId=114) total : {totalMonoBehaviours}");
// //         WriteLog($"📊 Avec champ m_Script trouvé        : {totalWithScript}");
// //         WriteLog($"📊 Dont MonoScript est 'SoundBank'  : {totalSoundBank}");
// //         WriteLog($"📊 Avec champ Environments valide   : {totalWithEnvironments}");
// //         WriteLog($"📊 Total PPtr<$AudioClip> rencontrés   : {totalClipFields}");
// //         WriteLog($"📊 Dont m_PathID null ou invalide      : {totalPathIdNull}");
// //         WriteLog($"📊 Dont m_PathID ≠ 0                   : {totalPathIdNonZero}");
// //         WriteLog($"📊 PathID extraits depuis banques      : {totalClipsExtracted}");
// //         
// //         if (totalClipsExtracted == 0) return;
// //
// //         // === Deuxième passe : extraction des AudioClip + mapping ===
// //
// //         Parallel.ForEach(filteredBundles, bundlePath =>
// //         {
// //             var fileLock = _fileLocks.GetOrAdd(bundlePath, _ => new object());
// //             lock (fileLock)
// //             {
// //                 try
// //                 {
// //                     if (!TryGetRelativeAssetPath(bundlePath, baseAssetPath, out var bundleRelative)) return;
// //
// //                     var classdataPath = Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch", "classdata.tpk");
// //                     if (!File.Exists(bundlePath) || !File.Exists(classdataPath)) return;
// //
// //                     var manager = new AssetsManager();
// //                     manager.LoadClassPackage(classdataPath);
// //                     var bundleInst = manager.LoadBundleFile(bundlePath);
// //                     var bundleFile = bundleInst.file;
// //
// //                     foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
// //                     {
// //                         if (dirInfo.Name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
// //
// //                         var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
// //                         if (assetsInst?.file == null) continue;
// //
// //                         foreach (var assetInfo in assetsInst.file.Metadata.AssetInfos)
// //                         {
// //                             if (assetInfo.TypeId != 83) continue;
// //
// //                             var baseField = manager.GetBaseField(assetsInst, assetInfo);
// //                             if (baseField == null) continue;
// //
// //                             var name = baseField["m_Name"].AsString;
// //                             if (string.IsNullOrWhiteSpace(name)) continue;
// //
// //                             if (!FilterKeywords.Any(k => name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
// //                                 continue;
// //
// //                             long pathId = assetInfo.PathId;
// //
// //                             var entry = audioClipMap.GetOrAdd(pathId.ToString(), _ => new AudioClipEntry
// //                             {
// //                                 clip = name,
// //                                 PathID = pathId.ToString()
// //                             });
// //
// //                             lock (entry)
// //                             {
// //                                 if (!entry.ClipsFromBundle.Contains(bundleRelative))
// //                                     entry.ClipsFromBundle.Add(bundleRelative);
// //
// //                                 if (clipBankRefs.TryGetValue(pathId, out var banks))
// //                                 {
// //                                     foreach (var bank in banks)
// //                                     {
// //                                         if (!entry.ClipsFromBank.Contains(bank))
// //                                             entry.ClipsFromBank.Add(bank);
// //                                     }
// //                                 }
// //                             }
// //                         }
// //
// //                         manager.UnloadAssetsFile(assetsInst);
// //                     }
// //
// //                     manager.UnloadBundleFile(bundleInst);
// //                 }
// //                 catch (Exception ex)
// //                 {
// //                     WriteLog($"❌ Erreur lecture (2e passe) {bundlePath} : {ex.Message}");
// //                 }
// //             }
// //         });
// //
// //         // Écriture du JSON
// //         var outputPath = Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch", "scrapAudioClip.json");
// //         var json = JsonConvert.SerializeObject(audioClipMap, Formatting.Indented);
// //         File.WriteAllText(outputPath, json);
// //         WriteLog($"📦 JSON généré : {outputPath}");
// //
// //         var logPath = Path.Combine(BepInEx.Paths.PluginPath, "TimeStretch", "scan_log.txt");
// //         File.WriteAllLines(logPath, _internalLogs);
// //         WriteLog("✅ Scan terminé. JSON généré.");
// //     }
// //
// //     private static AssetTypeValueField TryGetBaseFieldFromPPtr(AssetsManager manager, AssetsFileInstance inst, AssetTypeValueField ptrField)
// //     {
// //         AssetPPtr pptr = AssetPPtr.FromField(ptrField);
// //         var ext = manager.GetExtAsset(inst, pptr.FileId, pptr.PathId);
// //         return ext.baseField;
// //     }
// //
// //     private static bool TryGetRelativeAssetPath(string fullPath, string baseAssetPath, out string relative)
// //     {
// //         fullPath = Path.GetFullPath(fullPath).Replace('\\', '/');
// //         baseAssetPath = Path.GetFullPath(baseAssetPath).Replace('\\', '/');
// //
// //         if (!fullPath.StartsWith(baseAssetPath))
// //         {
// //             relative = "";
// //             return false;
// //         }
// //
// //         relative = "assets" + fullPath.Substring(baseAssetPath.Length).TrimStart('/');
// //         return true;
// //     }
// //
// //     private static void WriteLog(string message)
// //     {
// //         _log?.LogInfo(message);
// //         _internalLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
// //     }
// // }
// //
// // public class AudioClipEntry
// // {
// //     public string clip { get; set; }
// //     public string PathID { get; set; }
// //     public List<string> ClipsFromBundle { get; set; } = new();
// //     public List<string> ClipsFromBank { get; set; } = new();
// // }