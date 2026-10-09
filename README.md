<div align="center">

<img src="docs/banner.png" alt="Velora PC" width="100%">

# Velora PC

**One click. Full PC optimization.**
A clean, fast, honest all-in-one PC maintenance app for Windows 10 & 11.

[![Build](https://github.com/TechnicPlayz/pc-optimizer/actions/workflows/build.yml/badge.svg)](https://github.com/TechnicPlayz/pc-optimizer/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/TechnicPlayz/pc-optimizer?display_name=tag&color=5B8CFF)](https://github.com/TechnicPlayz/pc-optimizer/releases/latest)
![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4)

[**⬇ Download**](https://github.com/TechnicPlayz/pc-optimizer/releases/latest) · [Report a bug](https://github.com/TechnicPlayz/pc-optimizer/issues/new?template=bug_report.md) · [Request a feature](https://github.com/TechnicPlayz/pc-optimizer/issues/new?template=feature_request.md)

</div>

---

## Why Velora

Most "PC boosters" invent scary scores and fake progress bars. Velora does the opposite: **open it, see your PC's status, press one button, watch real work happen, see measured results.** If something wasn't needed, it says so and skips it.

## What it does

| | Feature | What you get |
|---|---|---|
| 🚀 | **ULTRA ULTRA FAST PC** | One-click boost: memory, temp cleanup, Windows health check, safe power and gaming settings. Each step runs only if needed. |
| ⚡ | **RAM Boost** | Frees memory apps aren't using. Shows real before/after numbers. |
| 🧹 | **Deep Clean** | Scan first, review every item, then clean. Only system cache locations are touched. |
| 🎮 | **Game Boost** | Game Mode, background capture and power plan. Original settings are saved and restorable. |
| 🩺 | **PC Health** | Read-only checks: storage, memory, uptime, restart state, startup apps, network, Windows integrity. |
| 🔧 | **System Repair** | SFC and DISM (Check / Scan / Restore) explained in plain language with live progress. |
| 🕘 | **History** | Everything Velora has done, with details. |
| ⚙️ | **Settings** | Light / dark / system theme, start with Windows, weekly auto-maintenance, logs, one-click updates. |

## Our promises

- ✅ **Never deletes personal files.** Only a fixed list of system cache folders is cleaned.
- ✅ **Never disables Windows Defender or any security feature.**
- ✅ **Never kills processes.** Heavy apps are only *recommended* to you.
- ✅ **No fake progress, no fake scan results, no invented problems.**
- ✅ **Never claims to add RAM.** It asks Windows to release unused memory, and reports what was measured.
- ✅ **Changes are reversible.** Gaming settings are snapshotted first and can be restored.
- ✅ **No telemetry.** The only network request is the optional update check against this repo.
- ✅ **Admin only when needed.** One clear prompt, with an explanation.

## Download & install

1. Open the [**latest release**](https://github.com/TechnicPlayz/pc-optimizer/releases/latest).
2. Download **`VeloraPC-Setup-x.y.z.exe`** (installer + uninstaller) or **`VeloraPC.exe`** (portable).
3. Run it. If Windows SmartScreen says *"Windows protected your PC"*, click **More info → Run anyway** (the app isn't code-signed yet).

Updates are built in: **Settings → Updates**, or the banner on the Dashboard. Velora downloads the new installer, verifies its SHA-256, installs and reopens.

## Screenshots

<!-- After your first run, take screenshots and save them in docs/screenshots/, then replace this comment with:
<p align="center">
  <img src="docs/screenshots/dashboard.png" width="48%">
  <img src="docs/screenshots/boost-complete.png" width="48%">
</p>
-->
Screenshots coming with the first release.

## How the boost works

1. **Check system** - measures RAM and storage.
2. **Optimize memory** - trims unused app memory *(skipped if memory is already plentiful)*.
3. **Clean temporary files** - temp folders, Recycle Bin, safe Windows caches *(skipped if under 50 MB)*.
4. **Check Windows health** - fast DISM check *(needs admin; skipped if checked in the last 3 days)*.
5. **Safe optimizations** - fixes *Power saver* while plugged in; lists heavy background apps.
6. **Gaming settings** - Game Mode on, background capture off *(skipped if already set)*.
7. **Finalize** - saves the result to History.

You can cancel any time. Anything already finished is kept.

## Build from source

Requires Windows and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bat
BUILD.bat
```

Output: `publish\VeloraPC.exe`. To also build the installer, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) first; `BUILD.bat` will run it automatically.

Or let GitHub do it: every push runs the **Build Velora PC** workflow and uploads the EXE and installer as artifacts. Pushing a tag like `v1.0.1` publishes them on the Releases page. Full walkthrough: [docs/GITHUB_GUIDE.md](docs/GITHUB_GUIDE.md).

## Project structure

```
src/VeloraPC/
  Core/        Win32 calls, logging, settings, history, admin, process runner, system info
  Services/    Boost, Cleanup, Memory, Game, Health, Repair, Updates, Startup
  UI/          Design-system toolkit, theme service, page base class
  Views/       One class per screen
  Themes/      Dark.xaml, Light.xaml (colors) and Styles.xaml (controls)
installer/     Inno Setup script (installer + uninstaller)
docs/          Banner and guides
```

Editions: `Edition` in `src/VeloraPC/Core/Basics.cs` is the Free/Pro switch (everything is unlocked for now).

## Privacy

Velora collects no telemetry and uploads nothing. Settings, history and logs live in `%LocalAppData%\VeloraPC`. See [PRIVACY.txt](PRIVACY.txt).

## Support

Found a problem? [Open an issue](https://github.com/TechnicPlayz/pc-optimizer/issues/new?template=bug_report.md). In the app, **Settings → Logs → Export logs** creates a file you can attach.

## License

Velora PC is covered by the end-user license in [EULA.txt](EULA.txt).
