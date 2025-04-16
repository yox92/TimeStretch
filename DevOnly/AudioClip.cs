// using System;
// using System.Collections;
// using System.IO;
// using System.Linq;
// using System.Reflection;
// using BepInEx.Logging;
// using EFT;
// using EFT.InventoryLogic;
// using HarmonyLib;
// using KmyTarkovReflection;
// using Newtonsoft.Json.Linq;
// using UnityEngine;
//
// namespace TimeStretch.Patches
// {
//     [HarmonyPatch]
//     public static class WeaponSoundTracker
//     {
//         public static ManualLogSource LOGSource;
//         public static Weapon CurrentWeapon;
//         public static bool IsFiring = false;
//         private static Coroutine resetCoroutine;
//
//         static MethodBase TargetMethod()
//         {
//             var controllerType = RefTool.EftTypes.FirstOrDefault(t =>
//                 t.Name == "FirearmController" &&
//                 t.DeclaringType != null &&
//                 t.DeclaringType.Name == "Player"
//             );
//
//             if (controllerType == null)
//             {
//                 LOGSource.LogError("❌ FirearmController introuvable.");
//                 return null;
//             }
//
//             LOGSource.LogInfo($"✅ FirearmController trouvé : {controllerType.FullName}");
//
//             var method = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
//                 .FirstOrDefault(m =>
//                     m.Name == "method_59" &&
//                     m.GetParameters().Length == 5 &&
//                     m.GetParameters()[0].Name == "weaponSoundPlayer" &&
//                     m.GetParameters()[1].Name == "ammo" &&
//                     m.GetParameters()[2].ParameterType == typeof(Vector3) &&
//                     m.GetParameters()[3].ParameterType == typeof(Vector3) &&
//                     m.GetParameters()[4].ParameterType == typeof(bool)
//                 );
//
//             if (method == null)
//             {
//                 LOGSource.LogError("❌ Méthode 'method_59' introuvable dans FirearmController.");
//             }
//
//             return method;
//         }
//
//         private static void Prefix(Player.FirearmController __instance)
//         {
//             Weapon weapon = __instance.Item;
//             if (weapon == null)
//             {
//                 CurrentWeapon = null;
//                 IsFiring = false;
//                 return;
//             }
//
//             CurrentWeapon = weapon;
//             IsFiring = true;
//
//             if (PluginTest.Instance != null)
//             {
//                 if (resetCoroutine != null)
//                     PluginTest.Instance.StopCoroutine(resetCoroutine);
//
//                 resetCoroutine = PluginTest.Instance.StartCoroutine(ResetFiringFlagAfterDelay(3f));
//             }
//
//             LOGSource.LogInfo(
//                 $"🪖 Tir détecté → Arme: {weapon.TemplateId}, {weapon.LocalizedName()} ({weapon.WeapClass})");
//         }
//
//         private static IEnumerator ResetFiringFlagAfterDelay(float delay)
//         {
//             yield return new WaitForSeconds(delay);
//             IsFiring = false;
//             LOGSource.LogInfo("🧯 IsFiring désactivé (timeout)");
//         }
//
//         [HarmonyPatch(typeof(SoundBank), nameof(SoundBank.PickClipsByDistance))]
//         public static class SoundBank_PickClipsByDistancePatch
//         {
//             [HarmonyPostfix]
//             public static void Postfix(ref AudioClip clip1, ref AudioClip clip2)
//             {
//                 LOGSource.LogInfo($"[PickClipsByDistance] IsFiring={IsFiring}, CurrentWeapon={(CurrentWeapon != null ? CurrentWeapon.TemplateId : "null")}");
//                 if (!IsFiring || CurrentWeapon == null)
//                     return;
//
//                 string id = CurrentWeapon.TemplateId;
//                 string basePath = BepInEx.Paths.PluginPath;
//                 string fireRatesPath = Path.Combine(basePath, "TimeStretch/fireRates.json");
//                 string clipMapPath = Path.Combine(basePath, "TimeStretch/bundle_clips_map.json");
//                 string missPath = Path.Combine(basePath, "TimeStretch/miss.json");
//
//                 if (!File.Exists(fireRatesPath) || !File.Exists(clipMapPath))
//                 {
//                     LOGSource.LogWarning("❌ Fichiers JSON manquants !");
//                     return;
//                 }
//
//                 JObject fireRates = JObject.Parse(File.ReadAllText(fireRatesPath));
//                 JObject bundleMap = JObject.Parse(File.ReadAllText(clipMapPath));
//                 JObject miss = File.Exists(missPath) ? JObject.Parse(File.ReadAllText(missPath)) : new JObject();
//
//                 if (!fireRates.ContainsKey(id))
//                 {
//                     LOGSource.LogInfo($"⚠️ ID inconnu dans fireRates.json : {id}");
//                     return;
//                 }
//
//                 JToken armeData = fireRates[id];
//                 JArray clipsArray = (JArray)armeData["audio"]["clips"];
//                 string bundleName = armeData["audio"]["bundle"]?.ToString();
//
//                 void AddClip(AudioClip clip)
//                 {
//                     if (clip == null)
//                     {
//                         return;
//                     }
//                     if (clip.name.Contains("shell"))
//                     {
//                         return;
//                     }
//                     if (clip.name.Contains("concrete"))
//                     {
//                         return;
//                     }
//                     if (clip.name.Contains("metal"))
//                     {
//                         return;
//                     }
//
//                     string clipName = clip.name;
//                     LOGSource.LogInfo($"🎧 Analyse du clip '{clipName}' pour l'arme {id}");
//
//                     string foundBundle = null;
//
//                     foreach (var pair in bundleMap)
//                     {
//                         if (pair.Value is JArray arr && arr.Any(t =>
//                                 string.Equals(t.ToString(), clipName, StringComparison.OrdinalIgnoreCase)))
//                         {
//                             foundBundle = pair.Key;
//                             break;
//                         }
//                     }
//
//                     if (foundBundle == null)
//                     {
//                         LOGSource.LogInfo($"🔍 Clip '{clipName}' non trouvé dans bundle_clips_map.");
//
//                         const string bundleKey = "unknown_bundle";
//
//                         if (!miss.ContainsKey(bundleKey))
//                             miss[bundleKey] = new JObject();
//
//                         JObject bundleSection = (JObject)miss[bundleKey];
//
//                         if (!bundleSection.ContainsKey(id))
//                             bundleSection[id] = new JArray();
//
//                         JArray clipList = (JArray)bundleSection[id];
//
//                         if (!clipList.Any(
//                                 x => string.Equals(x.ToString(), clipName, StringComparison.OrdinalIgnoreCase)))
//                         {
//                             clipList.Add(clipName);
//                             LOGSource.LogWarning(
//                                 $"❌ Clip '{clipName}' introuvable → ajouté à miss.json [{bundleKey}] / {id}");
//                         }
//                         else
//                         {
//                             LOGSource.LogInfo($"⚠️ Clip '{clipName}' déjà listé dans miss.json pour {id}");
//                         }
//
//                         return;
//                     }
//
//                     LOGSource.LogInfo($"✅ Clip '{clipName}' trouvé dans le bundle '{foundBundle}'");
//
//                     if (!clipsArray.Any(x => string.Equals(x.ToString(), clipName, StringComparison.OrdinalIgnoreCase)))
//                     {
//                         clipsArray.Add(clipName);
//                         LOGSource.LogInfo($"---------------------------------------");
//                         LOGSource.LogInfo($"🎧 Ajout de '{clipName}' à l'arme {id}");
//                     }
//                     else
//                     {
//                         LOGSource.LogInfo($"📎 Clip '{clipName}' déjà présent dans fireRates pour {id}");
//                     }
//
//                     if (string.IsNullOrEmpty(bundleName))
//                     {
//                         armeData["audio"]["bundle"] = foundBundle;
//                         LOGSource.LogInfo($"📦 Bundle associé à {id} → '{foundBundle}' (via {clipName})");
//                     }
//                 }
//
//                 AddClip(clip1);
//                 AddClip(clip2);
//
//                 File.WriteAllText(fireRatesPath, fireRates.ToString());
//                 File.WriteAllText(missPath, miss.ToString());
//             }
//         }
//     }
// }