namespace LoopDiDoop.Models;

public class HotkeyModel
{
    public int VirtualKey { get; set; } = 0x20; // Default VK_SPACE (32)
    public string KeyName { get; set; } = "Space";
    public bool Control { get; set; } = true;
    public bool Alt { get; set; } = true;
    public bool Shift { get; set; } = false;
    public bool Windows { get; set; } = false;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Windows) parts.Add("Win");
        parts.Add(string.IsNullOrWhiteSpace(KeyName) ? $"Key({VirtualKey})" : KeyName);
        return string.Join(" + ", parts);
    }
}

public class AppConfig
{
    public HotkeyModel? Hotkey { get; set; }
    public bool PassThroughHotkey { get; set; } = true;
    public int LoopPauseMs { get; set; } = 750;
    public int MaxRecordSeconds { get; set; } = 30;
    public int MinTapThresholdMs { get; set; } = 300;
    public bool AudioCuesEnabled { get; set; } = true;
    public bool NormalizeAudio { get; set; } = true;
    public bool TrimSilence { get; set; } = true;
    public float SilenceThreshold { get; set; } = 0.012f;
}
