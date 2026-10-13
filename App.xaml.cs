using System.Threading;
using System.Windows;
using LoopDiDoop.Services;
using LoopDiDoop.Views;

namespace LoopDiDoop;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private ConfigService _configService = null!;
    private SoundCueService _soundCueService = null!;
    private AudioService _audioService = null!;
    private GlobalHotkeyService _hotkeyService = null!;
    private TrayService _trayService = null!;
    private OverlayWindow _overlayWindow = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = @"Local\LoopDiDoop_SingleInstance_Mutex";
        _mutex = new Mutex(false, mutexName);

        bool hasHandle = false;
        try
        {
            hasHandle = _mutex.WaitOne(TimeSpan.Zero, false);
        }
        catch (AbandonedMutexException)
        {
            hasHandle = true;
        }

        if (!hasHandle)
        {
            System.Windows.MessageBox.Show("LoopDiDoop is already running in the system tray.", "LoopDiDoop",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Initialize core services
        _configService = new ConfigService();
        _soundCueService = new SoundCueService();
        _audioService = new AudioService(_configService, _soundCueService);
        _hotkeyService = new GlobalHotkeyService(_configService);
        _trayService = new TrayService(_configService);

        // Initialize overlay
        _overlayWindow = new OverlayWindow();
        _overlayWindow.HideOverlay();

        // Connect audio events to overlay
        _audioService.AudioLevelChanged += (level) => _overlayWindow.UpdateAudioLevel(level);
        _audioService.RecordingTick += (elapsed) => _overlayWindow.UpdateTimer(elapsed, _configService.Current.MaxRecordSeconds);
        _audioService.LoopStarted += () =>
        {
            string hint = _configService.Current.Hotkey?.ToString() ?? "hotkey";
            _overlayWindow.ShowLooping(hint);
        };
        _audioService.LoopStopped += () => _overlayWindow.HideOverlay();
        _audioService.RecordingAborted += () => _overlayWindow.HideOverlay();

        // Connect hotkey events
        _hotkeyService.HotkeyDown += OnHotkeyDown;
        _hotkeyService.HotkeyUp += OnHotkeyUp;

        // Connect tray events
        _trayService.OpenHotkeySettingsRequested += OpenHotkeySettings;
        _trayService.ExitRequested += () => Shutdown();

        // First run check: If no hotkey configured yet, prompt user
        if (!_configService.HasConfiguredHotkey)
        {
            OpenHotkeySettings();
            if (!_configService.HasConfiguredHotkey)
            {
                _configService.Current.Hotkey = new Models.HotkeyModel
                {
                    VirtualKey = 0x20,
                    KeyName = "Space",
                    Control = true,
                    Alt = true
                };
                _configService.Save(_configService.Current);
                _trayService.UpdateStatusText();
            }
        }

        // Start global keyboard monitoring
        _hotkeyService.StartHook();
    }

    private void OnHotkeyDown()
    {
        Dispatcher.Invoke(() =>
        {
            if (_audioService.IsLooping)
            {
                // Tapped during loop playback: clear loop immediately!
                _hotkeyService.SuppressNextRelease();
                _audioService.StopLoop();
                _hotkeyService.RestoreLockState();
            }
            else if (!_audioService.IsRecording)
            {
                // Idle -> Start recording
                _overlayWindow.ShowRecording(_configService.Current.MaxRecordSeconds);
                _audioService.StartRecording();
            }
        });
    }

    private void OnHotkeyUp()
    {
        Dispatcher.Invoke(() =>
        {
            if (_audioService.IsRecording)
            {
                // Finished holding -> polish audio & start loop
                _audioService.StopRecordingAndStartLoop();
                if (_audioService.IsLooping)
                {
                    _hotkeyService.RestoreLockState();
                }
            }
        });
    }

    private void OpenHotkeySettings()
    {
        Dispatcher.Invoke(() =>
        {
            _hotkeyService.IsSuspended = true;

            var dialog = new HotkeyDialog(_configService)
            {
                Owner = null,
                Topmost = true
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                _trayService.UpdateStatusText();
            }

            _hotkeyService.IsSuspended = false;
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _audioService?.Dispose();
        _trayService?.Dispose();
        _overlayWindow?.Close();

        if (_mutex != null)
        {
            try { _mutex.ReleaseMutex(); } catch { }
            _mutex.Dispose();
            _mutex = null;
        }

        base.OnExit(e);
    }
}
