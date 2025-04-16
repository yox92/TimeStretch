using System.IO;

namespace TimeStretch.Utils
{
    public static class PathsFile
    {
        public static readonly string FireRatePath = Path.Combine(
            BepInEx.Paths.PluginPath, "TimeStretch", "fireRates.json");
        
        public static readonly string TimeStretch = Path.Combine(
            BepInEx.Paths.PluginPath, "TimeStretch");

        public static readonly string ClassDataPath = Path.Combine(
            BepInEx.Paths.PluginPath, "TimeStretch", "classdata.tpk");

        public static readonly string GameFolder = Path.Combine(
            BepInEx.Paths.PluginPath, "..", "..");

        public static readonly string BaseAudioPath = Path.Combine(
            GameFolder, "EscapeFromTarkov_Data", "StreamingAssets", "Windows",
            "assets", "content", "audio");

        public static readonly string BaseAssetsPath = Path.Combine(
            GameFolder, "EscapeFromTarkov_Data", "StreamingAssets", "Windows",
            "assets");

        public static readonly string BaseAudioWeaponsPath = Path.Combine(
            GameFolder, "EscapeFromTarkov_Data", "StreamingAssets", "Windows",
            "assets", "content", "audio", "weapons");

        public static readonly string BaseAudioBanksPath = Path.Combine(
            GameFolder, "EscapeFromTarkov_Data", "StreamingAssets", "Windows",
            "assets", "content", "audio", "banks");

        public static readonly string BaseWeaponsPath = Path.Combine(
            GameFolder, "EscapeFromTarkov_Data", "StreamingAssets", "Windows",
            "assets", "content", "weapons");

        public static readonly string LogFilePath = Path.Combine(
            BepInEx.Paths.PluginPath, "TimeStretch", "batch_log.txt");

        public static readonly string DebugPath = Path.Combine(
            BepInEx.Paths.PluginPath, "TimeStretch", "debug.cfg");
    }
}