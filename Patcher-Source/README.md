# Project Zek patcher source — 1.0.14.0

Graphics, Zeal frame limit and supported client resolutions are configurable and remembered. EQW renders windowed and clears exclusive fullscreen refresh; the patcher refresh selector and monitor-changing helper were removed after owner testing. Windows controls the monitor refresh rate. Old refreshRate YAML keys are ignored and omitted on the next save without resetting graphics/FPS/resolution preferences.

All Classic fixes, optional music state, download/repair and verified Play behavior remain included. The executable and window title are Project-Zek-Patcher. The supplied build uses local files only; downloads are disabled until an online build is configured. Build-Patcher.ps1 can build an online patcher when supplied a real HTTPS manifest URL. No display host is built or deployed. Tests/DisplayOptionsTests.cs and DisplayPolicyTests.cs check preferences, supported resolutions and layout; PatcherFormTests.cs and music tests check the retained flows without starting a game or changing display hardware.

Build-Patcher.ps1 and the project defaults produce Project-Zek-Patcher.exe, including the matching self-update filename and Windows product metadata.
