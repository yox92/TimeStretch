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
        public static async Task<AudioClip> TransformAsync(AudioClip original, float tempoChangePercent,
            List<string> log, string weaponId)
        {
            log.Add(
                $"[AudioClipTransformer] 🟡 Start transform '{original?.name ?? "null"}' weapon {weaponId}");

            if (original == null)
            {
                log.Add("[AudioClipTransformer] ❌ Clip null");
                return original;
            }

            log.Add($"[AudioClipTransformer] 📋 Clip Infos:");
            log.Add($"  ├─ Name: {original.name}");
            log.Add($"  ├─ LoadState: {original.loadState}");
            log.Add($"  ├─ LoadType: {original.loadType}");
            log.Add($"  ├─ PreloadAudioData: {original.preloadAudioData}");
            log.Add($"  ├─ LoadInBackground: {original.loadInBackground}");
            log.Add($"  ├─ Samples: {original.samples}");
            log.Add($"  └─ Length (sec): {original.length}");

            var channels = original.channels;
            var sampleRate = original.frequency;
            var sampleCount = original.samples;
            var originalData = new float[sampleCount * channels];

            log.Add($"[AudioClipTransformer] 📊 Extraction data : {sampleCount} samples × {channels} canaux");
            
            if (!await EnsureClipIsLoadedAsync(original, 500)) return original;
            try
            {
                original.GetData(originalData, 0);
            }
            catch (Exception ex)
            {
                BatchLogger.Error($"[AudioClipTransformer]🔴 Error GetData on unload AudioClip: {ex}");
                return original;
            }
           
            if (originalData.Length > 0 && Math.Abs(originalData[0]) > 0.00001f) 
                log.Add($"🔊 PCM {original.name} — first sample = {originalData[0]:F6} (total {originalData.Length} samples)");
            else
                log.Add($"🔇 PCM {original.name} empty (originalData[0] = {originalData[0]:F6})");
            
            log.Add($"[AudioClipTransformer] 🔁 SoundTouch treatment (tempo +{tempoChangePercent}%)");
            
            
            var processedData = await Task.Run(() =>
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

            var newSampleCount = processedData.Length / channels;

            log.Add($"[AudioClipTransformer] ✅ Traitement Finish — new data : {processedData.Length} samples / {newSampleCount} frames");

            try
            {
                var newClip = AudioClip.Create(original.name + "_mod", newSampleCount, channels, sampleRate, false);

                if (!newClip.SetData(processedData, 0))
                {
                    BatchLogger.Error($"[AudioClipTransformer]❌ SetData() failed for '{newClip.name}', returning original clip.");
                    return original;
                }

                BatchLogger.Log($"[AudioClipTransformer]🎉 New clip '{newClip.name}' created successfully !");
                AudioClipModifier.RegisterReplacement(original, newClip, log, weaponId);
                return newClip;
            }
            catch (Exception ex)
            {
                BatchLogger.Error($"[AudioClipTransformer]🔴 Exception during clip creation or SetData: {ex}");
                return original;
            }
        }

        private static async Task<bool> EnsureClipIsLoadedAsync(AudioClip clip, int timeoutMs)
        {
            if (clip == null)
            {
                BatchLogger.Error("[AudioClipTransformer]⛔ AudioClip null passed to EnsureClipIsLoadedAsync()");
                return false;
            }

            BatchLogger.Warn($"[AudioClipTransformer] LoadAudioData on clip '{clip.name}' (initial loadState = {clip.loadState})");

            clip.LoadAudioData();

            var waited = 0;
            const int step = 20; // 20ms fixes
            var probe = new float[1];

            while (waited < timeoutMs)
            {
                try
                {
                    if (clip.GetData(probe, 0))
                    {
                        BatchLogger.Log($"✅ Clip '{clip.name}' is usable after {waited}ms");
                        return true;
                    }
                }
                catch
                {
                    // Unity not ready yet — ignore
                }

                await Task.Delay(step);
                waited += step;
            }

            BatchLogger.Error($"[AudioClipTransformer]⏱️ Timeout while loading clip '{clip.name}' after {timeoutMs}ms — GetData still fails");
            return false;
        }
        
        


        private static float[] CollectAllSamples(SoundTouchProcessor processor)
        {
            const int frameBlockSize = 1024;
            var maxChannels = processor.Channels;
            Span<float> buffer = stackalloc float[frameBlockSize * maxChannels];

            List<float[]> chunks = [];
            var totalSamples = 0;

            while (true)
            {
                var receivedFrames = processor.ReceiveSamples(buffer, frameBlockSize);
                if (receivedFrames == 0)
                    break;

                var samples = receivedFrames * maxChannels;
                var chunk = new float[samples];

                for (var i = 0; i < samples; i++)
                    chunk[i] = buffer[i];

                chunks.Add(chunk);
                totalSamples += samples;
            }

            // Fusion en une seule fois
            var final = new float[totalSamples];
            var offset = 0;
            foreach (var chunk in chunks)
            {
                Buffer.BlockCopy(chunk, 0, final, offset * sizeof(float), chunk.Length * sizeof(float));
                offset += chunk.Length;
            }

            return final;
        }
    }
}