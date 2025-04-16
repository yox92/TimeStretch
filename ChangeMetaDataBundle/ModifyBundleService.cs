using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace ChangeMetaDataBundle
{
    public static class BundleModifier
    {
        private static readonly HashSet<string> BundlesDejaTraites = new();

        public static void ModifierChampsAudioClipByName(
            string bundlePath,
            string classdataPath,
            List<string> clipNames,
            List<string> localLog)
        {
            if (!BundlesDejaTraites.Add(bundlePath))
            {
                localLog.Add($"⚠️ Le bundle {bundlePath} a déjà été traité !");
                System.Diagnostics.Debug.Assert(false, $"Le bundle {bundlePath} a été re-traité deux fois !");
            }

            localLog.Add($"🔍 Traitement du bundle : {bundlePath}");
            if (!File.Exists(bundlePath))
            {
                localLog.Add($"❌ Fichier bundle introuvable : {bundlePath}");
                return;
            }

            if (!File.Exists(classdataPath))
            {
                localLog.Add($"❌ classdata.tpk introuvable : {classdataPath}");
                return;
            }

            try
            {
                var manager = new AssetsManager();
                manager.LoadClassPackage(classdataPath);
                var bundleInst = manager.LoadBundleFile(bundlePath);
                var bundleFile = bundleInst.file;

                var bundleModifie = false;
                var clipSet = new HashSet<string>(clipNames);

                int totalAssets = 0;
                foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
                {
                    if (dirInfo.Name.EndsWith(".resource"))
                        continue;

                    var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, dirInfo.Name);
                    var assetsFile = assetsInst.file;

                    var aEteModifie = false;

                    foreach (var assetInfo in assetsFile.Metadata.AssetInfos)
                    {
                        if (assetInfo.TypeId != 83) // AudioClip
                            continue;

                        var baseField = manager.GetBaseField(assetsInst, assetInfo);
                        var nom = baseField["m_Name"].AsString;

                        if (!clipSet.Contains(nom))
                            continue;

                        localLog.Add($"✅ Clip ciblé : {nom}");

                        AudioClipOriginalValues.ApplyExpectedValues(baseField);
                        localLog.Add($"🛠 Applied values: {AudioClipOriginalValues.DescribeExpectedValues()}");

                        using var memStream = new MemoryStream();
                        var writer = new AssetsFileWriter(memStream);
                        baseField.Write(writer);

                        assetInfo.SetNewData(memStream.ToArray());
                        localLog.Add($"🧬 Champ modifié : {nom} → Preload=✔️, LoadType=0, PreloadBack=❌");
                        aEteModifie = true;
                        totalAssets++;
                    }

                    if (aEteModifie)
                    {
                        using var assetStream = new MemoryStream();
                        var writer = new AssetsFileWriter(assetStream);
                        assetsFile.Write(writer);

                        dirInfo.Replacer = new ContentReplacerFromBuffer(assetStream.ToArray());
                        bundleModifie = true;

                        localLog.Add($"📝 Modifications écrites dans {dirInfo.Name}");
                    }
                }

                localLog.Add($"📦 Total clips modifiés : {totalAssets}");

                if (bundleModifie)
                {
                    var tempPath = bundlePath + ".temp";
                    try
                    {

                        try
                        {
                            using var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                            var writer = new AssetsFileWriter(tempStream);
                            bundleFile.Write(writer);
                        }
                        catch (Exception exWrite)
                        {
                            localLog.Add($"❌ Erreur lors de l'écriture du bundle modifié : {exWrite.Message}");
                            throw;
                        }

                        manager.UnloadBundleFile(bundleInst);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        WaitForFileAccess(bundlePath);


                        try
                        {
                            File.Delete(bundlePath);
                        }
                        catch (Exception exDelete)
                        {
                            localLog.Add($"❌ Erreur lors de la suppression du fichier original : {exDelete.Message}");
                            throw;
                        }

                        try
                        {
                            File.Move(tempPath, bundlePath);
                        }
                        catch (Exception exMove)
                        {
                            localLog.Add($"❌ Erreur lors du déplacement du fichier temporaire : {exMove.Message}");
                            throw;
                        }

                        manager.UnloadBundleFile(bundleInst);
                        localLog.Add($"🎉 Bundle sauvegardé : {bundlePath}");
                    }
                    catch (Exception ex)
                    {
                        localLog.Add($"❌ Erreur de sauvegarde : {ex.Message}");
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }
                }
                else
                {
                    manager.UnloadBundleFile(bundleInst);
                    localLog.Add($"ℹ️ Aucun clip à modifier dans ce bundle.");
                }
            }
            catch (Exception ex)
            {
                localLog.Add($"❌ Exception globale : {ex.Message}\n📌 StackTrace: {ex.StackTrace}");
            }
        }

        private static void WaitForFileAccess(string filePath, int timeoutMs = 10000, int pollDelayMs = 50)
        {
            var start = DateTime.UtcNow;
            int retryCount = 0;

            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        return;
                }
                catch (IOException)
                {
                    retryCount++;
                    if (retryCount % 10 == 0)
                    {
                        Console.WriteLine($"⏳ [{DateTime.Now:HH:mm:ss.fff}] Toujours bloqué après {retryCount} essais sur : {filePath} (Thread={Thread.CurrentThread.ManagedThreadId})");
                    }

                    Thread.Sleep(pollDelayMs);
                }
            }

            throw new IOException($"⏳ Timeout d'accès au fichier après {retryCount} essais : {filePath}");
        }
    }
}