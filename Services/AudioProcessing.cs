using NAudio.Wave;

namespace LoopDiDoop.Services;

public static class AudioProcessing
{
    public static byte[] NormalizeAndTrim(
        byte[] rawPcm,
        WaveFormat format,
        bool trimSilence,
        bool normalize,
        float silenceThreshold = 0.012f,
        float targetPeak = 0.90f)
    {
        if (rawPcm.Length == 0) return rawPcm;

        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat || format.BitsPerSample == 32;
        int bytesPerSample = format.BitsPerSample / 8;
        int channels = format.Channels;
        int frameSize = bytesPerSample * channels;

        if (frameSize <= 0) return rawPcm;

        int totalFrames = rawPcm.Length / frameSize;
        if (totalFrames <= 0) return rawPcm;

        // Convert to float samples per frame (max amplitude across channels)
        float[] framePeaks = new float[totalFrames];

        for (int f = 0; f < totalFrames; f++)
        {
            float maxFrameAmp = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                int byteOffset = (f * frameSize) + (ch * bytesPerSample);
                float sampleVal;

                if (isFloat)
                {
                    sampleVal = Math.Abs(BitConverter.ToSingle(rawPcm, byteOffset));
                }
                else if (bytesPerSample == 2)
                {
                    short s = BitConverter.ToInt16(rawPcm, byteOffset);
                    sampleVal = Math.Abs(s / 32768f);
                }
                else
                {
                    sampleVal = 0f;
                }

                if (sampleVal > maxFrameAmp) maxFrameAmp = sampleVal;
            }
            framePeaks[f] = maxFrameAmp;
        }

        // 1. Trim silence from start and end
        int startFrame = 0;
        int endFrame = totalFrames - 1;

        if (trimSilence)
        {
            // Find start
            while (startFrame < totalFrames && framePeaks[startFrame] < silenceThreshold)
            {
                startFrame++;
            }

            // Find end
            while (endFrame > startFrame && framePeaks[endFrame] < silenceThreshold)
            {
                endFrame--;
            }

            // Leave a small pad of ~50ms (e.g. 0.05 * SampleRate frames) if available to avoid clipping speech attacks
            int padFrames = (int)(format.SampleRate * 0.04);
            startFrame = Math.Max(0, startFrame - padFrames);
            endFrame = Math.Min(totalFrames - 1, endFrame + padFrames);
        }

        if (startFrame >= endFrame)
        {
            startFrame = 0;
            endFrame = totalFrames - 1;
        }

        int trimmedFrames = endFrame - startFrame + 1;
        int trimmedBytes = trimmedFrames * frameSize;
        byte[] trimmedPcm = new byte[trimmedBytes];
        Buffer.BlockCopy(rawPcm, startFrame * frameSize, trimmedPcm, 0, trimmedBytes);

        // 2. Peak Normalization
        if (normalize && trimmedFrames > 0)
        {
            float overallPeak = 0f;
            for (int f = startFrame; f <= endFrame; f++)
            {
                if (framePeaks[f] > overallPeak) overallPeak = framePeaks[f];
            }

            if (overallPeak > 0.005f && overallPeak < targetPeak)
            {
                float gain = Math.Min(targetPeak / overallPeak, 20.0f); // Cap maximum gain at 20x (+26dB)

                for (int f = 0; f < trimmedFrames; f++)
                {
                    for (int ch = 0; ch < channels; ch++)
                    {
                        int byteOffset = (f * frameSize) + (ch * bytesPerSample);
                        if (isFloat)
                        {
                            float sample = BitConverter.ToSingle(trimmedPcm, byteOffset);
                            float boosted = Math.Clamp(sample * gain, -1.0f, 1.0f);
                            byte[] bytes = BitConverter.GetBytes(boosted);
                            Buffer.BlockCopy(bytes, 0, trimmedPcm, byteOffset, 4);
                        }
                        else if (bytesPerSample == 2)
                        {
                            short sample = BitConverter.ToInt16(trimmedPcm, byteOffset);
                            int boosted = (int)Math.Round(sample * gain);
                            boosted = Math.Clamp(boosted, short.MinValue, short.MaxValue);
                            byte[] bytes = BitConverter.GetBytes((short)boosted);
                            Buffer.BlockCopy(bytes, 0, trimmedPcm, byteOffset, 2);
                        }
                    }
                }
            }
        }

        return trimmedPcm;
    }
}
