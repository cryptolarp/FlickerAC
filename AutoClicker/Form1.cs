#nullable disable

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoClicker
{
    public partial class Form1 : Form
    {
        // ──────────────────────────────────────────────
        //  Windows API imports
        // ──────────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        // Constants
        const int INPUT_MOUSE = 0;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        const uint MOD_CONTROL = 0x0002;
        const uint MOD_ALT = 0x0001;
        const uint MOD_SHIFT = 0x0004;
        const int WM_HOTKEY = 0x0312;
        const int HOTKEY_ID = 9001;
        const int WM_NCLBUTTONDOWN = 0xA1;

        const int WH_MOUSE_LL = 14;
        const int WM_LBUTTONDOWN = 0x0201;

        // Structs
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int x, y;
        }

        delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        // ──────────────────────────────────────────────
        //  Application state
        // ──────────────────────────────────────────────
        private bool _isRunning = false;
        private bool _isPausedByBorder = false;
        private System.Windows.Forms.Timer _clickTimer;
        private readonly Random _rng = new Random();

        private uint _hotkeyModifiers = MOD_CONTROL;
        private Keys _hotkeyKey = Keys.F6;
        private bool _settingHotkey = false;

        private bool _useTimeLimit = false;
        private int _timeLimitSeconds = 60;
        private DateTime _startTime;

        private bool _useBorderControl = false;
        private int _borderMargin = 20;

        private static IntPtr _mouseHookId = IntPtr.Zero;
        private static LowLevelMouseProc _hookProc;
        private static Form1 _instance;
        private bool _isSelectingPosition = false;

        // Animation timers
        private System.Windows.Forms.Timer _fadeTimer;
        private System.Windows.Forms.Timer _buttonColorTimer;
        private System.Windows.Forms.Timer _pulseTimer;
        private Color _targetToggleColor;
        private Color _currentToggleColor;
        private System.Windows.Forms.Timer _countdownTimer;
        private float _pulsePhase = 0f;

        // ──────────────────────────────────────────────
        //  UI Controls
        // ──────────────────────────────────────────────
        private RadioButton rbLeft, rbRight;
        private NumericUpDown numDelay, numOffset;
        private Label lblHotkey, lblStatus, lblCountdown;
        private Button btnToggle, btnSetHotkey;
        private CheckBox chkBorderControl, chkTimeLimit;
        private NumericUpDown numBorderMargin, numTimeLimit;
        private GroupBox grpPosition;
        private RadioButton rbCursorPos, rbFixedPos;
        private NumericUpDown numPosX, numPosY;
        private Button btnSelectLocation;

        // Vibrant theme colours
        private static readonly Color AccentBlue = Color.FromArgb(0, 180, 255);
        private static readonly Color AccentBlueHover = Color.FromArgb(80, 200, 255);
        private static readonly Color DarkBackground = Color.FromArgb(12, 12, 12);
        private static readonly Color DarkInput = Color.FromArgb(35, 35, 40);
        private static readonly Color StartGreen = Color.FromArgb(0, 200, 80);
        private static readonly Color StopRed = Color.FromArgb(235, 65, 65);
        private static readonly Color PausedGold = Color.FromArgb(255, 200, 0);
        private static readonly Color BorderGreen = Color.LimeGreen;

        public Form1()
        {
            _instance = this;
            InitializeComponent();
            RegisterHotKey(Handle, HOTKEY_ID, _hotkeyModifiers, (uint)_hotkeyKey);
            ApplyRoundedCorners();
            this.Opacity = 0;
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 10 };
            _fadeTimer.Tick += FadeIn;
            _fadeTimer.Start();
        }

        private void FadeIn(object sender, EventArgs e)
        {
            if (this.Opacity < 1.0)
                this.Opacity += 0.06;
            else
            {
                this.Opacity = 1.0;
                _fadeTimer.Stop();
                _fadeTimer.Dispose();
                _fadeTimer = null;
            }
        }

        private void ApplyRoundedCorners()
        {
            int radius = 16;
            IntPtr region = CreateRoundRectRgn(0, 0, this.Width, this.Height, radius, radius);
            SetWindowRgn(this.Handle, region, true);
        }

        private void CenterControlHorizontally(Control ctrl)
        {
            ctrl.Left = (this.ClientSize.Width - ctrl.Width) / 2;
        }

        // ──────────────────────────────────────────────
        //  UI construction (centered layout)
        // ──────────────────────────────────────────────
        private void InitializeComponent()
        {
            this.Text = "FlickerClick";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ClientSize = new Size(420, 640);
            this.BackColor = DarkBackground;
            this.ForeColor = Color.White;
            this.KeyPreview = true;

            int groupWidth = 380;
            int leftMargin = (this.ClientSize.Width - groupWidth) / 2; // 20

            int y = 20;

            // ── Mouse Button group ───────────────────
            GroupBox grpButton = new GroupBox()
            {
                Text = "Mouse Button",
                Size = new Size(groupWidth, 75),
                ForeColor = AccentBlue,
                Location = new Point(leftMargin, y)
            };
            rbLeft = new RadioButton() { Text = "Left click", Location = new Point(25, 28), Checked = true, BackColor = DarkBackground, ForeColor = Color.White };
            rbRight = new RadioButton() { Text = "Right click", Location = new Point(130, 28), BackColor = DarkBackground, ForeColor = Color.White };
            grpButton.Controls.Add(rbLeft);
            grpButton.Controls.Add(rbRight);
            y += 90;

            // ── Timing group ──────────────────────────
            GroupBox grpTiming = new GroupBox()
            {
                Text = "Timing (ms)",
                Size = new Size(groupWidth, 75),
                ForeColor = AccentBlue,
                Location = new Point(leftMargin, y)
            };
            Label lblDelay = new Label() { Text = "Delay:", Location = new Point(25, 28), AutoSize = true, ForeColor = Color.White };
            numDelay = new NumericUpDown() { Location = new Point(75, 25), Size = new Size(75, 22), Minimum = 1, Maximum = 10000, Value = 100, BackColor = DarkInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Label lblOffset = new Label() { Text = "Offset:", Location = new Point(180, 28), AutoSize = true, ForeColor = Color.White };
            numOffset = new NumericUpDown() { Location = new Point(230, 25), Size = new Size(75, 22), Minimum = 0, Maximum = 1000, Value = 20, BackColor = DarkInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            grpTiming.Controls.Add(lblDelay);
            grpTiming.Controls.Add(numDelay);
            grpTiming.Controls.Add(lblOffset);
            grpTiming.Controls.Add(numOffset);
            y += 90;

            // ── Border control ────────────────────────
            GroupBox grpBorder = new GroupBox()
            {
                Text = "Border Control",
                Size = new Size(groupWidth, 60),
                ForeColor = AccentBlue,
                Location = new Point(leftMargin, y)
            };
            chkBorderControl = new CheckBox() { Text = "Pause when cursor near edge (px):", Location = new Point(25, 22), AutoSize = true, BackColor = DarkBackground, ForeColor = Color.White };
            numBorderMargin = new NumericUpDown() { Location = new Point(280, 20), Size = new Size(70, 22), Minimum = 1, Maximum = 200, Value = 20, BackColor = DarkInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Enabled = false };
            chkBorderControl.CheckedChanged += (s, e) =>
            {
                numBorderMargin.Enabled = chkBorderControl.Checked;
                _useBorderControl = chkBorderControl.Checked;
                _borderMargin = (int)numBorderMargin.Value;
            };
            numBorderMargin.ValueChanged += (s, e) => _borderMargin = (int)numBorderMargin.Value;
            grpBorder.Controls.Add(chkBorderControl);
            grpBorder.Controls.Add(numBorderMargin);
            y += 70;

            // ── Time limit ────────────────────────────
            GroupBox grpTimeLimit = new GroupBox()
            {
                Text = "Run Time Limit",
                Size = new Size(groupWidth, 60),
                ForeColor = AccentBlue,
                Location = new Point(leftMargin, y)
            };
            chkTimeLimit = new CheckBox() { Text = "Stop after (seconds):", Location = new Point(25, 22), AutoSize = true, BackColor = DarkBackground, ForeColor = Color.White };
            numTimeLimit = new NumericUpDown() { Location = new Point(280, 20), Size = new Size(70, 22), Minimum = 1, Maximum = 86400, Value = 60, BackColor = DarkInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Enabled = false };
            chkTimeLimit.CheckedChanged += (s, e) =>
            {
                numTimeLimit.Enabled = chkTimeLimit.Checked;
                _useTimeLimit = chkTimeLimit.Checked;
                _timeLimitSeconds = (int)numTimeLimit.Value;
            };
            numTimeLimit.ValueChanged += (s, e) => _timeLimitSeconds = (int)numTimeLimit.Value;
            grpTimeLimit.Controls.Add(chkTimeLimit);
            grpTimeLimit.Controls.Add(numTimeLimit);
            y += 70;

            // ── Position ──────────────────────────────
            grpPosition = new GroupBox()
            {
                Text = "Click Position",
                Size = new Size(groupWidth, 115),
                ForeColor = AccentBlue,
                Location = new Point(leftMargin, y)
            };
            rbCursorPos = new RadioButton() { Text = "Current cursor position", Location = new Point(25, 24), Checked = true, BackColor = DarkBackground, ForeColor = Color.White };
            rbFixedPos = new RadioButton() { Text = "Fixed position:", Location = new Point(25, 48), BackColor = DarkBackground, ForeColor = Color.White };
            Label lblX = new Label() { Text = "X:", Location = new Point(140, 48), AutoSize = true, ForeColor = Color.White };
            numPosX = new NumericUpDown() { Location = new Point(160, 46), Size = new Size(60, 22), Minimum = 0, Maximum = 9999, Value = 100, BackColor = DarkInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Enabled = false };
            Label lblY = new Label() { Text = "Y:", Location = new Point(230, 48), AutoSize = true, ForeColor = Color.White };
            numPosY = new NumericUpDown() { Location = new Point(250, 46), Size = new Size(60, 22), Minimum = 0, Maximum = 9999, Value = 100, BackColor = DarkInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Enabled = false };
            btnSelectLocation = new Button()
            {
                Text = "Select Location",
                Location = new Point(25, 80),
                Size = new Size(130, 24),
                BackColor = AccentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnSelectLocation.FlatAppearance.BorderSize = 0;
            btnSelectLocation.Click += BtnSelectLocation_Click;
            btnSelectLocation.MouseEnter += (s, e) => { if (!_isSelectingPosition) btnSelectLocation.BackColor = AccentBlueHover; };
            btnSelectLocation.MouseLeave += (s, e) => { if (!_isSelectingPosition) btnSelectLocation.BackColor = AccentBlue; };
            rbCursorPos.CheckedChanged += (s, e) => UpdatePositionControls();
            rbFixedPos.CheckedChanged += (s, e) => UpdatePositionControls();
            grpPosition.Controls.Add(rbCursorPos);
            grpPosition.Controls.Add(rbFixedPos);
            grpPosition.Controls.Add(lblX);
            grpPosition.Controls.Add(numPosX);
            grpPosition.Controls.Add(lblY);
            grpPosition.Controls.Add(numPosY);
            grpPosition.Controls.Add(btnSelectLocation);
            y += 130;

            // ── Hotkey ────────────────────────────────
            GroupBox grpHotkey = new GroupBox()
            {
                Text = "Toggle Hotkey",
                Size = new Size(groupWidth, 60),
                ForeColor = AccentBlue,
                Location = new Point(leftMargin, y)
            };
            lblHotkey = new Label() { Text = "Ctrl + F6", Location = new Point(25, 24), AutoSize = true, ForeColor = Color.White };
            btnSetHotkey = new Button() { Text = "Set Hotkey", Location = new Point(270, 20), Size = new Size(90, 24), BackColor = AccentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSetHotkey.FlatAppearance.BorderSize = 0;
            btnSetHotkey.Click += BtnSetHotkey_Click;
            btnSetHotkey.MouseEnter += (s, e) => btnSetHotkey.BackColor = AccentBlueHover;
            btnSetHotkey.MouseLeave += (s, e) => btnSetHotkey.BackColor = AccentBlue;
            grpHotkey.Controls.Add(lblHotkey);
            grpHotkey.Controls.Add(btnSetHotkey);
            y += 75;

            // ── Start/Stop button (centered, with visible green border) ──
            btnToggle = new Button()
            {
                Text = "Start",
                Size = new Size(160, 42),
                BackColor = StartGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btnToggle.FlatAppearance.BorderColor = BorderGreen;   // Always visible green border
            btnToggle.FlatAppearance.BorderSize = 2;              // 2px solid border
            btnToggle.Click += BtnToggle_Click;
            btnToggle.MouseEnter += BtnToggle_MouseEnter;
            btnToggle.MouseLeave += BtnToggle_MouseLeave;
            CenterControlHorizontally(btnToggle);
            btnToggle.Top = y;
            y += 52;

            // Status label centered
            lblStatus = new Label()
            {
                Text = "Status: Stopped",
                AutoSize = true,
                ForeColor = Color.LightCoral,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            CenterControlHorizontally(lblStatus);
            lblStatus.Top = y;
            y += 28;

            // Countdown centered
            lblCountdown = new Label()
            {
                Text = "",
                AutoSize = true,
                ForeColor = AccentBlue,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Visible = false
            };
            CenterControlHorizontally(lblCountdown);
            lblCountdown.Top = y;

            this.Controls.AddRange(new Control[] {
                grpButton, grpTiming, grpBorder, grpTimeLimit, grpPosition,
                grpHotkey, btnToggle, lblStatus, lblCountdown
            });

            this.KeyDown += Form1_KeyDown;

            // Enable window dragging – but NOT for buttons (so clicks work)
            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && !(s is Button))
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, 0x2, 0);
                }
            };
            foreach (Control ctrl in this.Controls)
            {
                ctrl.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left && !(ctrl is Button))
                    {
                        ReleaseCapture();
                        SendMessage(Handle, WM_NCLBUTTONDOWN, 0x2, 0);
                    }
                };
            }
        }

        private void UpdatePositionControls()
        {
            bool fixedMode = rbFixedPos.Checked;
            numPosX.Enabled = fixedMode;
            numPosY.Enabled = fixedMode;
            btnSelectLocation.Enabled = fixedMode;
        }

        // ──────────────────────────────────────────────
        //  Select Location (no minimise)
        // ──────────────────────────────────────────────
        private void BtnSelectLocation_Click(object sender, EventArgs e)
        {
            if (_isSelectingPosition)
            {
                UninstallMouseHook();
                _isSelectingPosition = false;
                btnSelectLocation.Text = "Select Location";
                btnSelectLocation.BackColor = AccentBlue;
                return;
            }

            _isSelectingPosition = true;
            btnSelectLocation.Text = "Click anywhere...";
            btnSelectLocation.BackColor = PausedGold;
            InstallMouseHook();
        }

        private void InstallMouseHook()
        {
            if (_mouseHookId != IntPtr.Zero) return;
            _hookProc = HookCallback;
            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, IntPtr.Zero, 0);
        }

        private static void UninstallMouseHook()
        {
            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                if (GetCursorPos(out POINT pt))
                {
                    UninstallMouseHook();
                    _instance.BeginInvoke(new Action(() =>
                    {
                        _instance.numPosX.Value = pt.x;
                        _instance.numPosY.Value = pt.y;
                        _instance._isSelectingPosition = false;
                        _instance.btnSelectLocation.Text = "Select Location";
                        _instance.btnSelectLocation.BackColor = AccentBlue;
                    }));
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // ──────────────────────────────────────────────
        //  Hotkey capture
        // ──────────────────────────────────────────────
        private void BtnSetHotkey_Click(object sender, EventArgs e)
        {
            _settingHotkey = true;
            lblHotkey.Text = "Press a key combination...";
            this.Focus();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_settingHotkey) return;
            e.SuppressKeyPress = true;
            _settingHotkey = false;

            uint mods = 0;
            if (e.Control) mods |= MOD_CONTROL;
            if (e.Alt) mods |= MOD_ALT;
            if (e.Shift) mods |= MOD_SHIFT;
            Keys key = e.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu)
            {
                lblHotkey.Text = "Invalid – try again";
                return;
            }

            UnregisterHotKey(Handle, HOTKEY_ID);
            _hotkeyModifiers = mods;
            _hotkeyKey = key;
            bool ok = RegisterHotKey(Handle, HOTKEY_ID, mods, (uint)key);
            if (ok)
                lblHotkey.Text = $"{ModsToString(mods)}{key}";
            else
            {
                lblHotkey.Text = "Registration failed – maybe already in use?";
                RegisterHotKey(Handle, HOTKEY_ID, _hotkeyModifiers, (uint)_hotkeyKey);
            }
        }

        private string ModsToString(uint mods)
        {
            string s = "";
            if ((mods & MOD_CONTROL) != 0) s += "Ctrl + ";
            if ((mods & MOD_ALT) != 0) s += "Alt + ";
            if ((mods & MOD_SHIFT) != 0) s += "Shift + ";
            return s;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
                ToggleAutoClicker();
            base.WndProc(ref m);
        }

        // ──────────────────────────────────────────────
        //  Button hover effects
        // ──────────────────────────────────────────────
        private void BtnToggle_MouseEnter(object sender, EventArgs e)
        {
            // brighten slightly on hover, but keep border unchanged
            if (_isRunning && !_isPausedByBorder)
                btnToggle.BackColor = Color.FromArgb(255, 90, 90);
            else if (!_isRunning)
                btnToggle.BackColor = Color.FromArgb(30, 230, 100);
        }

        private void BtnToggle_MouseLeave(object sender, EventArgs e)
        {
            btnToggle.BackColor = _currentToggleColor;
        }

        // ──────────────────────────────────────────────
        //  Start / Stop / Pause
        // ──────────────────────────────────────────────
        private void BtnToggle_Click(object sender, EventArgs e) => ToggleAutoClicker();

        private void ToggleAutoClicker()
        {
            if (_isRunning)
                StopClicking();
            else
                StartClicking();
        }

        private void StartClicking()
        {
            _isRunning = true;
            _isPausedByBorder = false;
            lblStatus.Text = "Status: Running";
            lblStatus.ForeColor = Color.LimeGreen;

            if (_useTimeLimit)
            {
                _startTime = DateTime.Now;
                lblCountdown.Visible = true;
                UpdateCountdown();
                _countdownTimer = new System.Windows.Forms.Timer { Interval = 500 };
                _countdownTimer.Tick += CountdownTick;
                _countdownTimer.Start();
            }
            else
                lblCountdown.Visible = false;

            AnimateButtonColor(StartGreen, StopRed);
            StartPulseEffect();

            _clickTimer = new System.Windows.Forms.Timer();
            _clickTimer.Tick += Timer_Tick;
            SetNextInterval();
            _clickTimer.Start();
        }

        private void StopClicking()
        {
            _isRunning = false;
            _isPausedByBorder = false;
            if (_clickTimer != null)
            {
                _clickTimer.Stop();
                _clickTimer.Dispose();
                _clickTimer = null;
            }
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();
            _countdownTimer = null;
            lblCountdown.Visible = false;

            lblStatus.Text = "Status: Stopped";
            lblStatus.ForeColor = Color.LightCoral;

            StopPulseEffect();
            AnimateButtonColor(_currentToggleColor, StartGreen);
        }

        private void CountdownTick(object sender, EventArgs e) => UpdateCountdown();

        private void UpdateCountdown()
        {
            if (!_isRunning || !_useTimeLimit) return;
            double remaining = _timeLimitSeconds - (DateTime.Now - _startTime).TotalSeconds;
            if (remaining <= 0)
            {
                lblCountdown.Text = "0s";
                StopClicking();
            }
            else
                lblCountdown.Text = $"{Math.Ceiling(remaining)}s left";
        }

        // Smooth color transition for toggle button
        private void AnimateButtonColor(Color from, Color to)
        {
            _targetToggleColor = to;
            _buttonColorTimer?.Stop();
            _buttonColorTimer = new System.Windows.Forms.Timer { Interval = 15 };
            int steps = 10;
            int step = 0;
            _buttonColorTimer.Tick += (s, e) =>
            {
                step++;
                float t = Math.Min(1, step / (float)steps);
                int r = Lerp(from.R, to.R, t);
                int g = Lerp(from.G, to.G, t);
                int b = Lerp(from.B, to.B, t);
                _currentToggleColor = Color.FromArgb(r, g, b);
                btnToggle.BackColor = _currentToggleColor;
                // Keep border colour in sync with the main colour (or fixed bright green)
                btnToggle.FlatAppearance.BorderColor = BorderGreen; // always bright green
                if (step >= steps)
                {
                    _buttonColorTimer.Stop();
                    _buttonColorTimer.Dispose();
                    _buttonColorTimer = null;
                }
            };
            _buttonColorTimer.Start();
        }

        private int Lerp(int a, int b, float t) => (int)(a + (b - a) * t);

        // Pulsing effect when running (smooth sine wave)
        private void StartPulseEffect()
        {
            StopPulseEffect();
            _pulseTimer = new System.Windows.Forms.Timer { Interval = 30 };
            _pulsePhase = 0f;
            _pulseTimer.Tick += PulseTick;
            _pulseTimer.Start();
        }

        private void StopPulseEffect()
        {
            _pulseTimer?.Stop();
            _pulseTimer?.Dispose();
            _pulseTimer = null;
        }

        private void PulseTick(object sender, EventArgs e)
        {
            if (!_isRunning || _isPausedByBorder) return;
            _pulsePhase += 0.08f;
            float intensity = (float)(Math.Sin(_pulsePhase) * 0.15 + 0.85);
            int r = (int)(StopRed.R * intensity);
            int g = (int)(StopRed.G * intensity);
            int b = (int)(StopRed.B * intensity);
            _currentToggleColor = Color.FromArgb(r, g, b);
            btnToggle.BackColor = _currentToggleColor;
            btnToggle.FlatAppearance.BorderColor = BorderGreen; // keep border green
        }

        // ──────────────────────────────────────────────
        //  Main click timer tick
        // ──────────────────────────────────────────────
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isRunning) return;

            // Time limit check
            if (_useTimeLimit && (DateTime.Now - _startTime).TotalSeconds >= _timeLimitSeconds)
            {
                StopClicking();
                return;
            }

            // Border control check
            if (_useBorderControl && IsCursorNearBorder(out _))
            {
                if (!_isPausedByBorder)
                {
                    _isPausedByBorder = true;
                    lblStatus.Text = "Status: Paused (border)";
                    lblStatus.ForeColor = PausedGold;
                    _clickTimer.Interval = 100;
                }
                return;
            }
            else if (_isPausedByBorder)
            {
                _isPausedByBorder = false;
                lblStatus.Text = "Status: Running";
                lblStatus.ForeColor = Color.LimeGreen;
                SetNextInterval();
            }

            DoMouseClick();
            SetNextInterval();
        }

        private bool IsCursorNearBorder(out Rectangle screenBounds)
        {
            GetCursorPos(out POINT pt);
            Screen screen = Screen.FromPoint(new Point(pt.x, pt.y));
            screenBounds = screen.Bounds;
            return pt.x <= screenBounds.Left + _borderMargin ||
                   pt.x >= screenBounds.Right - _borderMargin ||
                   pt.y <= screenBounds.Top + _borderMargin ||
                   pt.y >= screenBounds.Bottom - _borderMargin;
        }

        private void SetNextInterval()
        {
            int baseDelay = (int)numDelay.Value;
            int offset = (int)numOffset.Value;
            int actualDelay = baseDelay;
            if (offset > 0)
            {
                actualDelay = baseDelay + _rng.Next(-offset, offset + 1);
                if (actualDelay < 1) actualDelay = 1;
            }
            if (_clickTimer != null)
                _clickTimer.Interval = actualDelay;
        }

        private void DoMouseClick()
        {
            if (rbFixedPos.Checked)
            {
                SetCursorPos((int)numPosX.Value, (int)numPosY.Value);
            }

            bool isLeft = rbLeft.Checked;
            uint down = isLeft ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_RIGHTDOWN;
            uint up = isLeft ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTUP;

            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dwFlags = down;
            inputs[1].type = INPUT_MOUSE;
            inputs[1].mi.dwFlags = up;
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(Handle, HOTKEY_ID);
            UninstallMouseHook();
            StopClicking();
            base.OnFormClosing(e);
        }
    }
}