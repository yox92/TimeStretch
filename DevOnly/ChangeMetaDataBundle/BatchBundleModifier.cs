using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ChangeMetaDataBundle
{
    public class BatchBundleModifier
    {
        private static readonly ConcurrentDictionary<string, object> FileLocks = new();

        private static readonly string RootPrefix =
            $"assets{Path.DirectorySeparatorChar}content{Path.DirectorySeparatorChar}";

        public void ReadApplyChange()
        {
            if (!File.Exists(PathsFile.FireRatePath))
            {
                BatchLogger.Error($"Fichier fireRates.json introuvable à : {PathsFile.FireRatePath}");
                return;
            }

            var json = JObject.Parse(File.ReadAllText(PathsFile.FireRatePath));
            var bundleClipMap = new ConcurrentDictionary<string, HashSet<string>>();
            

            // Étape 1 : Construction du mapping bundle -> clips
            foreach (var prop in json.Properties())
            {
                if (prop.Value is not JObject weapon)
                    continue;

                if (weapon["audio"]?["clips"] is not JObject clips)
                    continue;

                foreach (var clipProp in clips.Properties())
                {
                    if (clipProp.Value is not JObject clipInfo)
                        continue;
                    var clipName = clipProp.Name;

                    var paths = (clipInfo["bundle"]?.ToObject<List<string>>() ?? [])
                        .Concat(clipInfo["bank"]?.ToObject<List<string>>() ?? []);

                    foreach (var path in paths)
                    {
                        if (!bundleClipMap.ContainsKey(path))
                            bundleClipMap[path] = [];

                        bundleClipMap[path].Add(clipName);
                    }
                }
            }
            
            int total = bundleClipMap.Count;
            var current = 0;
            object progressLock = new();
            // Étape 2 : Traitement optimisé par fichier
            Parallel.ForEach(bundleClipMap, entry =>
            {
                lock (progressLock)
                {
                    current++;
                    DrawProgressBar(current, total);
                }
                try
                {
                    var bundlePathFromJson = entry.Key;
                    var clipNames = entry.Value;

                    var localLog = new List<string>();
                    var fullBundlePath = ResolveFullBundlePath(bundlePathFromJson);
                    var fileLock = FileLocks.GetOrAdd(fullBundlePath, _ => new object());

                    lock (fileLock)
                    {
                        var neededClips = clipNames
                            .Where(clip =>
                                BundleReader.ContainsAudioClipByName(fullBundlePath, PathsFile.ClassDataPath, clip))
                            .Where(clip => BundleReader.ShouldModifyBundleByName(fullBundlePath,
                                PathsFile.ClassDataPath, clip, "AVANT", localLog))
                            .ToList();

                        if (neededClips.Count == 0)
                        {
                            localLog.Add($"✅ Aucun clip à modifier dans {Path.GetFileName(fullBundlePath)}");
                            BatchLogger.Block(localLog);
                            return;
                        }

                        localLog.Add(
                            $"🛠 Modification de {neededClips.Count} clips dans {Path.GetFileName(fullBundlePath)}");

                        BundleModifier.ModifierChampsAudioClipByName(
                            fullBundlePath,
                            PathsFile.ClassDataPath,
                            neededClips,
                            localLog);

                        BundleReader.ReadAudioClip(fullBundlePath, PathsFile.ClassDataPath, neededClips, "APRES",
                            localLog);
                        BatchLogger.Block(localLog);
                    }
                }
                catch (Exception ex)
                {
                    BatchLogger.Block(new List<string>
                    {
                        $"❌ Erreur pendant traitement bundle : {entry.Key}",
                        $"📌 {ex.Message}",
                        $"📍 StackTrace: {ex.StackTrace}"
                    });
                }
            });
        }

        private static string ResolveFullBundlePath(string bundlePathFromJson)
        {
            if (string.IsNullOrWhiteSpace(bundlePathFromJson))
                throw new ArgumentException("Le chemin du bundle JSON ne peut pas être vide.",
                    nameof(bundlePathFromJson));

            var normalizedPath = bundlePathFromJson.Replace('/', Path.DirectorySeparatorChar);

            if (!normalizedPath.StartsWith(RootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Le Fichier doit commencer par : \"assets/content/\" : {bundlePathFromJson}");

            var relativePath = normalizedPath.Substring(RootPrefix.Length);

            if (relativePath.StartsWith("audio" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var pathInAudio = relativePath.Substring("audio".Length + 1);
                return Path.Combine(PathsFile.BaseAudioPath, pathInAudio);
            }

            if (relativePath.StartsWith("weapons" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var pathInWeapons = relativePath.Substring("weapons".Length + 1);
                return Path.Combine(PathsFile.BaseWeaponsPath, pathInWeapons);
            }

            throw new ArgumentException(
                $"Le chemin relatif ne commence pas par \"audio/\" ou \"weapons/\" après \"assets/content/\" : {bundlePathFromJson}");
        }
        private static void DrawProgressBar(int current, int total, int barSize = 40)
        {
            if (total == 0) return; // sécurité anti-division par 0

            var progress = Math.Clamp((double)current / total, 0, 1);
            var filledBars = Math.Clamp((int)(progress * barSize), 0, barSize);
            var emptyBars = barSize - filledBars;
            var percentage = $"{(int)(progress * 100)}%".PadLeft(4);

            var bar = "[" + new string('#', filledBars) + new string('-', emptyBars) + $"] {percentage} ({current}/{total})";
            Console.CursorLeft = 0;
            Console.Write(bar);
            if (current == total)
                Console.WriteLine();
        }

    }
}