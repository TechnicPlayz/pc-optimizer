# Velora PC

All-in-one PC optimization and maintenance for Windows 10/11.
**Open app → see PC status → click one big button → watch it optimize → see the results.**

## Features
- **ULTRA ULTRA FAST PC** - one-click boost: memory, temp cleanup, Windows health check, safe power/gaming settings. Steps are skipped when not needed.
- **RAM Boost** - measured before/after memory usage (never claims to add RAM).
- **Deep Clean** - scan, review, then clean. Only fixed system cache locations; personal files are never touched.
- **Game Boost** - Game Mode, background capture, power plan. Original settings are saved and restorable.
- **PC Health** - read-only checks (storage, memory, uptime, restart state, startup apps, network, Windows integrity).
- **System Repair** - SFC and DISM Check / Scan / Restore with plain-language explanations and live progress.
- **History**, **Settings** (light/dark/system theme, start with Windows, weekly auto-maintenance, logs, updates), first-run **onboarding**.

Runs with standard rights; asks for administrator access only when a tool needs it (one UAC prompt, with an explanation).

## Project layout
```
src/VeloraPC/
  Core/       Win32 calls, logging, settings, history, admin, process runner, system info
  Services/   Boost, Cleanup, Memory, Game, Health, Repair, Updates, Startup
  UI/         Design-system toolkit (Ui.cs), theme service, page base class
  Views/      One class per screen
  Themes/     Dark.xaml, Light.xaml (colors) and Styles.xaml (controls)
installer/    Inno Setup script (installer + uninstaller)
```
`Edition` in `Core/Basics.cs` is the Free/Pro switch (everything unlocked for now).

## Build the EXE
**On GitHub (easiest):** push to `main`, open the **Actions** tab, download the `VeloraPC` artifact (contains the EXE and the installer).
For a public download link, publish a Release with a tag like `v1.0.0` - the workflow attaches both files automatically.

**Locally (Windows + .NET 8 SDK):** double-click `BUILD.bat`. Output: `publish\VeloraPC.exe`.

## Build the installer
The GitHub workflow builds it automatically (`dist\VeloraPC-Setup-<version>.exe`).
Locally: install [Inno Setup 6](https://jrsoftware.org/isinfo.php), run `BUILD.bat` first, then `iscc installer\VeloraPC.iss`.
The installer includes an uninstaller that also removes the startup entry, scheduled task and local data.

## Before selling it
- Replace `EULA.txt` and `PRIVACY.txt` (templates) with lawyer-reviewed versions.
- Code-sign the EXE and installer, otherwise Windows SmartScreen will warn users.
- Change `Branding.Repo` in `Core/Basics.cs` and the URLs in `installer/VeloraPC.iss` if the repo moves.
