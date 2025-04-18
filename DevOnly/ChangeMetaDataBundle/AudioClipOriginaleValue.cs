using AssetsTools.NET;

namespace ChangeMetaDataBundle
{
    public enum AudioClipMode
    {
        Original,
        Modified
    }

    public static class AudioClipOriginalValues
    {
        public static AudioClipMode Mode { get; set; } = AudioClipMode.Modified;

        // Vanilla
        private const bool OriginalPreload = false;
        private const bool OriginalPreloadBack = false;
        private const int OriginalLoadType = 2;

        // Modifié
        private const bool ModifiedPreload = true;
        private const bool ModifiedPreloadBack = false;
        private const int ModifiedLoadType = 0;

        // Actifs selon le mode
        public static bool ExpectedPreload =>
            Mode == AudioClipMode.Original ? OriginalPreload : ModifiedPreload;

        public static bool ExpectedPreloadBack =>
            Mode == AudioClipMode.Original ? OriginalPreloadBack : ModifiedPreloadBack;

        public static int ExpectedLoadType =>
            Mode == AudioClipMode.Original ? OriginalLoadType : ModifiedLoadType;

        public static bool IsCurrentStateExpected(bool preload, bool preloadBack, int loadType)
        {
            return preload == ExpectedPreload
                   && preloadBack == ExpectedPreloadBack
                   && loadType == ExpectedLoadType;
        }

        public static void ApplyExpectedValues(AssetTypeValueField baseField)
        {
            baseField["m_PreloadAudioData"].AsBool = ExpectedPreload;
            baseField["m_LoadType"].AsInt = ExpectedLoadType;
            baseField["m_LoadInBackground"].AsBool = ExpectedPreloadBack;
        }

        public static string DescribeExpectedValues()
        {
            return $"Preload={ExpectedPreload}, LoadInBackground={ExpectedPreloadBack}, LoadType={ExpectedLoadType}";
        }
    }
}