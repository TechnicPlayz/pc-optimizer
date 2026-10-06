using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCMaintenanceTool
{
    public class MainForm : Form
    {
        readonly CheckedListBox tasks = new();
        readonly TextBox log = new();
        readonly ProgressBar progress = new();
        readonly Label status = new();
        readonly Button run = new();
        readonly Button fast = new();
        readonly Button selectAll = new();
        readonly Button clear = new();

        readonly string[] taskNames = {
            "Clean temporary files",
            "Empty Recycle Bin",
            "DISM component cleanup",
            "DISM RestoreHealth",
            "SFC system-file repair",
            "Run Windows diagnostics",
            "Disable Xbox Game Bar background capture",
            "Enable Windows Game Mode",
            "Keep Windows on Balanced power plan"
        };

        public MainForm()
        {
            Text = "PC Maintenance Tool";
            Width = 900; Height = 650;
            MinimumSize = new System.Drawing.Size(760, 560);
            StartPosition = FormStartPosition.CenterScreen;

            var header = new Label {
                Text = "PC MAINTENANCE TOOL",
                Dock = DockStyle.Top, Height = 55,
                Font = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold),
                Padding = new Padding(18, 12, 0, 0)
            };
            var subtitle = new Label {
                Text = "Safe cleanup, repair, diagnostics and gaming optimizations",
                Dock = DockStyle.Top, Height = 32,
                Padding = new Padding(20, 0, 0, 0)
            };

            var left = new Panel { Dock = DockStyle.Left, Width = 350, Padding = new Padding(15) };

            tasks.Dock = DockStyle.Fill;
            tasks.CheckOnClick = true;
            foreach (var t in taskNames) tasks.Items.Add(t);

            selectAll.Text = "Select All";
            clear.Text = "Clear";
            fast.Text = "⚡ FAST PC";
            run.Text = "▶ Run Selected";

            foreach (var b in new[] { selectAll, clear, fast, run })
            {
                b.Height = 40;
                b.Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            }
            selectAll.Width = 95; clear.Width = 80; fast.Width = 115; run.Width = 135;

            selectAll.Click += (_, __) => SetAll(true);
            clear.Click += (_, __) => SetAll(false);
            fast.Click += async (_, __) => await RunFastPC();
            run.Click += async (_, __) => await RunSelected();

            var buttons = new FlowLayoutPanel {
                Dock = DockStyle.Bottom, Height = 95, WrapContents = true
            };
            buttons.Controls.Add(selectAll);
            buttons.Controls.Add(clear);
            buttons.Controls.Add(fast);
            buttons.Controls.Add(run);

            left.Controls.Add(tasks);
            left.Controls.Add(buttons);

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
                : "Run as Administrator for full DISM/SFC functionality.");
            Log("Fast PC mode uses conservative, low-risk changes.");
            UpdateSelectionText();
        }

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
            tasks.Enabled = selectAll.Enabled = clear.Enabled = fast.Enabled = run.Enabled = value;
        }

        static bool IsAdministrator()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        void Log(string text)
        {
            if (InvokeRequired) {
                BeginInvoke(new Action(() => Log(text)));
                return;
            }
            log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        }

        async Task RunSelected()
        {
            if (tasks.CheckedItems.Count == 0) {
                MessageBox.Show("Select at least one task.", "Nothing selected");
                return;
            }

            SetEnabled(false);
            progress.Value = 0;
            var selected = tasks.CheckedIndices.Cast<int>().ToList();

            try {
                for (int x = 0; x < selected.Count; x++) {
                    await ExecuteTask(selected[x]);
                    progress.Value = (int)((x + 1) * 100.0 / selected.Count);
                }
                status.Text = "Finished";
                Log("All selected tasks completed.");
            }
            catch (Exception ex) { Log("ERROR: " + ex.Message); }
            finally { SetEnabled(true); }
        }

        async Task RunFastPC()
        {
            var answer = MessageBox.Show(
                "FAST PC performs only conservative changes:\n\n" +
                "• Clean temporary files\n" +
                "• Empty Recycle Bin\n" +
                "• Disable Game Bar background capture\n" +
                "• Enable Game Mode\n" +
                "• Keep Balanced power plan\n\n" +
                "It will NOT disable antivirus, Windows Update, core services, or security features.\n\nContinue?",
                "Fast PC", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (answer != DialogResult.Yes) return;

            SetEnabled(false);
            progress.Value = 0;

            try {
                int[] safe = { 0, 1, 6, 7, 8 };
                for (int i = 0; i < safe.Length; i++) {
                    await ExecuteTask(safe[i]);
                    progress.Value = (i + 1) * 100 / safe.Length;
                }
                status.Text = "Fast PC optimization complete";
                Log("Fast PC finished. Restart Windows if desired.");
            }
            catch (Exception ex) { Log("ERROR: " + ex.Message); }
            finally { SetEnabled(true); }
        }

        async Task ExecuteTask(int index)
        {
            switch (index) {
                case 0:
                    Log("Cleaning temporary files...");
                    DeleteContents(Path.GetTempPath());
                    DeleteContents(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
                    Log("Temporary-file cleanup finished. Locked files were skipped.");
                    break;

                case 1:
                    Log("Emptying Recycle Bin...");
                    await RunProcess("powershell.exe",
                        "-NoProfile -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"");
                    break;

                case 2:
                    Log("Running DISM component cleanup...");
                    await RunProcess("dism.exe",
                        "/Online /Cleanup-Image /StartComponentCleanup");
                    break;

                case 3:
                    Log("Repairing Windows component store...");
                    await RunProcess("dism.exe",
                        "/Online /Cleanup-Image /RestoreHealth");
                    break;

                case 4:
                    Log("Running SFC system-file repair...");
                    await RunProcess("sfc.exe", "/scannow");
                    break;

                case 5:
                    Log("Running Windows diagnostics...");
                    await RunProcess("dism.exe",
                        "/Online /Cleanup-Image /CheckHealth");
                    await RunProcess("sfc.exe", "/verifyonly");
                    await RunProcess("chkdsk.exe", "/scan");
                    break;

                case 6:
                    Log("Disabling Xbox Game Bar background capture...");
                    await RunProcess("reg.exe",
                        @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR"" /v AppCaptureEnabled /t REG_DWORD /d 0 /f");
                    await RunProcess("reg.exe",
                        @"add ""HKCU\System\GameConfigStore"" /v GameDVR_Enabled /t REG_DWORD /d 0 /f");
                    break;

                case 7:
                    Log("Enabling Windows Game Mode...");
                    await RunProcess("reg.exe",
                        @"add ""HKCU\Software\Microsoft\GameBar"" /v AutoGameModeEnabled /t REG_DWORD /d 1 /f");
                    break;

                case 8:
                    Log("Setting Balanced power plan...");
                    await RunProcess("powercfg.exe", "/setactive SCHEME_BALANCED");
                    break;
            }
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

        async Task RunProcess(string file, string args)
        {
            Log($"> {file} {args}");
            var psi = new ProcessStartInfo {
                FileName = file,
                Arguments = args,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            using var p = Process.Start(psi)
                ?? throw new Exception("Could not start process.");

            await p.WaitForExitAsync();
            Log($"Finished: exit code {p.ExitCode}");
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
