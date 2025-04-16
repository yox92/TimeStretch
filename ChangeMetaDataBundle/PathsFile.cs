using System;
using System.IO;

namespace ChangeMetaDataBundle
{
    public static class PathsFile
    {
        // ✅ Dossier du jeu (celui contenant EscapeFromTarkov.exe)
        public static readonly string GameFolder = LocateOrAskGameDirectory();

        // 📁 BepInEx/plugins/TimeStretch
        public static readonly string PluginFolder = Path.Combine(GameFolder, "BepInEx", "plugins", "TimeStretch");

        // 📁 Fichiers de configuration
        public static readonly string FireRatePath = Path.Combine(PluginFolder, "fireRates.json");
        public static readonly string ClassDataPath = Path.Combine(PluginFolder, "classdata.tpk");
        public static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "batch_log.txt");
        public static readonly string DebugPath = Path.Combine(PluginFolder, "debug.cfg");

        // 📁 Dossiers audio
        public static readonly string BaseAssetsPath = Path.Combine(GameFolder, "EscapeFromTarkov_Data", "StreamingAssets", "Windows", "assets");
        public static readonly string BaseAudioPath = Path.Combine(BaseAssetsPath, "content", "audio");
        public static readonly string BaseAudioBanksPath = Path.Combine(BaseAudioPath, "banks");
        public static readonly string BaseAudioWeaponsPath = Path.Combine(BaseAudioPath, "weapons");
        public static readonly string BaseWeaponsPath = Path.Combine(BaseAssetsPath, "content", "weapons");

        // 🔍 Fonction de recherche du dossier du jeu
        private static string LocateOrAskGameDirectory()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current != null)
            {
                var tarkovExe = Path.Combine(current.FullName, "EscapeFromTarkov.exe");
                if (File.Exists(tarkovExe))
                    return current.FullName;

                current = current.Parent;
            }

            Console.WriteLine("🔍 Could not locate EscapeFromTarkov.exe automatically.");
            Console.WriteLine("📁 A folder selection window will now open.");

            string? selected = FolderSelector.AskUserForTarkovRoot();

            if (!string.IsNullOrEmpty(selected) && File.Exists(Path.Combine(selected, "EscapeFromTarkov.exe")))
                return selected;

            throw new DirectoryNotFoundException("❌ Tarkov root folder not found or EscapeFromTarkov.exe missing.");
        }    }
}
