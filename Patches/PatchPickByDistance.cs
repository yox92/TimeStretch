using System;
using System.Collections.Generic;
using BepInEx.Logging;
using EFT;
using HarmonyLib;
using EFT.InventoryLogic;
using TimeStretch.Cache;
using TimeStretch.Utils;
using UnityEngine;

namespace TimeStretch.Patches
{
    [HarmonyPatch]
    public static class PatchPickByDistance
    {
        [ThreadStatic] public static bool InWeaponFire;
        [ThreadStatic] public static Weapon CurrentWeapon;
        [ThreadStatic] public static bool IsLocalPlayerSound;
        public static ManualLogSource LOGSource;

        // ========== HOOK SoundBank.PickClipsByDistance ==========
        [HarmonyPatch(typeof(SoundBank), nameof(SoundBank.PickClipsByDistance))]
        public static class SoundBankPickClipsByDistancePatch
        {
            [HarmonyPostfix]
            public static void Postfix(ref AudioClip clip1, ref AudioClip clip2, SoundBank __instance)
            {
                try
                {
                    //🔐 same player than PatchFireBulletPlayerTracker ?
                    if (!IsLocalPlayerSound) return;
                    //🔐 audio mod ?
                    if (!Plugin.EnableAudioMod.Value) return;
                    //🔐 WeaponFire ?
                    if (!InWeaponFire) return;
                    //🔐 we get weapon on hand ?
                    if (CurrentWeapon == null) return;

                    string weaponId = CurrentWeapon.TemplateId;
                    
                    CacheObject.TryGetFireMode(weaponId, out var fireMode);
                    var isOverClock = fireMode == ((Weapon.EFireMode)0x08).ToString();
                    var weaponMod = CacheObject.IsHookAllowedForWeapon(CurrentWeapon.TemplateId);
                    
                    BatchLogger.Log($" [PatchPickByDistance] testx fire-mod {fireMode}");
                    if (!isOverClock && !weaponMod)
                    {
                        BatchLogger.Log($"⛔ [PatchPickByDistance] block access isOverClock : {isOverClock}, weaponMod : {weaponMod}");
                        return;
                    }
                    
                    List<string> log = [];

                    clip1 = ReplaceIfTransformed(clip1, weaponId, log, isOverClock);;
                    clip2 = ReplaceIfTransformed(clip2, weaponId, log, isOverClock);

                    BatchLogger.FlushClipLog(log, weaponId);
                }
                catch (Exception ex)
                {
                    BatchLogger.Log($"🤮 Erreur dans PickClipsByDistance Postfix : {ex.Message}");
                }
            }

            private static AudioClip ReplaceIfTransformed(
                AudioClip original,
                string weaponId,
                List<string> log,
                bool isOverClock)
            {
                if (original == null)
                    return null;

                var clipName = original.name;
                var baseName = CacheObject.RemoveSuffix(clipName);

                log.Add("---------------------------");

                if (string.IsNullOrWhiteSpace(weaponId))
                {
                    log.Add("[PatchPickByDistance] ❌ No player or weapon equipped");
                    return original;
                }
                
                var transformed = ProcessClipTransformation(original, baseName, weaponId, log, isOverClock);
                if (transformed == null)
                    return original;

                RegisterTransformedClip(baseName, transformed, log, isOverClock);

                LogClipTransformation(original, transformed, log, clipName, weaponId);

                return transformed;
            }

            /// <summary>
            /// Gère la transformation du clip. Vérifie le cache local et récupère les clips transformés.
            /// </summary>
            private static AudioClip ProcessClipTransformation(AudioClip original, string baseName, string weaponId, List<string> log, bool isOverClock)
            {
                if (TryGetCachedClip(baseName, weaponId, log, isOverClock, out var transformed)) 
                    return transformed;
                if (!isOverClock && !IsWeaponTransformable(weaponId, log)) 
                    return null;
                if (!ResolveTransformedClip(weaponId, original, log, isOverClock, out transformed))
                    return null;

                return transformed;
            }

            /// <summary>
            /// Vérifie si le clip est déjà en cache local.
            /// </summary>
            private static bool TryGetCachedClip(string baseName, string weaponId, List<string> log, bool isOverClock, out AudioClip transformed)
            {
                string transformedName;
                transformed = null;

                if (isOverClock)
                {
                    if (CacheObject.TryGetLocalNameOverClok(baseName, out transformedName) &&
                        CacheObject.TryResolveFromNameOverClock(transformedName, out transformed))
                    {
                        log.Add($"🎧 [FireRateRange] [CACHE] Clip found in cache: {transformedName}");
                        return true;
                    }
                    log.Add($"🎧 [FireRateRange][CACHE] Clip not found in cache.");
                }
                else
                {
                    if (CacheObject.TryGetLocalName(baseName, out transformedName) &&
                        CacheObject.TryResolveFromName(transformedName, out transformed))
                    {
                        log.Add($"🎧 [CACHE] Clip found in cache: {transformedName}");
                        return true;
                    }
                    log.Add($"🎧 [CACHE] Clip not found in cache.");
                }

                return false;
            }

            /// <summary>
            /// Vérifie si l'arme associée est transformable.
            /// </summary>
            private static bool IsWeaponTransformable(string weaponId, List<string> log)
            {
                if (!JsonCache.TryGetEntry(weaponId, out var entry) || !entry.Mod)
                {
                    log.Add($"[PatchPickByDistance] ❌ Weapon {weaponId} is not marked as modifiable");
                    return false;
                }
                return true;
            }

            /// <summary>
            /// Récupère le clip transformé à partir de la source appropriée.
            /// </summary>
            private static bool ResolveTransformedClip(string weaponId, AudioClip original, List<string> log, bool isOverClock, out AudioClip transformed)
            {
                transformed = null;

                if (isOverClock)
                {
                    if (!CacheObject.TryGetTransformedOverclock(weaponId, original, out transformed))
                    {
                        log.Add($"[TRACK ⏳ FireRateRange] Transformed clip not ready for: '{original.name}'");
                        return false;
                    }
                }
                else
                {
                    if (!CacheObject.TryGetTransformed(weaponId, original, out transformed))
                    {
                        log.Add($"[TRACK ⏳] Transformed clip not ready for: '{original.name}'");
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Enregistre le nom transformé pour un accès futur.
            /// </summary>
            private static void RegisterTransformedClip(string baseName, AudioClip transformed, List<string> log, bool isOverClock)
            {
                if (isOverClock)
                {
                    CacheObject.RegisterLocalNameOverClok(baseName, transformed.name);
                    log.Add($"🎧 [FireRateRange] Registered transformed clip name: {transformed.name}");
                }
                else
                {
                    CacheObject.RegisterLocalName(baseName, transformed.name);
                    log.Add($"🎧 Registered transformed clip name: {transformed.name}");
                }
            }

            /// <summary>
            /// Ajoute les informations détaillées des clips au journal.
            /// </summary>
            private static void LogClipTransformation(AudioClip original, AudioClip transformed, List<string> log, string clipName, string weaponId)
            {
                AudioClipInspector.Inspect(original, "🎧 Original: ", log);
                AudioClipInspector.Inspect(transformed, "🎧 Replacement: ", log);
                log.Add($"🎧 ✅ Replacement: '{clipName}' → '{transformed.name}' Weapon: {weaponId}");
            }
        }
    }
}