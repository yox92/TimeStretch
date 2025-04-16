using System;
using System.Collections.Generic;
using UnityEngine;


namespace TimeStretch
{
    public static class AudioClipInspector
    {
        public static void Inspect(AudioClip clip, string header, List<string> log)
        {
            if (clip == null)
            {
                log.Add("[AudioClipInspector] 🔍 AudioClip NULL, impossible d'inspecter.");
                return;
            }

            try
            {
                log.Add(header);
                log.Add($"[AudioClipInspector] 🎧 Clip : {clip.name}, Longueur : {clip.length} sec");

                float[] data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0))
                {
                    log.Add("[AudioClipInspector] ⚠️ Impossible de lire les données PCM (peut-être compressé ou streamé)");
                }
            }
            catch (Exception ex)
            {
                log.Add($"[AudioClipInspector] 🤮 Erreur pendant l'inspection de '{clip.name}' : {ex.Message}");
            }
        }
    }
}
