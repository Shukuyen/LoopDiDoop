using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using LoopDiDoop.Models;

namespace LoopDiDoop.Services;

public class TrayService : IDisposable
{
    private readonly ConfigService _configService;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _statusItem;

    public event Action? OpenHotkeySettingsRequested;
    public event Action? ExitRequested;

    public TrayService(ConfigService configService)
    {
        _configService = configService;
        InitializeTray();
    }

    private void InitializeTray()
    {
        var contextMenu = new ContextMenuStrip();

        // Title Header
        var titleItem = new ToolStripMenuItem("LoopDiDoop")
        {
            Enabled = false,
            Font = new Font(contextMenu.Font, FontStyle.Bold)
        };
        contextMenu.Items.Add(titleItem);

        // Status Item (Shows current hotkey)
        string hotkeyDesc = _configService.Current.Hotkey?.ToString() ?? "None";
        _statusItem = new ToolStripMenuItem($"Hotkey: {hotkeyDesc}")
        {
            Enabled = false
        };
        contextMenu.Items.Add(_statusItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // Change Hotkey
        var changeHotkeyItem = new ToolStripMenuItem("Change Hotkey...", null, (s, e) =>
        {
            OpenHotkeySettingsRequested?.Invoke();
        });
        contextMenu.Items.Add(changeHotkeyItem);

        // Pause Duration Submenu
        var pauseMenu = new ToolStripMenuItem("Loop Pause Gap");
        AddPauseOption(pauseMenu, "500 ms", 500);
        AddPauseOption(pauseMenu, "750 ms (Default)", 750);
        AddPauseOption(pauseMenu, "1000 ms", 1000);
        AddPauseOption(pauseMenu, "1500 ms", 1500);
        contextMenu.Items.Add(pauseMenu);

        // Audio Cues Toggle
        var audioCuesItem = new ToolStripMenuItem("Sound Cues")
        {
            Checked = _configService.Current.AudioCuesEnabled,
            CheckOnClick = true
        };
        audioCuesItem.Click += (s, e) =>
        {
            _configService.Current.AudioCuesEnabled = audioCuesItem.Checked;
            _configService.Save(_configService.Current);
        };
        contextMenu.Items.Add(audioCuesItem);

        // Pass-through toggle
        var passThroughItem = new ToolStripMenuItem("Pass Hotkey to Other Apps")
        {
            Checked = _configService.Current.PassThroughHotkey,
            CheckOnClick = true
        };
        passThroughItem.Click += (s, e) =>
        {
            _configService.Current.PassThroughHotkey = passThroughItem.Checked;
            _configService.Save(_configService.Current);
        };
        contextMenu.Items.Add(passThroughItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        // Exit
        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) =>
        {
            ExitRequested?.Invoke();
        });
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = "LoopDiDoop — Short-Term Memory Looper",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) =>
        {
            OpenHotkeySettingsRequested?.Invoke();
        };
    }

    private void AddPauseOption(ToolStripMenuItem parent, string label, int ms)
    {
        var item = new ToolStripMenuItem(label)
        {
            Checked = (_configService.Current.LoopPauseMs == ms)
        };

        item.Click += (s, e) =>
        {
            foreach (ToolStripMenuItem sibling in parent.DropDownItems)
            {
                sibling.Checked = false;
            }
            item.Checked = true;
            _configService.Current.LoopPauseMs = ms;
            _configService.Save(_configService.Current);
        };

        parent.DropDownItems.Add(item);
    }

    public void UpdateStatusText()
    {
        if (_statusItem != null)
        {
            string hotkeyDesc = _configService.Current.Hotkey?.ToString() ?? "None";
            _statusItem.Text = $"Hotkey: {hotkeyDesc}";
        }
    }

    public void ShowNotification(string title, string text)
    {
        _notifyIcon?.ShowBalloonTip(2000, title, text, ToolTipIcon.Info);
    }

    private static Icon CreateAppIcon()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var assoc = Icon.ExtractAssociatedIcon(exePath);
                if (assoc != null) return assoc;
            }

            string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(localIco))
            {
                return new Icon(localIco, 32, 32);
            }
        }
        catch { }

        // Fallback procedural icon
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Dark background circle
        using var bgBrush = new SolidBrush(Color.FromArgb(24, 24, 27));
        g.FillEllipse(bgBrush, 1, 1, 30, 30);

        // Cyan loop arc
        using var cyanPen = new Pen(Color.FromArgb(6, 182, 212), 3f);
        g.DrawArc(cyanPen, 6, 6, 20, 20, 35, 290);

        // Mini arrow / circle dot
        using var dotBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
        g.FillEllipse(dotBrush, 13, 13, 6, 6);

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
