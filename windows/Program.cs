using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace AntigravityCnSwitch
{
    public class ToggleSwitch : Control
    {
        private bool _checked;
        private bool _busy;

        public event EventHandler CheckedChangedByUser;

        public bool Checked
        {
            get { return _checked; }
            set { _checked = value; Invalidate(); }
        }

        public bool Busy
        {
            get { return _busy; }
            set { _busy = value; Cursor = value ? Cursors.WaitCursor : Cursors.Hand; Invalidate(); }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Size = new Size(54, 30);
            Cursor = Cursors.Hand;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_busy) return;
            if (CheckedChangedByUser != null)
                CheckedChangedByUser(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Color.White);

            int w = Width - 2;
            int h = Height - 2;
            int r = h;
            Rectangle trackRect = new Rectangle(1, 1, w, h);

            Color trackColor = _busy
                ? Color.FromArgb(180, 180, 180)
                : (_checked ? Color.FromArgb(122, 31, 26) : Color.FromArgb(205, 202, 196));

            using (GraphicsPath path = RoundedRect(trackRect, r))
            using (SolidBrush br = new SolidBrush(trackColor))
            {
                e.Graphics.FillPath(br, path);
            }

            int knobSize = h - 6;
            int knobX = _checked ? (1 + w - knobSize - 3) : 4;
            int knobY = 4;
            using (SolidBrush knobBr = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(knobBr, knobX, knobY, knobSize, knobSize);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int diameter)
        {
            GraphicsPath path = new GraphicsPath();
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class MainForm : Form
    {
        private readonly Color _warmPaper = Color.FromArgb(247, 245, 240);
        private readonly Color _cardBg = Color.FromArgb(252, 252, 251);
        private readonly Color _waxRed = Color.FromArgb(122, 31, 26);
        private readonly Color _warmOrange = Color.FromArgb(217, 119, 87);
        private readonly Color _inkColor = Color.FromArgb(20, 20, 20);

        private readonly string _homeDir;
        private readonly string _targetDir;
        private readonly string _userConfigPath;
        private readonly string _runtimeNodePath;
        private readonly string _appBaseDir;
        private readonly string _bundledPatchDir;
        private readonly string _bundledNodePath;

        private ToggleSwitch _toggle;
        private Label _lblState;
        private Label _lblStatusDesc;
        private Button _btnRestartApp;
        private Label _lblAppStatus;
        private LinkLabel _lnkFonts;
        private Label _lblError;
        private ComboBox _cbDay;
        private ComboBox _cbNight;
        private System.Windows.Forms.Timer _pollTimer;
        private bool _suppressComboEvents;

        private readonly string[] _dayKeys = { "high-contrast", "claude-desktop", "parchment" };
        private readonly string[] _dayLabels = { "高对比暖白", "Claude 净白", "复古羊皮纸" };
        private readonly string[] _nightKeys = { "onyx", "claude-warm" };
        private readonly string[] _nightLabels = { "官方碳黑", "Claude 暖纸深夜" };

        public MainForm()
        {
            _homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _targetDir = Path.Combine(_homeDir, ".gemini", "antigravity", "theme-patch");
            _userConfigPath = Path.Combine(_targetDir, "user", "config.json");
            _runtimeNodePath = Path.Combine(_targetDir, "runtime", "node.exe");
            _appBaseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPatch = Path.Combine(_appBaseDir, "patch");
            string localNode = Path.Combine(_appBaseDir, "runtime", "node.exe");
            _bundledPatchDir = Directory.Exists(localPatch) ? localPatch : Path.Combine(_targetDir, "bundled-patch");
            _bundledNodePath = File.Exists(localNode) ? localNode : _runtimeNodePath;

            InitUI();
            RefreshState();

            _pollTimer = new System.Windows.Forms.Timer();
            _pollTimer.Interval = 2500;
            _pollTimer.Tick += (s, e) => UpdateLiveStatusLabels();
            _pollTimer.Start();
        }

        private void InitUI()
        {
            Text = "反重力汉化开关";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(380, 446);
            BackColor = _warmPaper;
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            var lblTitle = new Label
            {
                Text = "反重力汉化开关",
                Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold),
                ForeColor = _inkColor,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 18),
                Size = new Size(340, 32)
            };
            Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "船板全栈补丁 v5.4.2 · 汉化 / 暖纸护眼 / 实时指标",
                Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(115, 112, 106),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 50),
                Size = new Size(340, 20)
            };
            Controls.Add(lblSub);

            // Main Toggle Card
            var cardMain = new Panel
            {
                BackColor = _cardBg,
                Location = new Point(20, 84),
                Size = new Size(340, 162),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblSwitchTitle = new Label
            {
                Text = "开启汉化",
                Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
                ForeColor = _inkColor,
                Location = new Point(16, 16),
                AutoSize = true
            };
            cardMain.Controls.Add(lblSwitchTitle);

            _lblState = new Label
            {
                Text = "检测中...",
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                ForeColor = Color.Gray,
                Location = new Point(16, 44),
                Size = new Size(230, 20)
            };
            cardMain.Controls.Add(_lblState);

            _toggle = new ToggleSwitch
            {
                Location = new Point(268, 22)
            };
            _toggle.CheckedChangedByUser += OnToggleClicked;
            cardMain.Controls.Add(_toggle);

            var divider = new Label
            {
                BackColor = Color.FromArgb(232, 228, 220),
                Location = new Point(16, 74),
                Size = new Size(306, 1)
            };
            cardMain.Controls.Add(divider);

            _lblStatusDesc = new Label
            {
                Text = "正在检测状态...",
                Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 98, 94),
                Location = new Point(16, 82),
                Size = new Size(308, 40)
            };
            cardMain.Controls.Add(_lblStatusDesc);

            _btnRestartApp = new Button
            {
                Text = "重启 Antigravity 并立即汉化",
                Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold),
                BackColor = _waxRed,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(16, 124),
                Size = new Size(306, 28),
                Visible = false,
                Cursor = Cursors.Hand
            };
            _btnRestartApp.FlatAppearance.BorderSize = 0;
            _btnRestartApp.Click += OnRestartAntigravityClicked;
            cardMain.Controls.Add(_btnRestartApp);

            Controls.Add(cardMain);

            // Theme Card
            var cardTheme = new Panel
            {
                BackColor = _cardBg,
                Location = new Point(20, 260),
                Size = new Size(340, 96),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblDay = new Label
            {
                Text = "白天主题",
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular),
                ForeColor = _inkColor,
                Location = new Point(16, 18),
                AutoSize = true
            };
            cardTheme.Controls.Add(lblDay);

            _cbDay = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(158, 14),
                Size = new Size(164, 26)
            };
            _cbDay.Items.AddRange(_dayLabels);
            _cbDay.SelectedIndexChanged += (s, e) => OnPresetChanged();
            cardTheme.Controls.Add(_cbDay);

            var lblNight = new Label
            {
                Text = "夜间主题",
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular),
                ForeColor = _inkColor,
                Location = new Point(16, 56),
                AutoSize = true
            };
            cardTheme.Controls.Add(lblNight);

            _cbNight = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(158, 52),
                Size = new Size(164, 26)
            };
            _cbNight.Items.AddRange(_nightLabels);
            _cbNight.SelectedIndexChanged += (s, e) => OnPresetChanged();
            cardTheme.Controls.Add(_cbNight);

            Controls.Add(cardTheme);

            _lblError = new Label
            {
                Text = "",
                Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.Firebrick,
                Location = new Point(20, 362),
                Size = new Size(340, 38)
            };
            Controls.Add(_lblError);

            // Footer
            _lblAppStatus = new Label
            {
                Text = "Antigravity 检测中...",
                Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(115, 112, 106),
                Location = new Point(20, 410),
                Size = new Size(210, 20)
            };
            Controls.Add(_lblAppStatus);

            _lnkFonts = new LinkLabel
            {
                Text = "下载推荐字体",
                Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular),
                LinkColor = _warmOrange,
                Location = new Point(240, 410),
                Size = new Size(120, 20),
                TextAlign = ContentAlignment.MiddleRight
            };
            _lnkFonts.LinkClicked += (s, e) =>
            {
                Process.Start(new ProcessStartInfo("https://drive.google.com/drive/folders/1NX92SgVUqtSjWg6bt6sIJXy-cn7J_boC") { UseShellExecute = true });
            };
            Controls.Add(_lnkFonts);
        }

        private void RefreshState()
        {
            var cfg = ReadUserConfig();
            bool cfgEnabled = cfg.ContainsKey("enabled") && cfg["enabled"] is bool && (bool)cfg["enabled"];

            _suppressComboEvents = true;
            string dayVal = cfg.ContainsKey("preset") ? Convert.ToString(cfg["preset"]) : "high-contrast";
            int dayIdx = Array.IndexOf(_dayKeys, dayVal);
            _cbDay.SelectedIndex = dayIdx >= 0 ? dayIdx : 0;

            string nightVal = cfg.ContainsKey("darkPreset") ? Convert.ToString(cfg["darkPreset"]) : "onyx";
            int nightIdx = Array.IndexOf(_nightKeys, nightVal);
            _cbNight.SelectedIndex = nightIdx >= 0 ? nightIdx : 0;
            _suppressComboEvents = false;

            bool serviceActive = IsServiceActive();
            bool active = cfgEnabled && serviceActive;
            _toggle.Checked = active;
            _lblState.Text = active ? "已开启 · 中文界面 + 暖纸主题" : "已关闭 · 原版英文界面";
            _lblState.ForeColor = active ? _waxRed : Color.Gray;

            UpdateLiveStatusLabels();
        }

        private void UpdateLiveStatusLabels()
        {
            bool running = GetAntigravityProcesses().Length > 0;
            bool daemonAlive = IsDaemonActuallyRunning();
            bool portReachable = running && IsAntigravityDebugPortReachable();

            if (_toggle.Checked && !daemonAlive && !_toggle.Busy)
            {
                StartDaemonDirectlyIfPresent();
                daemonAlive = IsDaemonActuallyRunning();
            }

            _lblAppStatus.Text = running
                ? (portReachable ? "● Antigravity 运行中 (已连接)" : "● Antigravity 运行中 (未连端口)")
                : "○ Antigravity 未运行";
            _lblAppStatus.ForeColor = running ? Color.ForestGreen : Color.FromArgb(115, 112, 106);

            if (!_toggle.Busy)
            {
                if (_toggle.Checked)
                {
                    if (running && !portReachable)
                    {
                        _lblStatusDesc.Text = "汉化已就绪，但当前 Antigravity 未开启调试端口（开启前已打开）。请点击下方按钮重启即可生效：";
                        _btnRestartApp.Visible = true;
                    }
                    else if (running && portReachable)
                    {
                        _lblStatusDesc.Text = daemonAlive
                            ? "已开启 · 守护服务运行中，Antigravity 已自动注入汉化与暖纸主题。"
                            : "已开启 · 正在唤醒后台守护服务...";
                        _btnRestartApp.Visible = false;
                    }
                    else
                    {
                        _lblStatusDesc.Text = "已开启 · 现在打开 Antigravity 即可自动显示中文界面与暖纸护眼主题。";
                        _btnRestartApp.Visible = false;
                    }
                }
                else
                {
                    _lblStatusDesc.Text = "已关闭 · Antigravity 保持原版英文界面。";
                    _btnRestartApp.Visible = false;
                }
            }

            string localFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
            string sysFonts = @"C:\Windows\Fonts";
            bool hasFonts = HasFontInDir(localFonts) || HasFontInDir(sysFonts);
            if (hasFonts)
            {
                _lnkFonts.Text = "字体已就绪";
                _lnkFonts.LinkBehavior = LinkBehavior.NeverUnderline;
                _lnkFonts.LinkColor = Color.FromArgb(115, 112, 106);
                _lnkFonts.Enabled = false;
            }
            else
            {
                _lnkFonts.Text = "下载推荐字体";
                _lnkFonts.LinkColor = _warmOrange;
                _lnkFonts.Enabled = true;
            }
        }

        private static Process[] GetAntigravityProcesses()
        {
            return Process.GetProcesses().Where(p =>
            {
                try
                {
                    return p.ProcessName.IndexOf("Antigravity", StringComparison.OrdinalIgnoreCase) >= 0
                        && p.ProcessName.IndexOf("CnSwitch", StringComparison.OrdinalIgnoreCase) < 0;
                }
                catch { return false; }
            }).ToArray();
        }

        private async void OnRestartAntigravityClicked(object sender, EventArgs e)
        {
            _btnRestartApp.Enabled = false;
            _btnRestartApp.Text = "正在重启 Antigravity...";
            _lblError.Text = "";

            try
            {
                await Task.Run(() =>
                {
                    var procs = GetAntigravityProcesses();
                    string exePath = null;
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (exePath == null && p.MainModule != null && File.Exists(p.MainModule.FileName))
                                exePath = p.MainModule.FileName;
                        }
                        catch { }
                    }

                    foreach (var p in procs)
                    {
                        try { p.Kill(); } catch { }
                    }

                    Thread.Sleep(1500);
                    StartDaemonDirectlyIfPresent();

                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = "--remote-debugging-port=9222",
                            UseShellExecute = true
                        });
                    }
                    Thread.Sleep(1500);
                });
            }
            catch (Exception ex)
            {
                _lblError.Text = "重启失败: " + ex.Message;
            }
            finally
            {
                _btnRestartApp.Enabled = true;
                _btnRestartApp.Text = "重启 Antigravity 并立即汉化";
                RefreshState();
            }
        }

        private static bool IsAntigravityDebugPortReachable()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var ports = new List<int>();
                string[] candidates = {
                    Path.Combine(appData, "Antigravity", "DevToolsActivePort"),
                    Path.Combine(appData, "Antigravity - Insiders", "DevToolsActivePort")
                };
                foreach (var p in candidates)
                {
                    if (!File.Exists(p)) continue;
                    string[] lines = File.ReadAllLines(p);
                    int port;
                    if (lines.Length > 0 && int.TryParse(lines[0].Trim(), out port) && port > 0)
                        ports.Add(port);
                }
                if (!ports.Contains(9222)) ports.Add(9222);
                if (!ports.Contains(9223)) ports.Add(9223);

                foreach (int port in ports)
                {
                    try
                    {
                        using (var client = new TcpClient())
                        {
                            var ar = client.BeginConnect("127.0.0.1", port, null, null);
                            if (ar.AsyncWaitHandle.WaitOne(200))
                            {
                                client.EndConnect(ar);
                                return true;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        private static bool HasFontInDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return false;
                return Directory.EnumerateFiles(dir).Any(f =>
                {
                    string name = Path.GetFileName(f);
                    return name.IndexOf("Sarasa", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Anthropic", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.Contains("更纱");
                });
            }
            catch { return false; }
        }

        private async void OnToggleClicked(object sender, EventArgs e)
        {
            bool targetState = !_toggle.Checked;
            _toggle.Busy = true;
            _btnRestartApp.Visible = false;
            _lblError.Text = "";
            _lblStatusDesc.Text = targetState ? "正在开启汉化并启动守护服务..." : "正在关闭汉化并恢复英文原貌...";

            try
            {
                await Task.Run(() =>
                {
                    string nodeExe = EnsureRuntimeNode();
                    string installScript = ResolveInstallScript();
                    string dayKey = _dayKeys[Math.Max(0, _cbDay.SelectedIndex)];
                    string nightKey = _nightKeys[Math.Max(0, _cbNight.SelectedIndex)];

                    if (targetState)
                    {
                        EnsureArgvDirsExist();
                        UpdateUserConfig(cfg =>
                        {
                            cfg["enabled"] = true;
                            cfg["preset"] = dayKey;
                            cfg["darkPreset"] = nightKey;
                        });
                        var res = RunProcess(nodeExe, "--disable-warning=ExperimentalWarning \"" + installScript + "\" install");
                        if (res.Item1 != 0)
                        {
                            WriteLog("install failed: " + res.Item2);
                            throw new Exception(string.IsNullOrEmpty(res.Item2) ? "安装失败 (exit " + res.Item1 + ")" : res.Item2);
                        }
                        if (!IsDaemonActuallyRunning())
                        {
                            StartDaemonDirectlyIfPresent();
                        }
                    }
                    else
                    {
                        if (File.Exists(installScript))
                        {
                            var res = RunProcess(nodeExe, "--disable-warning=ExperimentalWarning \"" + installScript + "\" uninstall");
                            if (res.Item1 != 0)
                            {
                                WriteLog("uninstall failed: " + res.Item2);
                                throw new Exception(string.IsNullOrEmpty(res.Item2) ? "卸载失败 (exit " + res.Item1 + ")" : res.Item2);
                            }
                        }
                        UpdateUserConfig(cfg => { cfg["enabled"] = false; });
                    }
                });
            }
            catch (Exception ex)
            {
                _lblError.Text = ex.Message;
            }
            finally
            {
                _toggle.Busy = false;
                RefreshState();
            }
        }

        private void EnsureArgvDirsExist()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(_homeDir, ".antigravity"));
            }
            catch { }
        }

        private void StartDaemonDirectlyIfPresent()
        {
            try
            {
                string daemonJs = Path.Combine(_targetDir, "core", "daemon.mjs");
                if (!File.Exists(_runtimeNodePath) || !File.Exists(daemonJs)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = _runtimeNodePath,
                    Arguments = "--disable-warning=ExperimentalWarning \"" + daemonJs + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch { }
        }

        private void OnPresetChanged()
        {
            if (_suppressComboEvents) return;
            string dayKey = _dayKeys[Math.Max(0, _cbDay.SelectedIndex)];
            string nightKey = _nightKeys[Math.Max(0, _cbNight.SelectedIndex)];
            UpdateUserConfig(cfg =>
            {
                cfg["preset"] = dayKey;
                cfg["darkPreset"] = nightKey;
            });
        }

        private void EnsureEmbeddedPayloadExtracted()
        {
            string bundledInstall = Path.Combine(_targetDir, "bundled-patch", "install.mjs");
            if (File.Exists(_runtimeNodePath) && File.Exists(bundledInstall))
                return;

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AntigravityCnSwitch.payload.zip"))
            {
                if (stream == null) return;
                Directory.CreateDirectory(_targetDir);
                string tempZip = Path.Combine(Path.GetTempPath(), "ag_cn_payload_" + Guid.NewGuid().ToString("N") + ".zip");
                try
                {
                    using (var fs = File.Create(tempZip))
                    {
                        stream.CopyTo(fs);
                    }
                    using (var archive = ZipFile.OpenRead(tempZip))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            string destPath = Path.GetFullPath(Path.Combine(_targetDir, entry.FullName));
                            if (!destPath.StartsWith(Path.GetFullPath(_targetDir), StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(destPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                entry.ExtractToFile(destPath, true);
                            }
                        }
                    }
                }
                finally
                {
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                }
            }
        }

        private string ResolveInstallScript()
        {
            EnsureEmbeddedPayloadExtracted();
            string bundled = Path.Combine(_bundledPatchDir, "install.mjs");
            if (File.Exists(bundled)) return bundled;
            return Path.Combine(_targetDir, "install.mjs");
        }

        private string EnsureRuntimeNode()
        {
            EnsureEmbeddedPayloadExtracted();
            string runtimeDir = Path.Combine(_targetDir, "runtime");
            Directory.CreateDirectory(runtimeDir);

            if (File.Exists(_bundledNodePath))
            {
                if (!File.Exists(_runtimeNodePath))
                    File.Copy(_bundledNodePath, _runtimeNodePath, true);
                return _runtimeNodePath;
            }
            if (File.Exists(_runtimeNodePath)) return _runtimeNodePath;

            string pfNode = @"C:\Program Files\nodejs\node.exe";
            if (File.Exists(pfNode)) return pfNode;

            throw new Exception("未找到 Node.js 22+ 运行时 (runtime\\node.exe)");
        }

        private static bool IsDaemonActuallyRunning()
        {
            try
            {
                foreach (var f in Directory.GetFiles(Path.GetTempPath(), "ag-theme-daemon-*.pid"))
                {
                    if (!File.Exists(f)) continue;
                    string[] lines = File.ReadAllLines(f, Encoding.UTF8);
                    if (lines.Length == 0) continue;
                    if (lines.Length > 1 && lines[1].Trim().Equals("STOP", StringComparison.OrdinalIgnoreCase)) continue;
                    int pid;
                    if (int.TryParse(lines[0].Trim(), out pid) && pid > 0)
                    {
                        try
                        {
                            var proc = Process.GetProcessById(pid);
                            if (!proc.HasExited) return true;
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return false;
        }

        private bool IsServiceActive()
        {
            if (IsDaemonActuallyRunning()) return true;

            var res = RunProcess("schtasks.exe", "/Query /TN AntigravityThemeDaemon");
            if (res.Item1 == 0) return true;

            var regRes = RunProcess("reg.exe", @"query HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v AntigravityThemeDaemon");
            if (regRes.Item1 == 0) return true;

            string startupVbs = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "AntigravityThemeDaemon.vbs");
            return File.Exists(startupVbs);
        }

        private Dictionary<string, object> ReadUserConfig()
        {
            try
            {
                if (!File.Exists(_userConfigPath)) return new Dictionary<string, object>();
                string raw = File.ReadAllText(_userConfigPath, Encoding.UTF8).TrimStart('\uFEFF');
                var ser = new JavaScriptSerializer();
                return ser.Deserialize<Dictionary<string, object>>(raw) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private void UpdateUserConfig(Action<Dictionary<string, object>> mutate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_userConfigPath));
                var cfg = ReadUserConfig();
                if (cfg.Count == 0)
                {
                    string tpl = Path.Combine(_bundledPatchDir, "user", "config.json");
                    if (File.Exists(tpl))
                    {
                        string raw = File.ReadAllText(tpl, Encoding.UTF8).TrimStart('\uFEFF');
                        var serTpl = new JavaScriptSerializer();
                        cfg = serTpl.Deserialize<Dictionary<string, object>>(raw) ?? new Dictionary<string, object>();
                    }
                }
                mutate(cfg);
                var ser = new JavaScriptSerializer();
                string json = ser.Serialize(cfg);
                File.WriteAllText(_userConfigPath, json + "\n", new UTF8Encoding(false));
            }
            catch { }
        }

        private static void WriteLog(string msg)
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "antigravity-cn-switch.log");
                File.AppendAllText(logPath, "[" + DateTime.Now.ToString("s") + "] " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        private static Tuple<int, string> RunProcess(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using (var proc = Process.Start(psi))
                {
                    string outStr = proc.StandardOutput.ReadToEnd();
                    string errStr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    string combined = (outStr + "\n" + errStr).Trim();
                    return Tuple.Create(proc.ExitCode, combined);
                }
            }
            catch (Exception ex)
            {
                return Tuple.Create(-1, ex.Message);
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
