using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using LoopDiDoop.Models;

namespace LoopDiDoop.Services;

public class AudioService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly SoundCueService _soundCueService;

    private WasapiCapture? _capture;
    private WaveOutEvent? _player;
    private MemoryStream? _recordStream;
    private DateTime _recordStartTime;
    private WaveFormat? _recordFormat;
    private readonly object _lock = new();

    public bool IsRecording { get; private set; }
    public bool IsLooping { get; private set; }

    public event Action<float>? AudioLevelChanged;
    public event Action<TimeSpan>? RecordingTick;
    public event Action? LoopStarted;
    public event Action? LoopStopped;
    public event Action? RecordingAborted;

    public AudioService(ConfigService configService, SoundCueService soundCueService)
    {
        _configService = configService;
        _soundCueService = soundCueService;
    }

    public void StartRecording()
    {
        lock (_lock)
        {
            if (IsRecording) return;

            // If already looping, stop it first
            if (IsLooping)
            {
                StopLoopInternal();
            }

            try
            {
                if (_configService.Current.AudioCuesEnabled)
                {
                    _soundCueService.PlayStartRecordCue();
                }

                _recordStream = new MemoryStream();
                _capture = new WasapiCapture();
                _recordFormat = _capture.WaveFormat;
                _recordStartTime = DateTime.UtcNow;

                _capture.DataAvailable += OnCaptureDataAvailable;
                _capture.RecordingStopped += OnCaptureRecordingStopped;

                _capture.StartRecording();
                IsRecording = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start audio recording: {ex.Message}");
                CleanupCapture();
            }
        }
    }

    private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!IsRecording || _recordStream == null || e.BytesRecorded == 0) return;

        _recordStream.Write(e.Buffer, 0, e.BytesRecorded);

        // Compute RMS level for visual indicator
        float level = CalculateLevel(e.Buffer, e.BytesRecorded, _recordFormat);
        AudioLevelChanged?.Invoke(level);

        var elapsed = DateTime.UtcNow - _recordStartTime;
        RecordingTick?.Invoke(elapsed);

        // Auto-cutoff safety limit
        if (elapsed.TotalSeconds >= _configService.Current.MaxRecordSeconds)
        {
            Task.Run(StopRecordingAndStartLoop);
        }
    }

    private float CalculateLevel(byte[] buffer, int count, WaveFormat? format)
    {
        if (format == null || count == 0) return 0f;
        try
        {
            bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat || format.BitsPerSample == 32;
            int bytesPerSample = format.BitsPerSample / 8;
            int samples = count / bytesPerSample;
            if (samples == 0) return 0f;

            float max = 0f;
            int step = Math.Max(1, samples / 64); // sample a subset for performance

            for (int i = 0; i < samples; i += step)
            {
                int offset = i * bytesPerSample;
                float val = 0f;
                if (isFloat)
                {
                    val = Math.Abs(BitConverter.ToSingle(buffer, offset));
                }
                else if (bytesPerSample == 2)
                {
                    val = Math.Abs(BitConverter.ToInt16(buffer, offset) / 32768f);
                }
                if (val > max) max = val;
            }
            return Math.Clamp(max, 0f, 1f);
        }
        catch
        {
            return 0f;
        }
    }

    private void OnCaptureRecordingStopped(object? sender, StoppedEventArgs e)
    {
        CleanupCapture();
    }

    public void StopRecordingAndStartLoop()
    {
        byte[] rawBytes;
        WaveFormat? format;
        TimeSpan duration;

        lock (_lock)
        {
            if (!IsRecording) return;
            IsRecording = false;

            duration = DateTime.UtcNow - _recordStartTime;

            try
            {
                _capture?.StopRecording();
            }
            catch { }

            rawBytes = _recordStream?.ToArray() ?? Array.Empty<byte>();
            format = _recordFormat;
            _recordStream = null;
        }

        // Accidental tap protection
        if (duration.TotalMilliseconds < _configService.Current.MinTapThresholdMs || rawBytes.Length == 0 || format == null)
        {
            RecordingAborted?.Invoke();
            return;
        }

        // Apply audio polish: Silence trimming and peak normalization
        var polishedBytes = AudioProcessing.NormalizeAndTrim(
            rawBytes,
            format,
            _configService.Current.TrimSilence,
            _configService.Current.NormalizeAudio,
            _configService.Current.SilenceThreshold
        );

        if (polishedBytes.Length == 0)
        {
            RecordingAborted?.Invoke();
            return;
        }

        // Start looping playback
        lock (_lock)
        {
            try
            {
                StopPlayer();

                var looper = new LoopingWaveProvider(polishedBytes, format, _configService.Current.LoopPauseMs);
                _player = new WaveOutEvent
                {
                    DesiredLatency = 120
                };
                _player.Init(looper);
                _player.Play();

                IsLooping = true;
                LoopStarted?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start loop player: {ex.Message}");
                IsLooping = false;
                RecordingAborted?.Invoke();
            }
        }
    }

    public void StopLoop()
    {
        lock (_lock)
        {
            if (!IsLooping && !IsRecording) return;

            if (IsRecording)
            {
                IsRecording = false;
                try { _capture?.StopRecording(); } catch { }
                _recordStream = null;
            }

            StopLoopInternal();
        }
    }

    private void StopLoopInternal()
    {
        StopPlayer();
        IsLooping = false;

        if (_configService.Current.AudioCuesEnabled)
        {
            _soundCueService.PlayClearCue();
        }

        LoopStopped?.Invoke();
    }

    private void StopPlayer()
    {
        if (_player != null)
        {
            try
            {
                _player.Stop();
                _player.Dispose();
            }
            catch { }
            _player = null;
        }
    }

    private void CleanupCapture()
    {
        if (_capture != null)
        {
            try
            {
                _capture.DataAvailable -= OnCaptureDataAvailable;
                _capture.RecordingStopped -= OnCaptureRecordingStopped;
                _capture.Dispose();
            }
            catch { }
            _capture = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            IsRecording = false;
            IsLooping = false;
            CleanupCapture();
            StopPlayer();
            _recordStream?.Dispose();
        }
    }
}
