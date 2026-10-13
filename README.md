# LoopDiDoop 🎙️🔁

A tiny, friction-free Windows 11 background utility that records your microphone when you hold a hotkey and loops the audio back to you until you tap the hotkey once more to dismiss it.

Built to offload thoughts and numbers into short-term memory (e.g. 2FA codes, tracking IDs, quick phone numbers, scratch calculations) and get rid of them the moment they're no longer needed.

---

## ⚡ How It Works

1. **Hold to Record**: Hold your hotkey (default `Ctrl + Alt + Space` or your custom key).
   - A soft blip plays to confirm recording has started.
   - A pill slides down from the top of your screen showing a red recording beacon, live volume meter, and elapsed timer.
2. **Release to Loop**: Let go of the hotkey when you finish speaking.
   - Accidental micro-taps (< 300ms) are silently discarded.
   - Leading and trailing silence is automatically trimmed.
   - Volume is peak-normalized so even whispered speech is clear and audible.
   - The audio repeats continuously with a comfortable **0.75s pause** between repetitions.
   - The pill displays a loop badge: *"Looping • Tap hotkey to clear"*.
3. **Tap to Clear**: Tap the hotkey once when you're done with the information.
   - Playback stops immediately.
   - Another blip plays and the pill slides away.
   - Audio buffer is wiped clean from memory.

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
