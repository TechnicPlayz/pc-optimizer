using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCMaintenanceTool
{
    public class MainForm : Form
    {
        record TaskDef(string Key, string Name, Func<Task> Run);

        readonly CheckedListBox tasks = new();
        readonly TextBox log = new();
        readonly ProgressBar progress = new();
        readonly Label status = new();
        readonly Button run = new();
        readonly Button fastPc = new();
        readonly Button springClean = new();
        readonly Button newPc = new();
        readonly Button selectAll = new();
        readonly Button clear = new();

        readonly List<TaskDef> defs;
        readonly Dictionary<string, string> hw = new();
        bool? wingetOk;
        bool needsRestart;

        // ---- Presets (by task key) ----
        static readonly string[] FastPcKeys = {
            "temp", "bin", "dns", "gamebar", "gamemode", "power",
            "storage", "fastui", "trim", "startuplist"
        };

        static readonly string[] SpringCleanKeys = {
            "temp", "bin", "dns", "dismclean",
            "gamebar", "gamemode", "power", "storage"
        };

        static readonly string[] NewPcKeys = {
            "restore", "detect", "wudrivers", "intel", "gpu", "oem", "runtimes", "wupdates",
            "temp", "bin", "gamebar", "gamemode", "power", "storage", "ext"
        };

        public MainForm()
        {
            defs = new List<TaskDef>
            {
                // New PC setup
                new("restore",    "Create a restore point (safety net)",           CreateRestorePoint),
                new("detect",     "Detect hardware",                               DetectHardwareTask),
                new("wudrivers",  "Install Windows drivers (Microsoft Update)",    () => WindowsUpdate("Driver")),
                new("intel",      "Intel driver tool (Intel PCs only)",            InstallIntelTool),
                new("gpu",        "NVIDIA / AMD graphics & chipset software",      GpuAndChipset),
                new("oem",        "PC-maker update tool (Dell, HP, Lenovo...)",    OemTool),
                new("runtimes",   "Gaming runtimes (VC++, DirectX, .NET)",         Runtimes),
                new("wupdates",   "Install pending Windows updates",               () => WindowsUpdate("Software")),
                // Cleanup & repair
                new("temp",       "Clean temporary files",                         CleanTemp),
                new("bin",        "Empty Recycle Bin",                             EmptyBin),
                new("dns",        "Flush DNS cache",                               FlushDns),
                new("dismclean",  "DISM component cleanup",                        () => Cmd("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup")),
                new("dismrepair", "DISM RestoreHealth",                            () => Cmd("dism.exe", "/Online /Cleanup-Image /RestoreHealth")),
                new("sfc",        "SFC system-file repair",                        () => Cmd("sfc.exe", "/scannow", Encoding.Unicode)),
                new("diag",       "Run Windows diagnostics",                       Diagnostics),
                // Optimizations
                new("gamebar",    "Disable Xbox Game Bar background capture",      DisableGameBar),
                new("gamemode",   "Enable Windows Game Mode",                      EnableGameMode),
                new("power",      "Keep Windows on Balanced power plan",           () => Cmd("powercfg.exe", "/setactive SCHEME_BALANCED")),
                new("storage",    "Enable Storage Sense (auto temp cleanup)",      EnableStorageSense),
                new("ext",        "Show file extensions (safer downloads)",        ShowFileExtensions),
                new("fastui",     "Faster Windows UI (menus, startup delay, no transparency)", FastUi),
                new("trim",       "Re-TRIM SSD (keeps SSD fast)",                  TrimSsd),
                new("startuplist","List startup apps (report only)",               ListStartupApps),
            };

            Text = "PC Maintenance Tool";
            Width = 960; Height = 680;
            MinimumSize = new System.Drawing.Size(800, 580);
            StartPosition = FormStartPosition.CenterScreen;

            var header = new Label {
                Text = "PC MAINTENANCE TOOL",
                Dock = DockStyle.Top, Height = 55,
                Font = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold),
                Padding = new Padding(18, 12, 0, 0)
            };
            var subtitle = new Label {
                Text = "Spring Clean for any PC  •  New PC Setup for a fresh prebuilt",
                Dock = DockStyle.Top, Height = 32,
                Padding = new Padding(20, 0, 0, 0)
            };

            var left = new Panel { Dock = DockStyle.Left, Width = 390, Padding = new Padding(15) };

            tasks.Dock = DockStyle.Fill;
            tasks.CheckOnClick = true;
            foreach (var t in defs) tasks.Items.Add(t.Name);
            tasks.ItemCheck += (_, __) => BeginInvoke(new Action(UpdateSelectionText));

            fastPc.Text = "⚡ Fast PC";
            springClean.Text = "🧹 Spring Clean";
            newPc.Text = "🚀 New PC Setup";
            selectAll.Text = "Select All";
            clear.Text = "Clear";
            run.Text = "▶ Run Selected";

            foreach (var b in new[] { fastPc, springClean, newPc })
            {
                b.Height = 48; b.Width = 172;
                b.Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold);
            }
            foreach (var b in new[] { selectAll, clear, run })
            {
                b.Height = 38;
                b.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            }
            selectAll.Width = 100; clear.Width = 75; run.Width = 150;

            selectAll.Click += (_, __) => SetAll(true);
            clear.Click += (_, __) => SetAll(false);
            fastPc.Click += async (_, __) => await RunPreset(
                "Fast PC", FastPcKeys,
                "Fast PC is a quick, safe speed boost:\n\n" +
                "• Clean temp files, empty Recycle Bin, flush DNS\n" +
                "• Disable Game Bar background capture, enable Game Mode\n" +
                "• Keep the Balanced power plan, turn on Storage Sense\n" +
                "• Faster menus, no startup delay, transparency off\n" +
                "• Re-TRIM your SSD (skipped on hard drives)\n" +
                "• List your startup apps so you can review them (nothing is disabled)\n\n" +
                "It will NOT disable antivirus, Windows Update, services or security features.\n" +
                "All changes are reversible.\n\nContinue?");
            springClean.Click += async (_, __) => await RunPreset(
                "Spring Clean", SpringCleanKeys,
                "Spring Clean will:\n\n" +
                "• Clean temporary files and empty the Recycle Bin\n" +
                "• Flush the DNS cache and run DISM component cleanup\n" +
                "• Disable Game Bar background capture\n" +
                "• Enable Game Mode and keep the Balanced power plan\n" +
                "• Turn on Storage Sense (automatic temp cleanup)\n\n" +
                "It will NOT disable antivirus, Windows Update, services or security features.\n\nContinue?");
            newPc.Click += async (_, __) => await RunPreset(
                "New PC Setup", NewPcKeys,
                "New PC Setup will:\n\n" +
                "• Create a restore point first\n" +
                "• Detect your CPU, GPU, network and PC maker\n" +
                "• Install official drivers through Microsoft Update (skips if up to date)\n" +
                "• Install the right vendor tools (Intel / NVIDIA / AMD / Dell / HP / Lenovo...) only if missing\n" +
                "• Install VC++, DirectX and .NET runtimes if missing\n" +
                "• Install pending Windows updates\n" +
                "• Apply the Spring Clean optimizations and show file extensions\n\n" +
                "It does NOT remove programs or disable security features.\n" +
                "This can take a while and may need a restart.\n\nContinue?");
            run.Click += async (_, __) => await RunSelected();

            newPc.Width = 350;
            var presets = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 115, WrapContents = true };
            presets.Controls.Add(fastPc);
            presets.Controls.Add(springClean);
            presets.Controls.Add(newPc);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 55, WrapContents = false };
            buttons.Controls.Add(selectAll);
            buttons.Controls.Add(clear);
            buttons.Controls.Add(run);

            // Dock order: Fill first, then Bottom, then Top
            left.Controls.Add(tasks);
            left.Controls.Add(buttons);
            left.Controls.Add(presets);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            status.Text = "Ready";
            status.Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold);
            status.Dock = DockStyle.Top; status.Height = 35;
            progress.Dock = DockStyle.Top; progress.Height = 25;

            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.Dock = DockStyle.Fill;

            right.Controls.Add(log);
            right.Controls.Add(progress);
            right.Controls.Add(status);

            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(subtitle);
            Controls.Add(header);

            Log(IsAdministrator()
                ? "Administrator access detected."
                : "Run as Administrator for full functionality.");
            Log("All presets use conservative, low-risk changes only.");
            UpdateSelectionText();
        }

        // ================= UI helpers =================

        void SetAll(bool value)
        {
            for (int i = 0; i < tasks.Items.Count; i++)
                tasks.SetItemChecked(i, value);
            UpdateSelectionText();
        }

        void UpdateSelectionText()
        {
            status.Text = $"{tasks.CheckedItems.Count} task(s) selected";
        }

        void SetEnabled(bool value)
        {
            tasks.Enabled = selectAll.Enabled = clear.Enabled = run.Enabled =
                fastPc.Enabled = springClean.Enabled = newPc.Enabled = value;
        }

        static bool IsAdministrator()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        void Log(string text)
        {
            if (InvokeRequired) {
                try { BeginInvoke(new Action(() => Log(text))); } catch { }
                return;
            }
            log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        }

        // ================= Running tasks =================

        async Task RunPreset(string title, string[] keys, string message)
        {
            SetAll(false);
            for (int i = 0; i < defs.Count; i++)
                if (keys.Contains(defs[i].Key)) tasks.SetItemChecked(i, true);
            UpdateSelectionText();

            var answer = MessageBox.Show(message, title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            await RunTasks(tasks.CheckedIndices.Cast<int>().ToList(), title);
        }

        async Task RunSelected()
        {
            if (tasks.CheckedItems.Count == 0) {
                MessageBox.Show("Select at least one task.", "Nothing selected");
                return;
            }
            await RunTasks(tasks.CheckedIndices.Cast<int>().ToList(), "Selected tasks");
        }

        async Task RunTasks(List<int> selected, string title)
        {
            SetEnabled(false);
            progress.Value = 0;
            needsRestart = false;
            int failed = 0;

            try {
                Log($"=== {title} started ===");
                for (int x = 0; x < selected.Count; x++) {
                    var def = defs[selected[x]];
                    status.Text = $"({x + 1}/{selected.Count}) {def.Name}";
                    try { await def.Run(); }
                    catch (Exception ex) {
                        failed++;
                        Log($"SKIPPED '{def.Name}': {ex.Message}");
                    }
                    progress.Value = (int)((x + 1) * 100.0 / selected.Count);
                }
                status.Text = failed == 0 ? $"{title} complete" : $"{title} complete ({failed} skipped)";
                Log($"=== {title} finished ===");

                if (needsRestart) {
                    var r = MessageBox.Show(
                        "Windows needs a restart to finish installing updates/drivers.\n\nRestart now?",
                        "Restart recommended", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes)
                        await Exec("shutdown.exe", "/r /t 10");
                }
            }
            catch (Exception ex) { Log("ERROR: " + ex.Message); }
            finally { SetEnabled(true); }
        }

        // ================= Process helpers =================

        static readonly Regex Noise = new(@"^\d+(\.\d+)?\s*(KB|MB|GB)\s*/", RegexOptions.IgnoreCase);

        static string Clean(string s)
        {
            s = s.Replace("\0", "").Trim();
            if (s.Length <= 1) return "";
            if (s.Contains('█') || s.Contains('▒')) return "";
            if (s.StartsWith("#< CLIXML") || s.StartsWith("<Objs")) return "";
            if (Noise.IsMatch(s)) return "";
            return s;
        }

        async Task<(int Code, string Output)> Exec(
            string file, string args, string? label = null, bool echo = true, Encoding? enc = null)
        {
            if (echo) Log("> " + (label ?? $"{file} {args}"));

            var sb = new StringBuilder();
            var psi = new ProcessStartInfo {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = enc ?? Encoding.UTF8,
                StandardErrorEncoding = enc ?? Encoding.UTF8
            };

            try {
                using var p = new Process { StartInfo = psi };
                DataReceivedEventHandler handler = (_, e) => {
                    if (e.Data == null) return;
                    var line = Clean(e.Data);
                    if (line.Length == 0) return;
                    lock (sb) sb.AppendLine(line);
                    if (echo) Log(line);
                };
                p.OutputDataReceived += handler;
                p.ErrorDataReceived += handler;

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await p.WaitForExitAsync();

                if (echo) Log($"Finished (exit code {p.ExitCode})");
                lock (sb) return (p.ExitCode, sb.ToString());
            }
            catch (Exception ex) {
                Log($"Could not run {file}: {ex.Message}");
                return (-1, "");
            }
        }

        Task<(int Code, string Output)> PS(string script, string label, bool echo = true)
        {
            var full = "[Console]::OutputEncoding=[Text.Encoding]::UTF8;" +
                       "$ProgressPreference='SilentlyContinue';" + script;
            var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
            return Exec("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + enc, label, echo);
        }

        async Task Cmd(string file, string args, Encoding? enc = null)
        {
            await Exec(file, args, null, true, enc);
        }

        void OpenPage(string name, string url)
        {
            Log($"Opening the official {name} page: {url}");
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Log("Could not open browser: " + ex.Message); }
        }

        // ================= Hardware detection =================

        const string HardwareScript = """
            $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
            $gpu = Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name }
            $cs  = Get-CimInstance Win32_ComputerSystem
            $bb  = Get-CimInstance Win32_BaseBoard
            $net = Get-CimInstance Win32_NetworkAdapter | Where-Object { $_.PhysicalAdapter -and $_.Name -notmatch 'Virtual|Hyper-V|VPN|TAP|Miniport|Bluetooth' } | ForEach-Object { $_.Name }
            'CPU=' + $cpu.Name
            'CPUMFR=' + $cpu.Manufacturer
            'GPU=' + ($gpu -join '; ')
            'PCMFR=' + $cs.Manufacturer
            'MODEL=' + $cs.Model
            'BOARDMFR=' + $bb.Manufacturer
            'NET=' + ($net -join '; ')
            """;

        async Task EnsureHardware()
        {
            if (hw.Count > 0) return;
            var (_, output) = await PS(HardwareScript, "Detecting hardware...", false);
            foreach (var line in output.Split('\n'))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                hw[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
        }

        bool Has(string key, string needle) =>
            hw.TryGetValue(key, out var v) && v.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        string Get(string key) => hw.TryGetValue(key, out var v) ? v : "";

        async Task DetectHardwareTask()
        {
            hw.Clear();
            await EnsureHardware();
            Log("Detected hardware:");
            Log("  CPU:      " + Get("CPU"));
            Log("  Graphics: " + Get("GPU"));
            Log("  PC:       " + Get("PCMFR") + " " + Get("MODEL"));
            Log("  Board:    " + Get("BOARDMFR"));
            Log("  Network:  " + Get("NET"));
        }

        // ================= winget helpers =================

        async Task<bool> HasWinget()
        {
            if (wingetOk.HasValue) return wingetOk.Value;
            var (code, _) = await Exec("winget.exe", "--version", null, false);
            wingetOk = code == 0;
            if (!wingetOk.Value)
                Log("winget is not available. Install 'App Installer' from the Microsoft Store, then run again.");
            return wingetOk.Value;
        }

        /// <summary>Installs the package if missing, upgrades it if present, skips if up to date.</summary>
        async Task<bool> WingetEnsure(string id, string friendly)
        {
            if (!await HasWinget()) return false;

            var (lc, lo) = await Exec("winget.exe",
                $"list --id {id} -e --accept-source-agreements", null, false);
            bool installed = lc == 0 &&
                lo.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0 &&
                lo.IndexOf("No installed package", StringComparison.OrdinalIgnoreCase) < 0;

            if (installed)
            {
                Log($"{friendly} is installed - checking for updates...");
                var (_, uo) = await Exec("winget.exe",
                    $"upgrade --id {id} -e --silent --accept-package-agreements --accept-source-agreements", null, false);
                if (uo.Contains("No available upgrade", StringComparison.OrdinalIgnoreCase) ||
                    uo.Contains("No newer package", StringComparison.OrdinalIgnoreCase))
                    Log($"{friendly}: already up to date - skipped.");
                else
                    Log($"{friendly}: update step finished.");
                return true;
            }

            Log($"Installing {friendly}...");
            var (ic, io) = await Exec("winget.exe",
                $"install --id {id} -e --silent --accept-package-agreements --accept-source-agreements");
            if (io.Contains("No package found", StringComparison.OrdinalIgnoreCase)) return false;
            return ic == 0;
        }

        async Task<bool> IsInstalled(string regexPattern)
        {
            var script = """
                $p = '__PAT__'
                $keys = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*','HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
                $found = @(Get-ItemProperty $keys -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match $p })
                if ($found.Count -gt 0) { 'INSTALLED' } else { 'MISSING' }
                """.Replace("__PAT__", regexPattern);
            var (_, o) = await PS(script, "Checking installed software...", false);
            return o.Contains("INSTALLED");
        }

        // ================= New PC Setup tasks =================

        async Task CreateRestorePoint()
        {
            Log("Creating a restore point...");
            var (_, o) = await PS("""
                try {
                    Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction SilentlyContinue
                    Checkpoint-Computer -Description 'PC Maintenance Tool' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
                    Write-Output 'Restore point created.'
                } catch { Write-Output ('Restore point not created: ' + $_.Exception.Message) }
                """, "Create restore point");
        }

        async Task WindowsUpdate(string type)
        {
            Log(type == "Driver"
                ? "Checking Microsoft Update for official drivers..."
                : "Checking Windows Update for pending updates...");

            var script = """
                try {
                    $session = New-Object -ComObject Microsoft.Update.Session
                    $result = $session.CreateUpdateSearcher().Search("IsInstalled=0 and Type='__TYPE__' and IsHidden=0")
                    $list = New-Object -ComObject Microsoft.Update.UpdateColl
                    foreach ($u in $result.Updates) {
                        if ($u.InstallationBehavior.CanRequestUserInput) { continue }
                        if (-not $u.EulaAccepted) { $u.AcceptEula() }
                        Write-Output ('Found: ' + $u.Title)
                        [void]$list.Add($u)
                    }
                    if ($list.Count -eq 0) { Write-Output 'Nothing to install - already up to date, skipped.'; return }
                    Write-Output 'Downloading...'
                    $dl = $session.CreateUpdateDownloader(); $dl.Updates = $list; [void]$dl.Download()
                    Write-Output 'Installing...'
                    $inst = $session.CreateUpdateInstaller(); $inst.Updates = $list
                    $res = $inst.Install()
                    Write-Output ('Install result code: ' + $res.ResultCode + ' (2 = success)')
                    if ($res.RebootRequired) { Write-Output 'REBOOT REQUIRED' }
                } catch { Write-Output ('Windows Update error: ' + $_.Exception.Message) }
                """.Replace("__TYPE__", type);

            var (_, o) = await PS(script, "Windows Update (" + type + ")");
            if (o.Contains("REBOOT REQUIRED")) needsRestart = true;
        }

        async Task InstallIntelTool()
        {
            await EnsureHardware();
            if (!(Has("CPUMFR", "Intel") || Has("GPU", "Intel") || Has("NET", "Intel")))
            {
                Log("No Intel hardware detected - skipped.");
                return;
            }
            bool ok = await WingetEnsure("Intel.IntelDriverAndSupportAssistant", "Intel Driver & Support Assistant");
            if (ok)
                Log("Tip: open 'Intel Driver & Support Assistant' from the Start menu to scan for Intel chipset, graphics and network updates.");
            else
                OpenPage("Intel Driver & Support Assistant", "https://www.intel.com/content/www/us/en/support/detect.html");
        }

        async Task GpuAndChipset()
        {
            await EnsureHardware();
            bool nvidia = Has("GPU", "NVIDIA");
            bool amdGpu = Has("GPU", "AMD") || Has("GPU", "Radeon");
            bool amdCpu = Has("CPUMFR", "AMD");

            if (!nvidia && !amdGpu && !amdCpu)
            {
                Log("No NVIDIA or AMD hardware detected - skipped.");
                return;
            }

            if (nvidia)
            {
                if (await IsInstalled("NVIDIA app|GeForce Experience"))
                    Log("NVIDIA app is already installed (it keeps the GPU driver updated) - skipped.");
                else
                    OpenPage("NVIDIA app", "https://www.nvidia.com/en-us/software/nvidia-app/");
            }

            if (amdGpu || amdCpu)
            {
                bool needGpu = amdGpu && !await IsInstalled("^AMD Software");
                bool needChipset = amdCpu && !await IsInstalled("AMD Chipset");
                if (!needGpu && !needChipset)
                    Log("AMD Software / Chipset already installed - skipped.");
                else
                {
                    var what = new List<string>();
                    if (needGpu) what.Add("graphics");
                    if (needChipset) what.Add("chipset");
                    Log("AMD " + string.Join(" + ", what) + " software is missing.");
                    OpenPage("AMD drivers", "https://www.amd.com/en/support/download/drivers.html");
                }
            }
        }

        async Task OemTool()
        {
            await EnsureHardware();
            var maker = Get("PCMFR");
            if (Regex.IsMatch(maker, "system manufacturer|to be filled|default string|^$", RegexOptions.IgnoreCase))
                maker = Get("BOARDMFR");

            var oems = new (string Pattern, string Name, string? WingetId, string Url)[]
            {
                (@"dell|alienware",            "Dell Command | Update",  "Dell.CommandUpdate",        "https://www.dell.com/support/home"),
                (@"\bhp\b|hewlett|omen",       "HP Support Assistant",   "HP.HPSupportAssistant",     "https://support.hp.com/drivers"),
                (@"lenovo",                    "Lenovo System Update",   "Lenovo.SystemUpdate",       "https://support.lenovo.com/"),
                (@"asus",                      "ASUS support",           null,                        "https://www.asus.com/support/"),
                (@"micro-star|\bmsi\b",        "MSI support",            null,                        "https://www.msi.com/support"),
                (@"acer|packard|predator",     "Acer support",           null,                        "https://www.acer.com/support"),
                (@"gigabyte|aorus",            "Gigabyte support",       null,                        "https://www.gigabyte.com/Support"),
                (@"asrock",                    "ASRock support",         null,                        "https://www.asrock.com/support/"),
            };

            foreach (var o in oems)
            {
                if (!Regex.IsMatch(maker, o.Pattern, RegexOptions.IgnoreCase)) continue;

                Log($"PC maker detected: {maker}");
                if (o.WingetId != null)
                {
                    if (await WingetEnsure(o.WingetId, o.Name))
                    {
                        Log($"Tip: open '{o.Name}' from the Start menu to apply any PC-specific firmware/BIOS/driver updates.");
                        return;
                    }
                    Log($"{o.Name} is not available through winget.");
                }
                OpenPage(o.Name, o.Url);
                return;
            }

            Log($"No known PC maker ('{maker}') - skipped. Microsoft Update drivers cover the basics.");
        }

        async Task Runtimes()
        {
            var items = new (string Id, string Name)[]
            {
                ("Microsoft.VCRedist.2015+.x64",       "Visual C++ Runtime (x64)"),
                ("Microsoft.DirectX",                  "DirectX runtime"),
                ("Microsoft.DotNet.DesktopRuntime.8",  ".NET 8 Desktop Runtime")
            };
            foreach (var (id, name) in items)
            {
                if (!await WingetEnsure(id, name))
                    Log($"{name}: could not be installed via winget (skipped).");
            }
        }

        // ================= Cleanup & repair tasks =================

        async Task CleanTemp()
        {
            Log("Cleaning temporary files...");
            await Task.Run(() =>
            {
                DeleteContents(Path.GetTempPath());
                DeleteContents(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
            });
            Log("Temporary-file cleanup finished. Locked files were skipped.");
        }

        async Task EmptyBin()
        {
            Log("Emptying Recycle Bin...");
            await PS("Clear-RecycleBin -Force -ErrorAction SilentlyContinue", "Clear-RecycleBin");
        }

        async Task FlushDns()
        {
            Log("Flushing DNS cache...");
            await Cmd("ipconfig.exe", "/flushdns");
        }

        async Task Diagnostics()
        {
            Log("Running Windows diagnostics...");
            await Cmd("dism.exe", "/Online /Cleanup-Image /CheckHealth");
            await Cmd("sfc.exe", "/verifyonly", Encoding.Unicode);
            await Cmd("chkdsk.exe", "/scan");
        }

        // ================= Optimization tasks =================

        async Task DisableGameBar()
        {
            Log("Disabling Xbox Game Bar background capture...");
            await Cmd("reg.exe", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR"" /v AppCaptureEnabled /t REG_DWORD /d 0 /f");
            await Cmd("reg.exe", @"add ""HKCU\System\GameConfigStore"" /v GameDVR_Enabled /t REG_DWORD /d 0 /f");
        }

        async Task EnableGameMode()
        {
            Log("Enabling Windows Game Mode...");
            await Cmd("reg.exe", @"add ""HKCU\Software\Microsoft\GameBar"" /v AutoGameModeEnabled /t REG_DWORD /d 1 /f");
        }

        async Task EnableStorageSense()
        {
            Log("Enabling Storage Sense...");
            await Cmd("reg.exe", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy"" /v 01 /t REG_DWORD /d 1 /f");
        }

        async Task ShowFileExtensions()
        {
            Log("Showing file extensions in File Explorer...");
            await Cmd("reg.exe", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v HideFileExt /t REG_DWORD /d 0 /f");
        }

        async Task FastUi()
        {
            Log("Applying faster UI settings (all reversible)...");
            await Cmd("reg.exe", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v EnableTransparency /t REG_DWORD /d 0 /f");
            await Cmd("reg.exe", @"add ""HKCU\Control Panel\Desktop"" /v MenuShowDelay /t REG_SZ /d 100 /f");
            await Cmd("reg.exe", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize"" /v StartupDelayInMSec /t REG_DWORD /d 0 /f");
            Log("Sign out and back in (or restart) for these to fully apply.");
        }

        async Task TrimSsd()
        {
            Log("Checking system drive type...");
            await PS("""
                try {
                    $letter = $env:SystemDrive.TrimEnd(':')
                    $part = Get-Partition -DriveLetter $letter -ErrorAction Stop
                    $pd = Get-PhysicalDisk | Where-Object { $_.DeviceId -eq "$($part.DiskNumber)" } | Select-Object -First 1
                    if ($pd -and $pd.MediaType -eq 'SSD') {
                        Optimize-Volume -DriveLetter $letter -ReTrim -ErrorAction Stop
                        Write-Output 'SSD re-trim finished.'
                    } else {
                        Write-Output 'System drive is not an SSD - skipped (Windows maintains hard drives itself).'
                    }
                } catch { Write-Output ('TRIM skipped: ' + $_.Exception.Message) }
                """, "SSD re-trim");
        }

        async Task ListStartupApps()
        {
            Log("Startup apps (report only - nothing is disabled):");
            await PS("""
                $items = @(Get-CimInstance Win32_StartupCommand -ErrorAction SilentlyContinue)
                if ($items.Count -eq 0) { Write-Output '  (none found)' }
                foreach ($i in $items) { Write-Output ('  ' + $i.Name) }
                Write-Output 'Tip: Task Manager > Startup apps lets you switch off ones you do not need.'
                """, "List startup apps");
        }

        void DeleteContents(string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;

            try {
                foreach (var f in Directory.EnumerateFiles(folder))
                    try { File.Delete(f); } catch { }

                foreach (var d in Directory.EnumerateDirectories(folder))
                    try { Directory.Delete(d, true); } catch { }
            } catch { }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
