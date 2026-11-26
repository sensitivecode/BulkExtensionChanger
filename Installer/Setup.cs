using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BulkExtensionChanger.Setup
{
    internal static class Program
    {
        private const string AppName = "Массовая смена расширений";
        private const string AppInternalName = "BulkExtensionChanger";
        private const string UninstallRegPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BulkExtensionChanger";
        private const string PayloadResourceName = "BulkExtensionChanger.Setup.payload.zip";

        internal static string AppVersion
        {
            get
            {
                try
                {
                    var ver = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
                    if (!string.IsNullOrEmpty(ver))
                    {
                        int plus = ver.IndexOf('+');
                        return plus > 0 ? ver.Substring(0, plus) : ver;
                    }
                }
                catch { }
                return "1.1.0";
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool silent = HasArg(args, "--silent") || HasArg(args, "/S");
            bool isUninstall = HasArg(args, "--uninstall");
            string uninstallTargetDir = GetArgValue(args, "--uninstall-worker");

            if (!string.IsNullOrEmpty(uninstallTargetDir))
            {
                return RunUninstallWorker(uninstallTargetDir, silent);
            }

            if (isUninstall)
            {
                return LaunchUninstall(silent);
            }

            string defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                AppInternalName);

            if (silent)
            {
                string targetDir = GetArgValue(args, "--dir") ?? defaultDir;
                bool addContextMenu = !HasArg(args, "--no-context-menu");
                bool startMenu = !HasArg(args, "--no-start-menu");
                bool desktop = HasArg(args, "--desktop");
                InstallFiles(targetDir, addContextMenu, startMenu, desktop);
                return 0;
            }

            using (var form = new SetupWizardForm(defaultDir))
            {
                Application.Run(form);
            }
            return 0;
        }

        private static bool HasArg(string[] args, string flag)
        {
            foreach (var a in args)
            {
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetArgValue(string[] args, string prefix)
        {
            string eqPrefix = prefix + "=";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(eqPrefix, StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(eqPrefix.Length).Trim('"');
                if (string.Equals(args[i], prefix, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1].Trim('"');
            }
            return null;
        }

        internal static bool IsDotNet8DesktopInstalled()
        {
            try
            {
                string sharedFx = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "dotnet", "shared", "Microsoft.WindowsDesktop.App");
                if (Directory.Exists(sharedFx))
                {
                    foreach (var dir in Directory.GetDirectories(sharedFx, "8.*"))
                        return true;
                }
            }
            catch { }
            return false;
        }

        internal static void InstallFiles(string installDir, bool addContextMenu, bool createStartMenu, bool createDesktop)
        {
            installDir = Path.GetFullPath(installDir);
            Directory.CreateDirectory(installDir);

            KillRunningAppInDirectory(installDir);

            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream(PayloadResourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("Встроенный архив приложения не найден.");

                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        string destPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));
                        if (!destPath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                            continue;

                        string parent = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(parent))
                            Directory.CreateDirectory(parent);

                        entry.ExtractToFile(destPath, true);
                    }
                }
            }

            string selfExe = asm.Location;
            string uninstallExe = Path.Combine(installDir, "Uninstall.exe");
            if (!string.Equals(selfExe, uninstallExe, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(selfExe, uninstallExe, true);
            }

            string mainExe = Path.Combine(installDir, AppInternalName + ".exe");

            if (addContextMenu && File.Exists(mainExe))
            {
                RunProcessAndWait(mainExe, "--install");
            }

            if (createStartMenu)
            {
                string startMenuDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    AppName);
                Directory.CreateDirectory(startMenuDir);
                CreateShortcut(Path.Combine(startMenuDir, AppName + ".lnk"), mainExe, installDir, null);
                CreateShortcut(Path.Combine(startMenuDir, "Удалить " + AppName + ".lnk"), uninstallExe, installDir, "--uninstall");
            }

            if (createDesktop)
            {
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcut(Path.Combine(desktopDir, AppName + ".lnk"), mainExe, installDir, null);
            }

            RegisterUninstaller(installDir, mainExe, uninstallExe);
        }

        private static void RegisterUninstaller(string installDir, string mainExe, string uninstallExe)
        {
            long totalBytes = 0;
            try
            {
                foreach (var file in Directory.GetFiles(installDir, "*", SearchOption.AllDirectories))
                    totalBytes += new FileInfo(file).Length;
            }
            catch { }

            using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegPath))
            {
                key.SetValue("DisplayName", AppName + " (" + AppInternalName + ")");
                key.SetValue("DisplayVersion", AppVersion);
                key.SetValue("Publisher", AppInternalName);
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", "\"" + mainExe + "\",0");
                key.SetValue("UninstallString", "\"" + uninstallExe + "\" --uninstall");
                key.SetValue("QuietUninstallString", "\"" + uninstallExe + "\" --uninstall --silent");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", (int)Math.Max(1, totalBytes / 1024), RegistryValueKind.DWord);
            }
        }

        private static int LaunchUninstall(bool silent)
        {
            if (!silent)
            {
                var answer = MessageBox.Show(
                    "Удалить «" + AppName + "» и убрать пункт из контекстного меню Windows?",
                    "Удаление программы",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (answer != DialogResult.Yes)
                    return 0;
            }

            string installDir = null;
            using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegPath))
            {
                if (key != null)
                    installDir = key.GetValue("InstallLocation") as string;
            }

            if (string.IsNullOrEmpty(installDir))
            {
                installDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }

            if (string.IsNullOrEmpty(installDir))
                return 1;

            string tempUninst = Path.Combine(Path.GetTempPath(), "bec_uninst_" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Assembly.GetExecutingAssembly().Location, tempUninst, true);

            string args = "--uninstall-worker=\"" + installDir + "\"" + (silent ? " --silent" : "");
            Process.Start(new ProcessStartInfo(tempUninst, args) { UseShellExecute = false });
            return 0;
        }

        private static int RunUninstallWorker(string installDir, bool silent)
        {
            Thread.Sleep(400);

            string mainExe = Path.Combine(installDir, AppInternalName + ".exe");
            if (File.Exists(mainExe))
            {
                RunProcessAndWait(mainExe, "--uninstall");
            }

            KillRunningAppInDirectory(installDir);

            try
            {
                string startMenuDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    AppName);
                if (Directory.Exists(startMenuDir))
                    Directory.Delete(startMenuDir, true);
            }
            catch { }

            try
            {
                string desktopShortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    AppName + ".lnk");
                if (File.Exists(desktopShortcut))
                    File.Delete(desktopShortcut);
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegPath, false);
            }
            catch { }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(installDir))
                        Directory.Delete(installDir, true);
                    break;
                }
                catch
                {
                    Thread.Sleep(300);
                }
            }

            if (!silent)
            {
                MessageBox.Show(
                    "Программа «" + AppName + "» полностью удалена.",
                    "Удаление завершено",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            try
            {
                string self = Assembly.GetExecutingAssembly().Location;
                Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 2 > nul & del /f /q \"" + self + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }

            return 0;
        }

        private static void KillRunningAppInDirectory(string installDir)
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(AppInternalName))
                {
                    try
                    {
                        string procPath = proc.MainModule != null ? proc.MainModule.FileName : null;
                        if (procPath != null && procPath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                        {
                            proc.Kill();
                            proc.WaitForExit(2000);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void RunProcessAndWait(string fileName, string args)
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(fileName, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    if (p != null)
                        p.WaitForExit(5000);
                }
            }
            catch { }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string arguments)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return;

            object shell = Activator.CreateInstance(shellType);
            try
            {
                object shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
                if (!string.IsNullOrEmpty(arguments))
                    scType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

                Marshal.ReleaseComObject(shortcut);
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }

    internal sealed class SetupWizardForm : Form
    {
        private readonly Panel _pagePanel;
        private readonly Button _backBtn;
        private readonly Button _nextBtn;
        private readonly Button _cancelBtn;

        private readonly CheckBox _acceptLicenseCheck;
        private readonly TextBox _pathBox;
        private readonly CheckBox _ctxMenuCheck;
        private readonly CheckBox _startMenuCheck;
        private readonly CheckBox _desktopCheck;
        private readonly CheckBox _launchAfterCheck;

        private int _step;
        private string _installDir;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public SetupWizardForm(string defaultDir)
        {
            _installDir = defaultDir;

            Text = "Установка — Массовая смена расширений";
            ClientSize = new Size(540, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 24, 27);
            ForeColor = Color.FromArgb(244, 244, 246);
            Font = new Font("Segoe UI", 9.5f);

            try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }

            HandleCreated += delegate
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            };

            _pagePanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 12)
            };

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(34, 34, 39)
            };

            _cancelBtn = CreateButton("Отмена", new Point(436, 13), false);
            _cancelBtn.Click += delegate { Close(); };

            _nextBtn = CreateButton("Далее >", new Point(332, 13), true);
            _nextBtn.Click += delegate { OnNext(); };

            _backBtn = CreateButton("< Назад", new Point(228, 13), false);
            _backBtn.Click += delegate { ShowStep(_step - 1); };

            footer.Controls.Add(_backBtn);
            footer.Controls.Add(_nextBtn);
            footer.Controls.Add(_cancelBtn);

            Controls.Add(_pagePanel);
            Controls.Add(footer);

            _acceptLicenseCheck = new CheckBox
            {
                Text = "Я принимаю условия лицензии GNU GPL v3.0",
                Checked = true,
                AutoSize = true,
                ForeColor = ForeColor
            };
            _acceptLicenseCheck.CheckedChanged += delegate
            {
                if (_step == 0) _nextBtn.Enabled = _acceptLicenseCheck.Checked;
            };

            _pathBox = new TextBox
            {
                Text = _installDir,
                BackColor = Color.FromArgb(44, 44, 50),
                ForeColor = ForeColor,
                BorderStyle = BorderStyle.FixedSingle,
                Width = 370
            };

            _ctxMenuCheck = new CheckBox { Text = "Добавить в контекстное меню Windows", Checked = true, AutoSize = true, ForeColor = ForeColor };
            _startMenuCheck = new CheckBox { Text = "Создать ярлык в меню «Пуск»", Checked = true, AutoSize = true, ForeColor = ForeColor };
            _desktopCheck = new CheckBox { Text = "Создать ярлык на Рабочем столе", Checked = false, AutoSize = true, ForeColor = ForeColor };
            _launchAfterCheck = new CheckBox { Text = "Запустить приложение после установки", Checked = true, AutoSize = true, ForeColor = ForeColor };

            ShowStep(0);
        }

        private static Button CreateButton(string text, Point loc, bool primary)
        {
            var btn = new Button
            {
                Text = text,
                Location = loc,
                Size = new Size(94, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Color.FromArgb(0, 120, 212) : Color.FromArgb(44, 44, 50),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = primary ? Color.FromArgb(0, 120, 212) : Color.FromArgb(62, 62, 70);
            return btn;
        }

        private void ShowStep(int step)
        {
            _step = step;
            _pagePanel.Controls.Clear();

            if (step == 0)
            {
                var title = new Label
                {
                    Text = "Добро пожаловать в мастер установки\n«Массовая смена расширений» v" + Program.AppVersion,
                    Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                    Location = new Point(24, 20),
                    Size = new Size(490, 52)
                };

                var desc = new Label
                {
                    Text = "Локальная утилита для массового переименования расширений файлов (.png, .jpg, .txt) прямо из контекстного меню Проводника Windows.\n\nПрограмма распространяется под свободной лицензией GNU General Public License v3.0 (GPL-3.0).",
                    ForeColor = Color.FromArgb(180, 180, 190),
                    Location = new Point(24, 88),
                    Size = new Size(490, 100)
                };

                _acceptLicenseCheck.Location = new Point(24, 210);

                _pagePanel.Controls.Add(title);
                _pagePanel.Controls.Add(desc);
                _pagePanel.Controls.Add(_acceptLicenseCheck);

                if (!Program.IsDotNet8DesktopInstalled())
                {
                    var warn = new Label
                    {
                        Text = "Примечание: для работы приложения требуется .NET 8 Desktop Runtime (x64).",
                        ForeColor = Color.FromArgb(251, 191, 36),
                        Location = new Point(24, 255),
                        Size = new Size(490, 30)
                    };
                    _pagePanel.Controls.Add(warn);
                }

                _backBtn.Enabled = false;
                _nextBtn.Text = "Далее >";
                _nextBtn.Enabled = _acceptLicenseCheck.Checked;
            }
            else if (step == 1)
            {
                var title = new Label
                {
                    Text = "Параметры установки",
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    Location = new Point(24, 20),
                    AutoSize = true
                };

                var pathLbl = new Label
                {
                    Text = "Папка установки:",
                    ForeColor = Color.FromArgb(180, 180, 190),
                    Location = new Point(24, 62),
                    AutoSize = true
                };

                _pathBox.Location = new Point(24, 86);
                var browseBtn = CreateButton("Обзор...", new Point(404, 84), false);
                browseBtn.Click += delegate
                {
                    using (var dlg = new FolderBrowserDialog { SelectedPath = _pathBox.Text })
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                            _pathBox.Text = Path.Combine(dlg.SelectedPath, "BulkExtensionChanger");
                    }
                };

                _ctxMenuCheck.Location = new Point(24, 138);
                _startMenuCheck.Location = new Point(24, 170);
                _desktopCheck.Location = new Point(24, 202);

                _pagePanel.Controls.AddRange(new Control[] { title, pathLbl, _pathBox, browseBtn, _ctxMenuCheck, _startMenuCheck, _desktopCheck });

                _backBtn.Enabled = true;
                _nextBtn.Text = "Установить";
                _nextBtn.Enabled = true;
            }
            else if (step == 2)
            {
                var title = new Label
                {
                    Text = "Установка завершена!",
                    Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(74, 222, 128),
                    Location = new Point(24, 24),
                    AutoSize = true
                };

                var info = new Label
                {
                    Text = "Приложение установлено в:\n" + _installDir + "\n\nТеперь вы можете выделить файлы в Проводнике, нажать правой кнопкой мыши и выбрать «Массовая смена расширений...».",
                    ForeColor = Color.FromArgb(200, 200, 210),
                    Location = new Point(24, 72),
                    Size = new Size(490, 110)
                };

                _launchAfterCheck.Location = new Point(24, 200);
                _pagePanel.Controls.AddRange(new Control[] { title, info, _launchAfterCheck });

                _backBtn.Enabled = false;
                _cancelBtn.Enabled = false;
                _nextBtn.Text = "Готово";
                _nextBtn.Enabled = true;
            }
        }

        private void OnNext()
        {
            if (_step == 0)
            {
                ShowStep(1);
                return;
            }

            if (_step == 1)
            {
                _installDir = _pathBox.Text.Trim();
                if (string.IsNullOrEmpty(_installDir))
                    return;

                _nextBtn.Enabled = false;
                _backBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                try
                {
                    Program.InstallFiles(_installDir, _ctxMenuCheck.Checked, _startMenuCheck.Checked, _desktopCheck.Checked);
                    Cursor = Cursors.Default;
                    ShowStep(2);
                }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    _nextBtn.Enabled = true;
                    _backBtn.Enabled = true;
                    MessageBox.Show(this, "Ошибка установки:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            if (_step == 2)
            {
                if (_launchAfterCheck.Checked)
                {
                    string exe = Path.Combine(_installDir, "BulkExtensionChanger.exe");
                    if (File.Exists(exe))
                    {
                        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                    }
                }
                Close();
            }
        }
    }
}
