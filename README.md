<div align="center">
  <img src="app_icon.png" alt="LoopDiDoop Logo" width="128" height="128" />
  <h1>LoopDiDoop</h1>
  <p><strong>A tiny, friction-free Windows 11 utility for short-term memory audio loops.</strong></p>
  <p>
    <img src="https://img.shields.io/badge/Platform-Windows%2011-0078D4?style=flat-square&logo=windows" alt="Platform: Windows 11" />
    <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 8" />
    <img src="https://img.shields.io/badge/Binary-&lt;%201%20MB-10B981?style=flat-square" alt="Binary Size" />
  </p>
</div>

---

**LoopDiDoop** records your microphone when you hold a hotkey and loops the audio back to you until you tap the hotkey once more to dismiss it.

Built to offload thoughts and numbers into short-term memory (e.g. 2FA codes, tracking IDs, quick phone numbers, scratch calculations) and get rid of them the moment they're no longer needed.

---

## ⚡ How It Works

1. **Hold to Record**: Hold your hotkey (default `Ctrl + Alt + Space` or your custom key).
   - A soft blip plays to confirm recording has started.
   - A Dynamic Island pill smoothly slides down from the top of your screen showing a red recording beacon, live volume meter, and elapsed timer.
2. **Release to Loop**: Let go of the hotkey when you finish speaking.
   - Accidental micro-taps (< 300ms) are silently discarded.
   - Leading and trailing silence is automatically trimmed.
   - Volume is peak-normalized so even whispered speech is clear and audible.
   - The audio repeats continuously with a comfortable **0.75s pause** between repetitions.
   - The pill displays a loop badge: *"Looping • Tap hotkey to clear"*.
3. **Tap to Clear**: Tap the hotkey once when you're done with the information.
   - Playback stops immediately.
   - A dismiss blip plays and the pill slides away.
   - The audio buffer is wiped clean from memory.

---

## 🚀 Running the App

### Standalone Executable
Download the single-file executable from [Releases](https://github.com/Shukuyen/LoopDiDoop/releases) (or build locally). It is self-contained, under 1 MB, and requires no installation.

```powershell
.\bin\Release\net8.0-windows\win-x64\publish\LoopDiDoop.exe
```

---

## ⚙️ Configuration & Customization

- **First Launch**: Automatically prompts you to press your desired hotkey combination.
- **System Tray**: Right-click the LoopDiDoop icon in the Windows taskbar tray to:
  - Change the hotkey anytime (`Change Hotkey...`).
  - Toggle `Pass Hotkey to Other Apps` (allows normal key presses and toggles to reach other apps).
  - Adjust the loop pause gap (`500 ms`, `750 ms`, `1000 ms`, `1500 ms`).
  - Toggle sound cues on/off.
  - Exit the application.
- **Intelligent Lock-Key Support**: When using keys like `CapsLock` or `Scroll Lock`, tapping the key toggles it on/off normally in Windows, while holding it to record restores the previous lock state upon release so your typing isn't affected.
- **Config File**: Stored in `%AppData%\LoopDiDoop\config.json`.

---

## 🧪 Testing

Run the automated test suite verifying silence trimming, peak normalization, and looping pause intervals:
```powershell
dotnet test tests/LoopDiDoop.Tests/LoopDiDoop.Tests.csproj
```

---

## 🛠️ Tech Stack
- **Framework**: C# / .NET 8 (WPF + Windows Forms for NotifyIcon)
- **Audio Engine**: NAudio (WASAPI in-memory capture, low-latency audio looper)
- **Global Input**: Win32 `SetWindowsHookEx(WH_KEYBOARD_LL)` with key-repeat suppression
- **Overlay**: Non-activating topmost Dynamic Island pill (`WS_EX_NOACTIVATE`)
