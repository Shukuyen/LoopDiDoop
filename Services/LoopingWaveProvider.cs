using NAudio.Wave;

namespace LoopDiDoop.Services;

public class LoopingWaveProvider : IWaveProvider
{
    private readonly byte[] _audioData;
    private readonly int _pauseByteCount;
    private int _audioOffset;
    private bool _inPause;
    private int _pauseBytesRemaining;

    public WaveFormat WaveFormat { get; }

    public LoopingWaveProvider(byte[] audioData, WaveFormat waveFormat, int pauseDurationMs)
    {
        _audioData = audioData;
        WaveFormat = waveFormat;

        // Calculate byte count for the silence pause
        int bytesPerSecond = waveFormat.AverageBytesPerSecond;
        _pauseByteCount = (int)(bytesPerSecond * (pauseDurationMs / 1000.0));
        // Align pauseByteCount to block align
        if (waveFormat.BlockAlign > 0)
        {
            _pauseByteCount -= _pauseByteCount % waveFormat.BlockAlign;
        }

        _audioOffset = 0;
        _inPause = false;
        _pauseBytesRemaining = 0;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_audioData.Length == 0)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        int totalRead = 0;

        while (totalRead < count)
        {
            int needed = count - totalRead;

            if (_inPause)
            {
                int silenceToServe = Math.Min(needed, _pauseBytesRemaining);
                Array.Clear(buffer, offset + totalRead, silenceToServe);
                _pauseBytesRemaining -= silenceToServe;
                totalRead += silenceToServe;

                if (_pauseBytesRemaining <= 0)
                {
                    _inPause = false;
                    _audioOffset = 0;
                }
            }
            else
            {
                int audioAvailable = _audioData.Length - _audioOffset;
                int audioToServe = Math.Min(needed, audioAvailable);

                Buffer.BlockCopy(_audioData, _audioOffset, buffer, offset + totalRead, audioToServe);
                _audioOffset += audioToServe;
                totalRead += audioToServe;

                if (_audioOffset >= _audioData.Length)
                {
                    if (_pauseByteCount > 0)
                    {
                        _inPause = true;
                        _pauseBytesRemaining = _pauseByteCount;
                    }
                    else
                    {
                        _audioOffset = 0;
                    }
                }
            }
        }

        return totalRead;
    }
}
