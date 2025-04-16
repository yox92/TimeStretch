using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET.Extra;

namespace ChangeMetaDataBundle
{
    public static class BundleReader
    {
        public static void ReadAudioClip(
            string bundlePath,
            string classDataPath,
            List<string> clipNames,
            string phase,
            List<string> localLog)
        {
            localLog.Add($"\n🔍 [{phase}] Lecture passive du bundle : {Path.GetFileName(bundlePath)}");

            if (!File.Exists(bundlePath) || !File.Exists(classDataPath))
            {
                localLog.Add($"❌ Fichier introuvable : {bundlePath} ou {classDataPath}");
                return;
            }

            try
            {
                var manager = new AssetsManager();
                manager.LoadClassPackage(classDataPath);
                var bundleInst = manager.LoadBundleFile(bundlePath);
                var bundleFile = bundleInst.file;

                var clipSet = new HashSet<string>(clipNames);
                var dirCount = 0;

                foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
                {
                    dirCount++;
                    if (dirInfo.Name.EndsWith(".resource"))
                        continue;

                    var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
                    var assetsFile = assetsInst.file;

                    foreach (var assetInfo in assetsFile.Metadata.AssetInfos)
                    {
                        if (assetInfo.TypeId != 83) // AudioClip
                            continue;

                        var baseField = manager.GetBaseField(assetsInst, assetInfo);
                        var nomClip = baseField["m_Name"].AsString;

                        if (!clipSet.Contains(nomClip))
                            continue;

                        var preload = baseField["m_PreloadAudioData"].AsBool;
                        var loadType = baseField["m_LoadType"].AsInt;
                        var preloadBack = baseField["m_LoadInBackground"].AsBool;

                        localLog.Add(
                            $"🔎 Clip : {nomClip} | Preload = {preload} | LoadType = {loadType} | preloadBack = {preloadBack}");
                    }
                }

                manager.UnloadBundleFile(bundleInst);
            }
            catch (Exception ex)
            {
                localLog.Add($"❌ Exception pendant lecture passive : {ex.Message}");
            }
        }

        public static bool ShouldModifyBundleByName(
            string bundlePath,
            string classDataPath,
            string clipName,
            string phase,
            List<string> localLog)
        {
            localLog.Add($"\n🔍 [{phase}] Vérification du bundle : {Path.GetFileName(bundlePath)}");

            if (!File.Exists(bundlePath) || !File.Exists(classDataPath))
            {
                localLog.Add($"❌ Fichier manquant : {bundlePath} ou {classDataPath}");
                return false;
            }

            try
            {
                var manager = new AssetsManager();
                manager.LoadClassPackage(classDataPath);
                var bundleInst = manager.LoadBundleFile(bundlePath);
                var bundleFile = bundleInst.file;

                foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
                {
                    if (dirInfo.Name.EndsWith(".resource"))
                        continue;

                    var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
                    var assetsFile = assetsInst.file;

                    foreach (var assetInfo in assetsFile.Metadata.AssetInfos)
                    {
                        if (assetInfo.TypeId != 83) // AudioClip uniquement
                            continue;

                        var baseField = manager.GetBaseField(assetsInst, assetInfo);
                        var nom = baseField["m_Name"].AsString;

                        if (!string.Equals(nom, clipName, StringComparison.Ordinal))
                            continue;

                        var preload = baseField["m_PreloadAudioData"].AsBool;
                        var loadType = baseField["m_LoadType"].AsInt;
                        var preloadBack = baseField["m_LoadInBackground"].AsBool;

                        localLog.Add(
                            $"🔎 {clipName} | Preload = {preload} | LoadType = {loadType} | preloadBack = {preloadBack}");

                        if (AudioClipOriginalValues.IsCurrentStateExpected(preload, preloadBack, loadType))
                        {
                            localLog.Add($"✅ {clipName} is already in expected state → {AudioClipOriginalValues.DescribeExpectedValues()}");
                            continue;
                        }

                        string action = AudioClipOriginalValues.Mode == AudioClipMode.Modified ? "Modification" : "Restoration";
                        localLog.Add($"🔧 {action} required for: {clipName} → Current: preload={preload}, preloadBack={preloadBack}, loadType={loadType}");

                        manager.UnloadBundleFile(bundleInst);
                        return true;
                    }
                }

                manager.UnloadBundleFile(bundleInst);
                localLog.Add($"✅ Aucun changement nécessaire pour {clipName} dans : {Path.GetFileName(bundlePath)}");
                return false;
            }
            catch (Exception ex)
            {
                localLog.Add($"❌ Exception lors de la vérification : {ex.Message}");
                return false;
            }
        }

        public static List<string> ReadAudioClipNames(string bundlePath, string classdataPath)
        {
            var result = new List<string>();

            if (!File.Exists(bundlePath) || !File.Exists(classdataPath))
                return result;

            var manager = new AssetsManager();
            manager.LoadClassPackage(classdataPath);
            var bundleInst = manager.LoadBundleFile(bundlePath);
            var bundleFile = bundleInst.file;

            foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
            {
                if (!dirInfo.Name.EndsWith(".assets", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
                var assetsFile = assetsInst.file;

                foreach (var assetInfo in assetsFile.Metadata.AssetInfos)
                {
                    switch (assetInfo.TypeId)
                    {
                        // AudioClip = 83
                        case 83:
                        {
                            var baseField = manager.GetBaseField(assetsInst, assetInfo);
                            result.Add(baseField["m_Name"].AsString);
                            break;
                        }
                        // SoundBank = MonoBehaviour (114)
                        case 114:
                        {
                            var baseField = manager.GetBaseField(assetsInst, assetInfo);
                            var className = baseField["m_Name"].AsString;
                            if (!className.Contains("SoundBank"))
                                continue;

                            try
                            {
                                var envArray = baseField["Environments"];
                                foreach (var env in envArray.Children)
                                {
                                    var distArray = env["Clips"];
                                    foreach (var dist in distArray.Children)
                                    {
                                        var clipArray = dist["Clips"];
                                        foreach (var clipField in clipArray.Children)
                                        {
                                            string clipName = clipField["m_Name"].AsString;
                                            if (!string.IsNullOrWhiteSpace(clipName))
                                                result.Add(clipName);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // ignore malformed SoundBanks
                            }

                            break;
                        }
                    }
                }
            }

            manager.UnloadBundleFile(bundleInst);
            return result.Distinct().OrderBy(x => x).ToList();
        }

        public static bool ContainsAudioClipByName(string bundlePath, string classdataPath, string clipName)
        {
            if (!File.Exists(bundlePath) || !File.Exists(classdataPath))
                return false;

            try
            {
                var manager = new AssetsManager();
                manager.LoadClassPackage(classdataPath);
                var bundleInst = manager.LoadBundleFile(bundlePath);
                var bundleFile = bundleInst.file;

                foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
                {
                    if (dirInfo.Name.EndsWith(".resource"))
                        continue;

                    var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
                    var assetsFile = assetsInst.file;

                    foreach (var assetInfo in assetsFile.Metadata.AssetInfos)
                    {
                        if (assetInfo.TypeId != 83) // AudioClip uniquement
                            continue;

                        var baseField = manager.GetBaseField(assetsInst, assetInfo);
                        string nom = baseField["m_Name"].AsString;

                        if (string.Equals(nom, clipName, StringComparison.Ordinal))
                        {
                            manager.UnloadBundleFile(bundleInst);
                            return true;
                        }
                    }
                }

                manager.UnloadBundleFile(bundleInst);
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}