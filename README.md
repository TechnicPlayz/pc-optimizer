# Velora PC

### 🚀 Simple, fast, open-source PC optimization for Windows 10 & 11

Velora PC is an open-source Windows utility designed to help you **clean, optimize, check, and maintain your PC** from one simple application.

**Open Velora PC → check your PC → optimize → see what changed.**

> ⚠️ **Open-source & antivirus notice:** Velora PC performs legitimate system-maintenance tasks that can require administrator permissions. Because of this, some antivirus products or Windows SmartScreen may occasionally flag the application as suspicious or potentially unwanted. These can be **false positives**. Velora PC is open source, so you can inspect the code yourself before running it.

---

## ✨ Features

### 🚀 One-Click Boost

Optimize your PC with one simple button.

Velora PC can perform checks and optimizations for:

* 🧠 Memory usage
* 🧹 Temporary system files
* ❤️ Windows health
* ⚡ Power and performance settings
* 🎮 Gaming-related Windows settings

Unnecessary steps are automatically skipped.

### 🧠 RAM Boost

See your memory usage before and after optimization.

Velora PC **does not add physical RAM**. Instead, it helps reduce unnecessary memory usage where possible.

### 🧹 Deep Clean

Scan supported Windows cache locations before cleaning them.

You can review what was found before anything is removed.

> 🔒 Velora PC does not target your personal documents, photos, videos, downloads, or other personal files with its built-in system cleaner.

### 🎮 Game Boost

Game Boost can optimize Windows gaming-related settings, including:

* Windows Game Mode
* Background capture
* Power/performance settings

Original settings are saved so they can be restored.

### ❤️ PC Health

Check important information about your PC:

* Storage
* Memory
* Uptime
* Restart status
* Startup applications
* Network status
* Windows integrity

Health checks are designed to be read-only.

### 🛠️ System Repair

Use Windows' built-in repair tools through an easy-to-understand interface.

Supported operations include:

* SFC Check
* SFC Scan
* SFC Restore
* DISM Check
* DISM Scan
* DISM Restore

Velora PC provides progress and explains results in plain language.

### 📋 History

View previous maintenance and optimization activity.

### ⚙️ Settings

Customize Velora PC with:

* 🌙 Dark mode
* ☀️ Light mode
* 🖥️ Follow Windows theme
* 🚀 Start with Windows
* 🔄 Weekly automatic maintenance
* 📝 Logging
* ⬆️ Update settings

### 👋 Easy Onboarding

A first-run setup helps you understand the application before you start using its features.

---

## 🔐 Permissions & Security

Velora PC normally runs with **standard Windows permissions**.

If a feature requires administrator access, Windows will display a normal **UAC prompt**.

Velora PC does not silently elevate itself.

Some features, such as Windows repair operations and certain system optimizations, require administrator permissions because Windows itself restricts access to them.

---

# 🛡️ Antivirus & SmartScreen Warnings

You may see a warning from Windows Defender, SmartScreen, or another antivirus product when downloading or running Velora PC.

### Why can this happen?

PC optimization software can perform actions that security software monitors closely, such as:

* Changing Windows settings
* Running Windows maintenance commands
* Cleaning system cache
* Changing power settings
* Accessing system information
* Requesting administrator permissions

These behaviors can sometimes cause **false-positive detections**, especially when an application is new or does not yet have a strong reputation/signing history.

### Is Velora PC open source?

**Yes.**

The source code is publicly available in this repository, allowing anyone to inspect how Velora PC works.

However, **open source does not mean you should automatically ignore an antivirus warning**.

If your antivirus reports a problem:

1. Make sure you downloaded Velora PC from the official repository or release.
2. Check that you have the expected release/version.
3. Review the source code if you are able to.
4. Scan the downloaded file with your security software.
5. Do not run a copy obtained from an unofficial website.

> 🔒 **Never disable your antivirus just to install Velora PC.** If you receive a detection you don't understand, investigate it first.

---

# 📥 Download

Download Velora PC from the project's official **GitHub Releases** page.

The release contains the application and, when available, the Windows installer.

> **Tip:** For the safest download, use only releases published from this repository.

**Latest release:**
➡️ Go to the **Releases** section of this GitHub repository.

---

# 🖥️ System Requirements

| Requirement           | Supported                                                  |
| --------------------- | ---------------------------------------------------------- |
| Windows 10            | ✅                                                          |
| Windows 11            | ✅                                                          |
| 64-bit Windows        | Recommended                                                |
| Administrator account | Only required for certain features                         |
| Internet connection   | Only required for features that need online access/updates |

Performance and available features may vary depending on your PC and Windows configuration.

---

# ❓ FAQ

### Does Velora PC actually add RAM?

No.

Velora PC cannot physically add RAM to your computer. The RAM Boost feature attempts to reduce unnecessary memory usage and shows measured usage before and after the operation.

### Will Velora PC delete my personal files?

The built-in Deep Clean is designed to clean supported Windows system/cache locations rather than personal files.

As with any system-maintenance software, **keep backups of important data**.

### Does Velora PC require administrator access?

Not for everything.

It runs with standard permissions where possible and requests administrator access only when Windows requires it for a particular operation.

### Can I undo Game Boost?

Yes. Game Boost saves the original settings it changes so they can be restored.

### Is Velora PC free?

The current release has its features unlocked.

The project also contains an edition system that can support Free/Pro editions in the future.

### Is Velora PC open source?

Yes. The source code is available in this GitHub repository.

### Why does my antivirus say Velora PC is suspicious?

Some security products may flag new system-utility applications because they perform actions such as changing system settings or requesting administrator privileges.

This may be a false positive, but **you should always verify the file rather than simply ignoring the warning**.

---

# 🧰 For Developers

The project is structured into several main areas:

```text
src/VeloraPC/
├── Core/        Win32, logging, settings, history, admin, system info
├── Services/    Boost, Cleanup, Memory, Game, Health, Repair, Updates
├── UI/          UI toolkit, themes, page base
├── Views/       Application screens
└── Themes/      Dark and Light themes

installer/
└── VeloraPC.iss
```

The current edition switch is located in:

```text
src/VeloraPC/Core/Basics.cs
```

---

# 🔨 Building from Source

### Requirements

* Windows 10 or Windows 11
* .NET 8 SDK
* Inno Setup 6 *(only required to build the installer)*

### Build the application

You can build the application locally using:

```text
BUILD.bat
```

The published executable will be placed in:

```text
publish/VeloraPC.exe
```

### Build the installer

After building the application, install Inno Setup 6 and run:

```text
iscc installer/VeloraPC.iss
```

The installer will be generated by the Inno Setup script.

---

# 🤝 Contributing

Contributions are welcome!

If you find a bug, have an optimization idea, or want to improve Velora PC:

1. Open an issue.
2. Explain the problem or proposed improvement.
3. Include relevant logs or screenshots when appropriate.
4. Submit a pull request for code changes.

Please avoid submitting sensitive personal information, system credentials, or private files.

---

# 🐛 Bug Reports

When reporting a bug, please include:

* Windows version
* Velora PC version
* What you were trying to do
* What happened
* Any error message
* Relevant logs, if available

**Please do not post passwords, license keys, personal files, or other sensitive information.**

---

# 📄 License

See [`LICENSE`](LICENSE) for the license and usage terms of this project.

---

# ❤️ Support Velora PC

If you find Velora PC useful:

⭐ **Star the repository**

🐛 **Report bugs**

💡 **Suggest improvements**

🔧 **Contribute code**

📢 **Share the project**

---

<p align="center">
  <b>Velora PC</b><br>
  Open source. Simple. Fast. Built for Windows.
</p>
