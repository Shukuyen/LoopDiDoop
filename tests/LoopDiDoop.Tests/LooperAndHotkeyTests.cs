using System.Text.Json;
using NAudio.Wave;
using LoopDiDoop.Models;
using LoopDiDoop.Services;
using Xunit;

namespace LoopDiDoop.Tests;

public class LooperAndHotkeyTests
{
    [Fact]
    public void LoopingWaveProvider_StreamsAudioThenPauseThenLoops()
    {
        var format = new WaveFormat(1000, 16, 1); // 1000 samples/sec, 2000 bytes/sec
        // 50 samples of audio data (100 bytes), all filled with non-zero byte (42)
        var audioBytes = new byte[100];
        Array.Fill(audioBytes, (byte)42);

        int pauseMs = 50; // 50ms pause = 50 samples = 100 bytes of silence
        var looper = new LoopingWaveProvider(audioBytes, format, pauseMs);

        // 1. Read first 100 bytes (should be all 42)
        var buffer1 = new byte[100];
        int read1 = looper.Read(buffer1, 0, 100);
        Assert.Equal(100, read1);
        Assert.All(buffer1, b => Assert.Equal(42, b));

        // 2. Read next 100 bytes (should be silence during pause: all 0)
        var buffer2 = new byte[100];
        int read2 = looper.Read(buffer2, 0, 100);
        Assert.Equal(100, read2);
        Assert.All(buffer2, b => Assert.Equal(0, b));

        // 3. Read next 100 bytes (pause finished, loop restarted: all 42)
        var buffer3 = new byte[100];
        int read3 = looper.Read(buffer3, 0, 100);
        Assert.Equal(100, read3);
        Assert.All(buffer3, b => Assert.Equal(42, b));
    }

    [Fact]
    public void HotkeyModel_ToString_FormatsCorrectly()
    {
        var hotkey1 = new HotkeyModel
        {
            VirtualKey = 0x20,
            KeyName = "Space",
            Control = true,
            Alt = true,
            Shift = false,
            Windows = false
        };
        Assert.Equal("Ctrl + Alt + Space", hotkey1.ToString());

        var hotkey2 = new HotkeyModel
        {
            VirtualKey = 0x91,
            KeyName = "ScrollLock",
            Control = false,
            Alt = false,
            Shift = false,
            Windows = false
        };
        Assert.Equal("ScrollLock", hotkey2.ToString());
    }

    [Fact]
    public void AppConfig_SerializationRoundtrip_PreservesSettings()
    {
        var config = new AppConfig
        {
            Hotkey = new HotkeyModel
            {
                VirtualKey = 0x77,
                KeyName = "F8",
                Control = false,
                Alt = false,
                Shift = true,
                Windows = false
            },
            LoopPauseMs = 1000,
            MaxRecordSeconds = 25,
            AudioCuesEnabled = false
        };

        var json = JsonSerializer.Serialize(config);
        var restored = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(restored);
        Assert.NotNull(restored.Hotkey);
        Assert.Equal("F8", restored.Hotkey.KeyName);
        Assert.True(restored.Hotkey.Shift);
        Assert.Equal(1000, restored.LoopPauseMs);
        Assert.Equal(25, restored.MaxRecordSeconds);
        Assert.False(restored.AudioCuesEnabled);
    }
}
