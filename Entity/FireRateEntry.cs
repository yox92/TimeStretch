using System.Collections.Generic;

namespace TimeStretch.Entity
{
    public class FireRateEntry
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public int FireRate { get; set; }
        public float FireRateMod { get; set; }
        public bool HasFullAuto { get; set; }
        public AudioData Audio { get; set; }
        public bool Mod { get; set; } // Tu peux aussi l'enlever si inutilisé
    }

    public class AudioData
    {
        public Dictionary<string, AudioClipInfo> Clips { get; set; }
    }

    public class AudioClipInfo
    {
        public string PathID { get; set; }
    }
}