# 🎵 Floating Media Studio

A sleek, luxurious, lightweight floating media controller widget for Windows that overlays seamlessly on full-screen apps and games without stealing focus.

Inspired by **Samsung One UI 9** & **Apple Music**, featuring dynamic color theming, interactive timeline scrubbing, 8-directional native window resizing, and adaptive responsive layouts.

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)
![Dependencies](https://img.shields.io/badge/Dependencies-Zero%20(Built--in%20.NET)-orange?style=flat-square)
![Binary Size](https://img.shields.io/badge/Binary%20Size-~44%20KB-purple?style=flat-square)

---

## ✨ Features

- **🛡️ Full-Screen & Game Overlay (`WS_EX_NOACTIVATE`)**:
  - Never steals keyboard or mouse focus from active games, video players (YouTube F11), or full-screen apps.
  - Keeps your games running at full frame rate without minimizing or losing cursor control.
- **🎨 One UI 9 Dynamic Color Theming**:
  - Automatically extracts the most vibrant color palette from the current song/video thumbnail.
  - The frosted glass card background, progress bar, play button, and ambient halo adapt to match the artwork.
- **⏱️ Live Timeline & Interactive Scrubbing**:
  - Real-time time synchronization (`01:23 / 03:45`) synchronized with Windows SMTC.
  - Click or drag the smooth One UI capsule scrubber thumb to seek directly through tracks.
- **📐 8-Way Native Border Resizing & Adaptive Layouts**:
  - Grab any border (top, bottom, left, right) or any of the 4 corners to freely resize the window.
  - **Card Mode** (tall): Large hero artwork, song metadata, timeline scrubber, full playback controls, and volume.
  - **Bar Mode** (wide): Compact horizontal toolbar with thumbnail, title, mini scrubber, and controls.
  - **Micro Mode** (small, < 135px): Minimizes down to just the album artwork + centered frosted Play/Pause button.
- **🌌 Ambient Glow**:
  - Diffused, soft radiant backlight surrounding the artwork and player card.
- **🧲 Edge Snapping**:
  - Automatically snaps cleanly to the edges of your screen when dragged close (< 18px).
- **📌 Pin & Lock Mode**:
  - Prevents accidental moves or resizes while gaming.
- **🔒 Single-Instance Protected**:
  - System Mutex prevents duplicate instances from opening.

---

## 🚀 Quick Start

### Option 1: Run the Precompiled Binary
Just run `FloatingMediaBar.exe` directly. No installation or setup required.

### Option 2: Build from Source
Compile in 1 second using Windows built-in C# compiler (`csc.exe`):
```cmd
build.bat
```
or run directly in PowerShell/CMD:
```cmd
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /optimize+ /lib:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF" /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:"C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll" /r:"C:\Windows\System32\WinMetadata\Windows.Foundation.winmd" /r:"C:\Windows\System32\WinMetadata\Windows.Media.winmd" /r:"C:\Windows\System32\WinMetadata\Windows.Storage.winmd" /out:FloatingMediaBar.exe FloatingMediaBar.cs
```

---

## 🎮 Shortcuts & Controls

| Action | Function |
| :--- | :--- |
| **Mouse Wheel (Scroll)** | Hover anywhere over the widget to adjust system volume up / down |
| **Drag Anywhere** | Left-click and drag any blank space to move the widget |
| **Edge / Corner Drag** | Drag any of the 8 borders or corners to resize freely |
| **Seek Bar Drag** | Drag the capsule thumb to seek playback position |
| **Right-Click** | Open context menu (Opacity, Layout presets, Lock mode, Ambient toggle, Exit) |
| **`📌` Button** | Lock / Unlock widget position and size |

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
