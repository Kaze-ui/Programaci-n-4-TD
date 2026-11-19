---
name: dual-display-arcade
description: >-
  Use this skill when managing multi-display rendering, dual-monitor arcade setups,
  live leaderboards on secondary screens, single-monitor fallbacks, and hotkey display
  swapping in Unity.
---

# Dual-Display Arcade & Live Leaderboard Architecture

This skill covers the setup and maintenance of the multi-display arcade system designed for expos and arcade cabinets:
- **Display 1 (Laptop Screen)**: Real-time 1P and 2P hall of fame / leaderboards.
- **Display 2 (Big External Monitor / TV)**: The gameplay arena where players play.

## Key Principles & Fallbacks

### 1. Single Monitor Safety (Avoiding Black Screens)
When `Display.displays.Length <= 1`:
- **Disable the secondary camera**: Leaving a secondary camera enabled with `Depth = 100` and `cullingMask = 0` clears the entire screen to black, hiding the game.
- **Use ScreenSpaceOverlay for UI**:
  ```csharp
  secondaryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
  secondaryCanvas.sortingOrder = 9999;
  secondaryCanvas.targetDisplay = 0;
  secondaryCanvas.gameObject.SetActive(showLeaderboardOnSingleDisplay);
  ```
- **Toggle Controls**:
  - `[F10]` or `[L]`: Toggles leaderboard overlay on and off.
  - `[Esc]`: Closes leaderboard and returns to the game.

### 2. Dual Monitor Setup (Laptop + Big Display)
When `Display.displays.Length > 1`:
- Call `Display.displays[1].Activate();` on startup.
- Assign all gameplay cameras and game canvases to `targetDisplay = gameDisplay`.
- Assign the leaderboard secondary camera and secondary canvas to `targetDisplay = leaderboardDisplay`.
- Pressing `[F10]` swaps `isGameOnDisplay2 = !isGameOnDisplay2;` and updates `targetDisplay` instantly on all cameras and canvases.

### 3. TV / External Monitor Input Lag Mitigation
If using a consumer television as the external big screen:
1. Enable **Game Mode** in TV Picture Settings.
2. Rename the HDMI input label to **PC** (forces the TV to disable motion smoothing, TruMotion, and frame buffers).
3. Reduces input latency from ~120ms down to < 5ms.

