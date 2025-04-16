// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;
// using HarmonyLib;
// using TimeStretch.Utils;
// using UnityEngine;
//
// namespace TimeStretch.Hooks
// {
//     [HarmonyPatch]
//     public static class PatchAssetBundleAllMethods
//     {
//         private static bool _isPatched = false;
//         private static Harmony _harmonyInstance;
//         private const string HarmonyId = "com.spt.assetbundle.dynamic";
//         private static bool ShouldTriggerPostfix = true;
//
//         public static void Enable()
//         {
//             if (_isPatched) return;
//             ShouldTriggerPostfix = true;
//
//             if (_harmonyInstance == null)
//                 _harmonyInstance = new Harmony(HarmonyId);
//
//             var patchedCount = 0;
//
//             foreach (var method in TargetMethods().Concat(InternalTargetMethods()))
//             {
//                 try
//                 {
//                     _harmonyInstance.Patch(method,
//                         prefix: new HarmonyMethod(typeof(PatchAssetBundleAllMethods), nameof(Prefix)),
//                         postfix: new HarmonyMethod(typeof(PatchAssetBundleAllMethods), nameof(Postfix)));
//
//                     BatchLogger.Log($" method Patch : {method.Name}");
//                     patchedCount++;
//                 }
//                 catch (Exception ex)
//                 {
//                     BatchLogger.Log($"❌ Erreur de patch sur {method.Name} : {ex.Message}");
//                 }
//             }
//
//             _isPatched = true;
//             BatchLogger.Log($"🔧 Total patchs appliqués : {patchedCount}");
//         }
//
//         public static void Disable()
//         {
//             if (!_isPatched) return;
//
//             Harmony.UnpatchID(HarmonyId);
//             ShouldTriggerPostfix = false;
//             _isPatched = false;
//
//             BatchLogger.Log("⛔ PatchAssetBundleAllMethods désactivé via UnpatchID.");
//         }
//
//         public static IEnumerable<MethodBase> TargetMethods()
//         {
//             return typeof(AssetBundle)
//                 .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
//                 .Where(m =>
//                     !m.IsGenericMethod &&
//                     m.ReturnType != typeof(void) &&
//                     m.GetParameters().All(p => !p.IsOut));
//         }
//
//         public static IEnumerable<MethodBase> InternalTargetMethods()
//         {
//             var type = typeof(AssetBundle);
//             var methodList = new[]
//             {
//                 ("LoadAssetWithSubAssetsAsync_Internal", new[] { typeof(string), typeof(Type) }),
//                 ("LoadAssetAsync_Internal", new[] { typeof(string), typeof(Type) }),
//                 ("LoadAsset_Internal", new[] { typeof(string), typeof(Type) }),
//                 ("LoadAssetWithSubAssets_Internal", new[] { typeof(string), typeof(Type) }),
//                 ("LoadAllAssetsAsync_Internal", new[] { typeof(Type) }),
//                 ("LoadAllAssets_Internal", new[] { typeof(Type) }),
//                 ("LoadAsset", new[] { typeof(string), typeof(Type) }),
//                 ("LoadAllAssets", new[] { typeof(Type) })
//             };
//
//             foreach (var (name, args) in methodList)
//             {
//                 var method = AccessTools.Method(type, name, args);
//                 if (method != null)
//                     yield return method;
//                 else
//                     BatchLogger.Warn($"⚠️ Méthode introuvable : {name}({string.Join(", ", args.Select(a => a.Name))})");
//             }
//         }
//
//         [HarmonyPrefix]
//         public static void Prefix(AssetBundle __instance)
//         {
//             // Optionnel : stocker l’origine
//         }
//
//         [HarmonyPostfix]
//         public static void Postfix(MethodBase __originalMethod, object __result)
//         {
//             
//             if (!ShouldTriggerPostfix)
//                 return;
//             var log = new List<string>();
//             try
//             {
//                 if (__result == null) return;
//                
//
//                 var observer = GameObject.Find("ClipObserver")?.GetComponent<ClipObserver>();
//                 if (observer != null && observer.isActiveAndEnabled)
//                 {
//                     observer.StartTimedScan();
//                     log.Add($" Scan : {__originalMethod?.Name}");
//                 }
//             }
//             catch (Exception ex)
//             {
//                 log.Add($"❌ Exception dans Postfix({__originalMethod?.Name}) : {ex.Message}");
//             }
//             BatchLogger.Block(log);
//         }
//     }
// }