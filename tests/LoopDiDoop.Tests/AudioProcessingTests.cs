using NAudio.Wave;
using LoopDiDoop.Services;
using Xunit;

namespace LoopDiDoop.Tests;

public class AudioProcessingTests
{
    [Fact]
    public void SilenceTrim_TrimsLeadingAndTrailingSilence()
    {
        var format = new WaveFormat(44100, 16, 1);
        int leadingSilenceSamples = 44100; // 1 second silence
        int activeSamples = 4410;          // 0.1 second audio
        int trailingSilenceSamples = 44100; // 1 second silence

        int totalSamples = leadingSilenceSamples + activeSamples + trailingSilenceSamples;
        var pcm = new byte[totalSamples * 2];

        // Fill active section with audible signal
        for (int i = 0; i < activeSamples; i++)
        {
            int sampleIndex = leadingSilenceSamples + i;
            short val = (short)(Math.Sin(2 * Math.PI * 440 * i / 44100.0) * 16000);
            pcm[sampleIndex * 2] = (byte)(val & 0xFF);
            pcm[sampleIndex * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }

        var trimmed = AudioProcessing.NormalizeAndTrim(
            pcm,
            format,
            trimSilence: true,
            normalize: false,
            silenceThreshold: 0.02f
        );

        // Trimmed result should be significantly smaller than initial 2.1 seconds
        Assert.True(trimmed.Length < pcm.Length);
        Assert.True(trimmed.Length > 0);
    }

    [Fact]
    public void NormalizePeak_BoostsQuietSignalToTarget()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        int samples = 1000;
        var pcm = new byte[samples * 4];

        // Fill with quiet signal (peak 0.10f)
        for (int i = 0; i < samples; i++)
        {
            float val = 0.10f * (float)Math.Sin(2 * Math.PI * 440 * i / 44100.0);
            byte[] bytes = BitConverter.GetBytes(val);
            Buffer.BlockCopy(bytes, 0, pcm, i * 4, 4);
        }

        var normalized = AudioProcessing.NormalizeAndTrim(
            pcm,
            format,
            trimSilence: false,
            normalize: true,
            targetPeak: 0.90f
        );

        // Find max peak in normalized output
        float maxPeak = 0f;
        for (int i = 0; i < samples; i++)
        {
            float val = Math.Abs(BitConverter.ToSingle(normalized, i * 4));
            if (val > maxPeak) maxPeak = val;
        }

        Assert.InRange(maxPeak, 0.85f, 0.95f);
    }
}
