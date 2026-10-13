using System.Windows;
using System.Windows.Input;
using LoopDiDoop.Models;
using LoopDiDoop.Services;

namespace LoopDiDoop.Views;

public partial class HotkeyDialog : Window
{
    private readonly ConfigService _configService;
    private HotkeyModel _pendingHotkey;

    public HotkeyModel SelectedHotkey => _pendingHotkey;

    public HotkeyDialog(ConfigService configService)
    {
        InitializeComponent();
        _configService = configService;

        _pendingHotkey = _configService.Current.Hotkey ?? new HotkeyModel
        {
            VirtualKey = 0x20,
            KeyName = "Space",
            Control = true,
            Alt = true,
            Shift = false,
            Windows = false
        };

        HotkeyDisplayText.Text = _pendingHotkey.ToString();
        PassThroughCheckBox.IsChecked = _configService.Current.PassThroughHotkey;

        Loaded += (s, e) =>
        {
            CaptureBox.Focus();
        };

        PreviewKeyDown += OnDialogPreviewKeyDown;
    }

    private void OnCaptureBoxMouseDown(object sender, MouseButtonEventArgs e)
    {
        CaptureBox.Focus();
    }

    private void OnDialogPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

        // Ignore pure modifier presses
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        if (key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool win = Keyboard.Modifiers.HasFlag(ModifierKeys.Windows);

        int vk = KeyInterop.VirtualKeyFromKey(key);

        _pendingHotkey = new HotkeyModel
        {
            VirtualKey = vk,
            KeyName = key.ToString(),
            Control = ctrl,
            Alt = alt,
            Shift = shift,
            Windows = win
        };

        HotkeyDisplayText.Text = _pendingHotkey.ToString();
        e.Handled = true;
    }

    private void OnPresetCtrlAltSpaceClick(object sender, RoutedEventArgs e)
    {
        _pendingHotkey = new HotkeyModel
        {
            VirtualKey = 0x20,
            KeyName = "Space",
            Control = true,
            Alt = true,
            Shift = false,
            Windows = false
        };
        HotkeyDisplayText.Text = _pendingHotkey.ToString();
    }

    private void OnPresetCapsLockClick(object sender, RoutedEventArgs e)
    {
        _pendingHotkey = new HotkeyModel
        {
            VirtualKey = 0x14, // VK_CAPITAL
            KeyName = "CapsLock",
            Control = false,
            Alt = false,
            Shift = false,
            Windows = false
        };
        HotkeyDisplayText.Text = _pendingHotkey.ToString();
    }

    private void OnPresetScrollLockClick(object sender, RoutedEventArgs e)
    {
        _pendingHotkey = new HotkeyModel
        {
            VirtualKey = 0x91, // VK_SCROLL
            KeyName = "ScrollLock",
            Control = false,
            Alt = false,
            Shift = false,
            Windows = false
        };
        HotkeyDisplayText.Text = _pendingHotkey.ToString();
    }

    private void OnPresetF8Click(object sender, RoutedEventArgs e)
    {
        _pendingHotkey = new HotkeyModel
        {
            VirtualKey = 0x77, // VK_F8
            KeyName = "F8",
            Control = false,
            Alt = false,
            Shift = false,
            Windows = false
        };
        HotkeyDisplayText.Text = _pendingHotkey.ToString();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _configService.Current.Hotkey = _pendingHotkey;
        _configService.Current.PassThroughHotkey = PassThroughCheckBox.IsChecked == true;
        _configService.Save(_configService.Current);
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
