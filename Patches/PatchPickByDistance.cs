using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using EFT;
using HarmonyLib;
using EFT.InventoryLogic;
using TimeStretch.Entity;
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

                    if (!CacheObject.IsHookAllowedForWeapon(CurrentWeapon.TemplateId))
                    {
                        if (CacheObject.TryLogOnce($"hook_skip:{CurrentWeapon.TemplateId}"))
                            BatchLogger.Log(
                                $"⛔ [PatchPickByDistance] Hook skipped for {CurrentWeapon.TemplateId} (mod == false)");
                        return;
                    }


                    string weaponId = CurrentWeapon.TemplateId;
                    List<string> log = [];

                    clip1 = ReplaceIfTransformed(clip1, weaponId, log);
                    clip2 = ReplaceIfTransformed(clip2, weaponId, log);

                    BatchLogger.FlushClipLog(log, weaponId);
                }
                catch (Exception ex)
                {
                    BatchLogger.Log($"🤮 Erreur dans PickClipsByDistance Postfix : {ex.Message}");
                }
            }

            private static AudioClip ReplaceIfTransformed(AudioClip original, string weaponId, List<string> log)
            {
                if (original == null)
                    return null;

                var clipName = original.name;
                var baseName = CacheObject.RemoveSuffix(clipName);

                log.Add($"---------------------------");

                if (string.IsNullOrWhiteSpace(weaponId))
                {
                    log.Add($"[PatchPickByDistance] ❌ No player or weapon equipped");
                    return original;
                }

                // cache local (nom → nom) + (nom → AudioClip) Rapide peu couteuse
                if (CacheObject.TryGetLocalName(baseName, out var transformedName))
                {
                    if (CacheObject.TryLogOnce($"seen:{weaponId}:{baseName}"))
                    {
                        log.Add($"🎧 🔎 [PatchPickByDistance] [CACHE] already fired");
                    }

                    if (CacheObject.TryResolveFromName(transformedName, out var transformedClip))
                    {
                        return transformedClip;
                    }

                    log.Add($"❌ [PatchPickByDistance] [CACHE] Clip cached [fast] but not yet visible");
                }
                else
                {
                    log.Add($" [PatchPickByDistance][CACHE] Weapon has never fired");
                }

                // vérifie dans le store si l’arme est modifiable
                if (!JsonCache.TryGetEntry(weaponId, out var entry) || !entry.Mod)
                {
                    log.Add($"[PatchPickByDistance] ❌ Arme {weaponId} not marked as modifiable ");
                    return original;
                }
                
                
                

                // regarde si un clip transformé est dispo
                if (!CacheObject.TryGetTransformed(weaponId, original, out var transformed))
                {
                    log.Add($"[TRACK ⏳ Transformed clip not ready for : '{clipName}']");
                    return original;
                }

                // 🧠 Enregistre le nom transformé pour usage futur (nom → nom)
                CacheObject.RegisterLocalName(baseName, transformed.name);

                AudioClipInspector.Inspect(original, "🎧 Original :", log);
                AudioClipInspector.Inspect(transformed, "🎧 Replacement :", log);
                log.Add($"🎧 ✅ Replacement : '{clipName}' → '{transformed.name}' Weapon : {weaponId}");

                return transformed;
            }
        }
    }
}