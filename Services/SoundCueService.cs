using System.IO;
using System.Media;

namespace LoopDiDoop.Services;

public class SoundCueService
{
    private readonly byte[] _startCueWav;
    private readonly byte[] _clearCueWav;

    public SoundCueService()
    {
        _startCueWav = GenerateChirpWav(startFreq: 460, endFreq: 720, durationMs: 45, volume: 0.22f);
        _clearCueWav = GenerateChirpWav(startFreq: 680, endFreq: 360, durationMs: 50, volume: 0.20f);
    }

    public void PlayStartRecordCue()
    {
        PlayWavBytes(_startCueWav);
    }

    public void PlayClearCue()
    {
        PlayWavBytes(_clearCueWav);
    }

    private static void PlayWavBytes(byte[] wavBytes)
    {
        try
        {
            var ms = new MemoryStream(wavBytes);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to play audio cue: {ex.Message}");
        }
    }

    private static byte[] GenerateChirpWav(double startFreq, double endFreq, int durationMs, float volume)
    {
        const int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * (durationMs / 1000.0));
        var pcmData = new byte[totalSamples * 2];

        double phase = 0;
        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / totalSamples;
            // Linear frequency sweep
            double currentFreq = startFreq + (endFreq - startFreq) * t;
            phase += 2.0 * Math.PI * currentFreq / sampleRate;

            // Smooth Hann / Cosine envelope to prevent clicks
            double envelope = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * t));
            double sampleValue = Math.Sin(phase) * envelope * volume;

            short sample16 = (short)(Math.Clamp(sampleValue, -1.0, 1.0) * 32767);
            pcmData[i * 2] = (byte)(sample16 & 0xFF);
            pcmData[i * 2 + 1] = (byte)((sample16 >> 8) & 0xFF);
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write("RIFF"u8.ToArray());
        bw.Write(36 + pcmData.Length);
        bw.Write("WAVE"u8.ToArray());

        // fmt subchunk
        bw.Write("fmt "u8.ToArray());
        bw.Write(16); // Subchunk1Size for PCM
        bw.Write((short)1); // AudioFormat: 1 = PCM
        bw.Write((short)1); // NumChannels: 1 = Mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
        bw.Write((short)2); // BlockAlign (NumChannels * BitsPerSample/8)
        bw.Write((short)16); // BitsPerSample

        // data subchunk
        bw.Write("data"u8.ToArray());
        bw.Write(pcmData.Length);
        bw.Write(pcmData);

        return ms.ToArray();
    }
}
