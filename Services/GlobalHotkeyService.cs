using System.Diagnostics;
using System.Runtime.InteropServices;
using LoopDiDoop.Models;

namespace LoopDiDoop.Services;

public class GlobalHotkeyService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private const int VK_CAPITAL = 0x14; // Caps Lock
    private const int VK_NUMLOCK = 0x90; // Num Lock
    private const int VK_SCROLL = 0x91;  // Scroll Lock

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private readonly ConfigService _configService;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _isHotkeyDown;
    private bool _suppressRelease;
    private bool _isSimulatingKey;
    private bool _lockStateOnKeyDown;

    public bool IsSuspended { get; set; }

    public event Action? HotkeyDown;
    public event Action? HotkeyUp;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public GlobalHotkeyService(ConfigService configService)
    {
        _configService = configService;
    }

    public void StartHook()
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr hModule = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;

        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hModule, 0);
        if (_hookId == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            Debug.WriteLine($"Failed to set keyboard hook, error {err}");
        }
    }

    public void StopHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !IsSuspended && !_isSimulatingKey)
        {
            int msg = wParam.ToInt32();
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var hotkey = _configService.Current.Hotkey;

            if (hotkey != null)
            {
                int targetVk = hotkey.VirtualKey;
                bool isKeyMatch = (kb.vkCode == targetVk);

                bool isCtrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                bool isAltDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                bool isShiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                bool isWinDown = ((GetAsyncKeyState(VK_LWIN) | GetAsyncKeyState(VK_RWIN)) & 0x8000) != 0;

                bool modifiersMatch = (hotkey.Control == isCtrlDown) &&
                                      (hotkey.Alt == isAltDown) &&
                                      (hotkey.Shift == isShiftDown) &&
                                      (hotkey.Windows == isWinDown);

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (isKeyMatch && modifiersMatch)
                    {
                        if (!_isHotkeyDown)
                        {
                            _isHotkeyDown = true;
                            _suppressRelease = false;
                            _lockStateOnKeyDown = GetLockKeyState(targetVk);
                            HotkeyDown?.Invoke();
                        }

                        // If user configured to swallow the key, consume it
                        if (!_configService.Current.PassThroughHotkey)
                        {
                            return (IntPtr)1;
                        }
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (_isHotkeyDown)
                    {
                        bool releasedMainKey = (kb.vkCode == targetVk);
                        bool releasedRequiredModifier =
                            (hotkey.Control && (kb.vkCode == 0xA2 || kb.vkCode == 0xA3 || kb.vkCode == VK_CONTROL)) ||
                            (hotkey.Alt && (kb.vkCode == 0xA4 || kb.vkCode == 0xA5 || kb.vkCode == VK_MENU)) ||
                            (hotkey.Shift && (kb.vkCode == 0xA0 || kb.vkCode == 0xA1 || kb.vkCode == VK_SHIFT)) ||
                            (hotkey.Windows && (kb.vkCode == VK_LWIN || kb.vkCode == VK_RWIN));

                        if (releasedMainKey || releasedRequiredModifier)
                        {
                            _isHotkeyDown = false;
                            if (!_suppressRelease)
                            {
                                HotkeyUp?.Invoke();
                            }

                            if (!_configService.Current.PassThroughHotkey)
                            {
                                return (IntPtr)1;
                            }
                        }
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void SuppressNextRelease()
    {
        _suppressRelease = true;
    }

    public void RestoreLockState()
    {
        var hotkey = _configService.Current.Hotkey;
        if (hotkey != null && IsToggleLockKey(hotkey.VirtualKey))
        {
            SetLockKeyState(hotkey.VirtualKey, _lockStateOnKeyDown);
        }
    }

    public static bool IsToggleLockKey(int vk)
    {
        return vk == VK_CAPITAL || vk == VK_SCROLL || vk == VK_NUMLOCK;
    }

    public static bool GetLockKeyState(int vk)
    {
        return (GetKeyState(vk) & 1) != 0;
    }

    public void SetLockKeyState(int vk, bool targetState)
    {
        if (!IsToggleLockKey(vk)) return;

        bool currentState = GetLockKeyState(vk);
        if (currentState != targetState)
        {
            _isSimulatingKey = true;
            try
            {
                byte scan = (byte)(vk == VK_CAPITAL ? 0x45 : 0x00);
                keybd_event((byte)vk, scan, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event((byte)vk, scan, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            finally
            {
                _isSimulatingKey = false;
            }
        }
    }

    public void Dispose()
    {
        StopHook();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
