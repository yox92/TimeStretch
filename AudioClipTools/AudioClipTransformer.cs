using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SoundTouch;
using TimeStretch.Utils;
using UnityEngine;


namespace TimeStretch.AudioClipTools
{
    public static class AudioClipTransformer
    {
        public static async Task<AudioClip> TransformAsync(AudioClip original, float tempoChangePercent, List<string> log, string weaponId)
        {
            if (original == null)
            {
                log.Add("[AudioClipTransformer] 🤮 Clip null reçu en entrée");
                return original;
            }

            if (original.loadState != AudioDataLoadState.Loaded)
            {
                log.Add($"[AudioClipTransformer] ❌ Clip '{original.name}' non chargé (loadState = {original.loadState})");
                return original;
            }

            if (original.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                log.Add($"[AudioClipTransformer] ⛔ Clip '{original.name}' n'est pas en DecompressOnLoad (loadType = {original.loadType})");
                return original;
            }

            var channels = original.channels;
            var sampleRate = original.frequency;
            var sampleCount = original.samples;
            var originalData = new float[sampleCount * channels];
            log.Add($"[AudioClipTransformer] ⚡ '{original.name}' — loadType={original.loadType}, loadState={original.loadState}, channels={original.channels}, freq={original.frequency}, samples={original.samples}");

            if (!original.GetData(originalData, 0))
            {
                log.Add($"[AudioClipTransformer] ❌ Impossible de lire les données de '{original.name}'");
                return original;
            }

            float[] processedData = await Task.Run(() =>
            {
                var processor = new SoundTouchProcessor
                {
                    Channels = channels,
                    SampleRate = sampleRate,
                    TempoChange = tempoChangePercent,
                    PitchSemiTones = 0,
                    RateChange = 0
                };

                processor.PutSamples(originalData, sampleCount);
                processor.Flush();

                return CollectAllSamples(processor);
            });

            // Retour au thread principal pour AudioClip.Create()
            int newSampleCount = processedData.Length / channels;
            var newClip = AudioClip.Create(original.name + "_mod", newSampleCount, channels, sampleRate, false);
            bool success = newClip.SetData(processedData, 0);

            if (!success)
            {
                log.Add($"[AudioClipTransformer] ❌ SetData a échoué pour '{newClip.name}'");
                return original;
            }

            AudioClipModifier.RegisterReplacement(original, newClip, log, weaponId);
            return newClip;
        }


        private static float[] CollectAllSamples(SoundTouchProcessor processor)
        {
            int frameBlockSize = 1024;
            int maxChannels = processor.Channels;
            Span<float> buffer = stackalloc float[frameBlockSize * maxChannels];

            List<float[]> chunks = new();
            int totalSamples = 0;

            while (true)
            {
                int receivedFrames = processor.ReceiveSamples(buffer, frameBlockSize);
                if (receivedFrames == 0)
                    break;

                int samples = receivedFrames * maxChannels;
                float[] chunk = new float[samples];

                for (int i = 0; i < samples; i++)
                    chunk[i] = buffer[i];

                chunks.Add(chunk);
                totalSamples += samples;
            }

            // Fusion en une seule fois
            float[] final = new float[totalSamples];
            int offset = 0;
            foreach (var chunk in chunks)
            {
                Buffer.BlockCopy(chunk, 0, final, offset * sizeof(float), chunk.Length * sizeof(float));
                offset += chunk.Length;
            }

            return final;
        }
    }
}