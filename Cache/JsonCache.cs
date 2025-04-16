using System.Collections.Generic;
using System.Linq;
using TimeStretch.Entity;
using TimeStretch.Utils;
using UnityEngine;

namespace TimeStretch.Entity
{
    public static class JsonCache
    {
        private static Dictionary<string, FireRateEntry> _entries = new();
        private static Dictionary<string, FireRateEntry> _data;

        public static bool IsInitialized => _data != null && _data.Count > 0;
        
        public static void LoadJsonFireRate()
        {
            BatchLogger.Info("♻ Chargement des FireRate intégrés (Embedded)");

            _entries = EmbeddedFireRateData.All;

            int totalClips = 0;
            int totalWeapons = 0;

            foreach (var entry in _entries.Values)
            {
                if (CacheObject.WeaponFireRates.TryGetValue(entry.ID, out var moddedRate))
                {
                    if (!Mathf.Approximately(moddedRate, entry.FireRate) && moddedRate > 0)
                    {
                        entry.FireRateMod = moddedRate;
                        entry.Mod = true;
                        BatchLogger.Log($"🔧 Mod détecté : {entry.ID} | Vanilla = {entry.FireRate}, Mod = {moddedRate}");
                    }
                    else
                    {
                        entry.Mod = false;
                    }
                }
                else
                {
                    entry.Mod = false;
                }

                // Comptage si l’arme a été modifiée et possède des clips
                if (entry.Mod && entry.Audio?.Clips != null)
                {
                    totalWeapons++;
                    totalClips += entry.Audio.Clips.Count;
                }
            }

            BatchLogger.Info("✅ Données FireRate intégrées chargées.");
            BatchLogger.Info(" 📊 Récapitulatif :");
            BatchLogger.Info($"   🔫 Armes modifiées : {totalWeapons}");
            BatchLogger.Info($"   🎧 Clips enregistrés : {totalClips}");

            _data = _entries;
            
            CacheObject.WeaponFireRates.Clear();
            BatchLogger.Info("🧹 Cache 'WeaponFireRates' vidé après fusion.");
        }

        public static bool IsTrackedClip(string clipName)
        {
            clipName = CacheObject.RemoveSuffix(clipName);
            return _entries.Values.Any(e => e.Audio?.Clips?.ContainsKey(clipName) == true);
        }

        public static float GetTempoModifier(string weaponId, string clipName)
        {
            var name = CacheObject.RemoveSuffix(clipName);

            if (!_entries.TryGetValue(weaponId, out var entry) || entry.Audio?.Clips?.ContainsKey(name) != true)
            {
                BatchLogger.Info($"⚠ Aucun clip enregistré pour '{clipName}' (baseName: '{name}')");
                return 0f;
            }

            if (!entry.Mod || entry.FireRateMod <= 0 || entry.FireRate <= 0)
            {
                BatchLogger.Warn($"⚠️ Aucun mod actif ou données invalides pour l'arme {weaponId}");
                return 0f;
            }

            float tempo = ((entry.FireRateMod / (float)entry.FireRate) - 1f) * 100f;
            BatchLogger.Info($"🎼 Tempo : '{clipName}' = {tempo:+0.00;-0.00}% (mod={entry.FireRateMod}, ref={entry.FireRate})");
            return tempo;
        }
        
        public static FireRateEntry? GetEntryByClip(string clipName)
        {
            clipName = CacheObject.RemoveSuffix(clipName);
            return _entries.Values.FirstOrDefault(e => e.Audio?.Clips?.ContainsKey(clipName) == true);
        }
        public static IEnumerable<FireRateEntry> AllEntries => _entries.Values;

        public static string[] GetAllTrackedClipNames()
        {
            return _data
                .Values
                .Where(e => e.Audio?.Clips != null)
                .SelectMany(e => e.Audio.Clips.Keys)
                .Distinct()
                .ToArray();
        }
        
        public static bool TryGetEntry(string weaponId, out FireRateEntry entry)
        {
            return _entries.TryGetValue(weaponId, out entry);
        }

    }
}
