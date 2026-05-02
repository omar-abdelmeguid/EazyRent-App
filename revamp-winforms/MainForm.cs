using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EazyRentRevamp
{
    public partial class MainForm : Form
    {
        private readonly BackendService _backend;
        private DateTimePicker? _fromPicker;
        private DateTimePicker? _toPicker;
        private Label? _countLabel;
        private ComboBox? _modeBox;
        private DataGridView? _dataGrid;
        private Panel? _sidebarPanel;
        private Panel? _headerPanel;
        private Panel? _contentPanel;
        private Label? _statusLabel;
        private Panel? _statusBar;
        private Label? _sendResultLabel;
        private LoadingOverlay? _loadingOverlay;
        private Button? _fetchBtn;
        private Button? _sendBtn;
        private Label? _modeLabel;
        private Button? _homeSideBtn;
        private Button? _errorsSideBtn;
        private string _page = "home"; // home | errors
        private TextBox? _errorSearchBox;
        private Button? _errorSearchBtn;
        private FlowLayoutPanel? _errorSearchRow;
        private string _lastAppliedPage = "";

        // Design tokens
        private static readonly Color ColorBg          = Color.FromArgb(245, 246, 250);
        private static readonly Color ColorSidebar     = Color.FromArgb(15,  23,  42);
        private static readonly Color ColorSidebarText = Color.FromArgb(148, 163, 184);
        private static readonly Color ColorAccent      = Color.FromArgb(99,  102, 241);
        private static readonly Color ColorAccentHover = Color.FromArgb(79,  70,  229);
        private static readonly Color ColorCard        = Color.White;
        private static readonly Color ColorBorder      = Color.FromArgb(226, 232, 240);
        private static readonly Color ColorText        = Color.FromArgb(15,  23,  42);
        private static readonly Color ColorTextMuted   = Color.FromArgb(100, 116, 139);
        private static readonly Color ColorSuccess     = Color.FromArgb(34,  197,  94);
        private static readonly Color ColorDanger      = Color.FromArgb(239,  68,  68);
        private static readonly Color ColorHeaderBg    = Color.White;

        private static readonly Font FontTitle    = new Font("Segoe UI", 13f, FontStyle.Bold);
        private static readonly Font FontSubtitle = new Font("Segoe UI", 9f,  FontStyle.Regular);
        private static readonly Font FontLabel    = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        private static readonly Font FontBold     = new Font("Segoe UI", 9f,  FontStyle.Bold);
        private static readonly Font FontMono     = new Font("Consolas", 8.5f, FontStyle.Regular);

        public MainForm()
        {
            _backend = new BackendService();
            BuildUI();
        }

        // Marquee state
        private System.Windows.Forms.Timer? _marqueeTimer;
        private int _marqueeX;
        private int _marqueeSpeed = 1;
        private readonly string[] _marqueeMessages = new[]
        {
            "Welcome back!",
            "Sync your GL records in seconds.",
            "EazyRent Plus — Property Management made easy.",
            "All unsent records are ready to import.",
            "Tip: use Change DB to switch databases anytime.",
        };
        private string _marqueeText = "";
        private void BuildUI()
        {
            this.Text            = "EazyRent Plus";
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.WindowState     = FormWindowState.Maximized;
            this.MinimumSize     = new Size(1100, 650);
            this.BackColor       = ColorBg;
            this.Font            = new Font("Segoe UI", 9f);

            try { this.Icon = new Icon("easy-rent-ico.ico"); } catch { }

            // ── Sidebar ────────────────────────────────────────────────────────
            _sidebarPanel = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 220,
                BackColor = ColorSidebar
            };
            _sidebarPanel.Paint += SidebarPaint;

            var logoLabel = new Label
            {
                Text      = "EazyRent Plus",
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                Bounds    = new Rectangle(0, 0, 220, 64),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var logoSub = new Label
            {
                Text      = "Property Management",
                ForeColor = ColorSidebarText,
                Font      = FontSubtitle,
                Bounds    = new Rectangle(0, 58, 220, 22),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // FIX 2: sidebar "Home" instead of "Journal Import"
            _homeSideBtn = CreateSidebarButton("  Home", true, 110);
            _errorsSideBtn = CreateSidebarButton("  Errors", false, 154);
            var settingsSideBtn = CreateSidebarButton("  Settings", false, 198);
            settingsSideBtn.Click += (s, e) => OnSettings();
            _homeSideBtn.Click += async (s, e) => await NavigateTo("home");
            _errorsSideBtn.Click += async (s, e) => await NavigateTo("errors");

            _sidebarPanel.Controls.Add(_homeSideBtn);
            _sidebarPanel.Controls.Add(_errorsSideBtn);
            _sidebarPanel.Controls.Add(settingsSideBtn);

            var versionLabel = new Label
            {
                Text      = "v2.0  •  2026",
                ForeColor = Color.FromArgb(71, 85, 105),
                Font      = new Font("Segoe UI", 8f),
                Dock      = DockStyle.Bottom,
                Height    = 32,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _sidebarPanel.Controls.Add(logoLabel);
            _sidebarPanel.Controls.Add(logoSub);
            _sidebarPanel.Controls.Add(versionLabel);

            // ── Main area ──────────────────────────────────────────────────────
            var mainArea = new Panel { Dock = DockStyle.Fill, BackColor = ColorBg };

            // FIX 3: Header now only contains the scrolling marquee banner
            _headerPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = Color.FromArgb(238, 242, 255),
                Padding   = new Padding(0)
            };
            _headerPanel.Paint += HeaderBottomBorder;

            // Double-buffered panel to eliminate flicker
            var marqueeClip = new DoubleBufferedPanel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(238, 242, 255)
            };

            _marqueeText = string.Join("", _marqueeMessages);

            // We'll use the Panel's Paint to draw segments with separators
            var msgFont   = new Font("Segoe UI", 9f, FontStyle.Regular);
            var textColor = Color.FromArgb(79, 70, 229);
            var sepBg     = Color.FromArgb(79, 70, 229);   // darker indigo wall
            var sepFg     = Color.FromArgb(220, 220, 255);
            const int sepW   = 28;
            const int padMsg = 24;

            // Pre-measure each message width
            int[] msgWidths;
            using (var g = marqueeClip.CreateGraphics())
                msgWidths = Array.ConvertAll(_marqueeMessages, m => (int)g.MeasureString(m, msgFont).Width + padMsg * 2);

            int totalW = 0;
            foreach (var w in msgWidths) totalW += w + sepW;

            marqueeClip.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int x = _marqueeX;

                // Draw two full cycles so the loop is seamless
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    foreach (var (msg, mw) in _marqueeMessages.Zip(msgWidths, (a, b) => (a, b)))
                    {
                        // Message text centered in its slot
                        var textY = (marqueeClip.Height - msgFont.Height) / 2;
                        g.DrawString(msg, msgFont, new SolidBrush(textColor), x + padMsg, textY);
                        x += mw;

                        // Separator wall
                        var sepRect = new Rectangle(x, 0, sepW, marqueeClip.Height);
                        g.FillRectangle(new SolidBrush(sepBg), sepRect);
                        // House shape in center of wall
                        int mx = x + sepW / 2;
                        int my = marqueeClip.Height / 2;
                        int hw = 7; // half-width of house base
                        int hh = 5; // height of house body
                        int rh = 4; // height of roof
                        // Roof triangle
                        g.FillPolygon(new SolidBrush(sepFg), new[]
                        {
                            new Point(mx,      my - hh - rh),   // peak
                            new Point(mx - hw, my - hh),        // left eave
                            new Point(mx + hw, my - hh),        // right eave
                        });
                        // Body rectangle
                        g.FillRectangle(new SolidBrush(sepFg),
                            mx - hw, my - hh, hw * 2, hh + 3);
                        // Door (small dark rect)
                        g.FillRectangle(new SolidBrush(sepBg),
                            mx - 2, my - 1, 5, hh + 2);
                        x += sepW;
                    }
                }
            };

            marqueeClip.Controls.Clear();
            _headerPanel.Controls.Add(marqueeClip);

            // Timer scrolls by invalidating the panel each tick
            _marqueeTimer = new System.Windows.Forms.Timer { Interval = 18 };
            _marqueeTimer.Tick += (s, e) =>
            {
                if (marqueeClip.IsDisposed) return;
                _marqueeX -= _marqueeSpeed;
                if (_marqueeX < -totalW) _marqueeX = 0;
                marqueeClip.Invalidate();
            };
            _marqueeTimer.Start();

            // Remove the marquee banner and replace it with a simple strip matching the sidebar color.
            if (_marqueeTimer != null)
            {
                try { _marqueeTimer.Stop(); } catch { }
                try { _marqueeTimer.Dispose(); } catch { }
                _marqueeTimer = null;
            }
            _headerPanel.Controls.Clear();
            _headerPanel.Height= 20;
            _headerPanel.BackColor = ColorSidebar;

            // FIX 4: Filter bar now holds dates + mode + all 4 buttons
            var filterCard = new Panel
            {
                Height    = 96,
                Dock      = DockStyle.Top,
                BackColor = ColorCard,
                Padding   = new Padding(0)
            };
            filterCard.Paint += CardBottomBorder;

            var filterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var filterFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                BackColor     = Color.Transparent,
                Padding       = new Padding(16, 10, 16, 0)
            };

            filterFlow.Controls.Add(MakeFilterLabel("From"));
            _fromPicker = MakeDatePicker(DateTime.Today.AddDays(-7));
            filterFlow.Controls.Add(_fromPicker);
            filterFlow.Controls.Add(MakeFilterLabel("To"));
            _toPicker = MakeDatePicker(DateTime.Today);
            filterFlow.Controls.Add(_toPicker);

            _modeLabel = MakeFilterLabel("Status");
            filterFlow.Controls.Add(_modeLabel);
            _modeBox = MakeModeCombo();
            filterFlow.Controls.Add(_modeBox);

            // Spacer
            filterFlow.Controls.Add(new Label
            {
                Width     = 20,
                Height    = 36,
                BackColor = Color.Transparent
            });

            // Actions (page-dependent)
            _fetchBtn = CreateActionButton("Fetch",      ColorAccent, Color.White, false);
            _sendBtn  = CreateActionButton("Send Range", ColorAccent, Color.White, true);
            // Settings is available from the sidebar; keep the header clean.

            _fetchBtn.Click += async (s, e) => await OnFetchUnified();
            _sendBtn.Click  += async (s, e) => await OnSend();

            filterFlow.Controls.Add(_fetchBtn);
            filterFlow.Controls.Add(_sendBtn);

            filterLayout.Controls.Add(filterFlow, 0, 0);

            _errorSearchRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(16, 0, 16, 0),
                Margin = new Padding(0)
            };
            _errorSearchBox = MakeSearchBox();
            _errorSearchBtn = MakeSearchButton();
            _errorSearchBtn.Click += async (s, e) => await OnErrorSearch();
            _errorSearchRow.Controls.Add(_errorSearchBtn);
            _errorSearchRow.Controls.Add(_errorSearchBox);
            SetCueBanner(_errorSearchBox, "search with serial number or Room Number");

            filterLayout.Controls.Add(_errorSearchRow, 0, 1);
            filterCard.Controls.Add(filterLayout);

            // ── Stats bar ──────────────────────────────────────────────────────
            var statsBar = new Panel
            {
                Height    = 36,
                Dock      = DockStyle.Top,
                BackColor = ColorBg,
                Padding   = new Padding(20, 6, 20, 0)
            };
            _countLabel = new Label
            {
                Text      = "No records loaded. click View to begin",
                ForeColor = ColorTextMuted,
                Font      = FontLabel,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _sendResultLabel = new Label
            {
                Text      = string.Empty,
                ForeColor = ColorTextMuted,
                Font      = FontLabel,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            };

            var statsLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = Color.Transparent
            };
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f));
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            statsLayout.Controls.Add(_countLabel, 0, 0);
            statsLayout.Controls.Add(_sendResultLabel, 1, 0);
            statsBar.Controls.Add(statsLayout);

            // ── Data grid  (FIX 1: bottom padding so it never touches footer) ──
            _contentPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = ColorBg,
                Padding   = new Padding(20, 0, 20, 20)   // <-- 20px bottom gap
            };

            var gridWrapper = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = ColorCard,
                Padding   = new Padding(0)
            };
            gridWrapper.Paint += GridWrapperPaint;

            _dataGrid = new DataGridView
            {
                Dock                      = DockStyle.Fill,
                ReadOnly                  = true,
                AutoGenerateColumns       = false,
                AllowUserToAddRows        = false,
                AllowUserToDeleteRows     = false,
                BorderStyle               = BorderStyle.None,
                BackgroundColor           = ColorCard,
                GridColor                 = ColorBorder,
                RowHeadersVisible         = false,
                EnableHeadersVisualStyles = false,
                SelectionMode             = DataGridViewSelectionMode.FullRowSelect,
                CellBorderStyle           = DataGridViewCellBorderStyle.SingleHorizontal,
                Font                      = FontLabel
            };

            StyleDataGrid(_dataGrid);
            ConfigureGridColumns(_dataGrid);

            gridWrapper.Controls.Add(_dataGrid);
            _contentPanel.Controls.Add(gridWrapper);

            // ── Status bar ─────────────────────────────────────────────────────
            _statusBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 28,
                BackColor = ColorSidebar
            };
            _statusLabel = new Label
            {
                Text      = "Ready",
                ForeColor = ColorSidebarText,
                Font      = new Font("Segoe UI", 8f),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(12, 0, 0, 0)
            };
            _statusBar.Controls.Add(_statusLabel);

            // ── Assembly ───────────────────────────────────────────────────────
            mainArea.Controls.Add(_contentPanel);
            mainArea.Controls.Add(statsBar);
            mainArea.Controls.Add(filterCard);
            mainArea.Controls.Add(_headerPanel);

            this.Controls.Add(mainArea);
            this.Controls.Add(_statusBar);
            this.Controls.Add(_sidebarPanel);

            // Modern loading overlay (shown during fetch/send)
            _loadingOverlay = new LoadingOverlay();
            this.Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();

            ApplyPageUi();
        }

        // ── Paint helpers ──────────────────────────────────────────────────────

        private void SidebarPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            using var accent = new SolidBrush(ColorAccent);
            g.FillRectangle(accent, 0, 0, 4, ((Panel)sender!).Height);
        }

        private void HeaderBottomBorder(object? sender, PaintEventArgs e)
        {
            var p = (Panel)sender!;
            using var pen = new Pen(ColorBorder);
            e.Graphics.DrawLine(pen, 0, p.Height - 1, p.Width, p.Height - 1);
        }

        private void CardBottomBorder(object? sender, PaintEventArgs e)
        {
            var p = (Panel)sender!;
            using var pen = new Pen(ColorBorder);
            e.Graphics.DrawLine(pen, 0, p.Height - 1, p.Width, p.Height - 1);
        }

        private void GridWrapperPaint(object? sender, PaintEventArgs e)
        {
            var p = (Panel)sender!;
            var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
            using var pen = new Pen(ColorBorder);
            e.Graphics.DrawRectangle(pen, rect);
        }

        // ── Factory helpers ────────────────────────────────────────────────────

        private Button CreateSidebarButton(string text, bool active, int y)
        {
            var bg   = active ? ColorAccent : Color.Transparent;
            var fg   = active ? Color.White : ColorSidebarText;
            var btn  = new Button
            {
                Text      = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Font      = active ? FontBold : FontLabel,
                Bounds    = new Rectangle(12, y, 196, 38),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize       = 0;
            btn.FlatAppearance.MouseOverBackColor = active
                ? ColorAccentHover
                : Color.FromArgb(30, 255, 255, 255);
            return btn;
        }

        private Button CreateActionButton(string text, Color bg, Color fg,
                                          bool primary, Color? border = null)
        {
            var btn = new Button
            {
                Text      = text,
                AutoSize  = false,
                Width     = primary ? 120 : 110,
                Height    = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Font      = primary ? FontBold : FontLabel,
                Margin    = new Padding(4, 0, 0, 0),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize  = border.HasValue ? 1 : 0;
            btn.FlatAppearance.BorderColor = border ?? bg;
            btn.FlatAppearance.MouseOverBackColor = primary
                ? ColorAccentHover
                : Color.FromArgb(248, 250, 252);
            return btn;
        }

        private Label MakeFilterLabel(string text) => new Label
        {
            Text      = text,
            ForeColor = ColorTextMuted,
            Font      = FontLabel,
            AutoSize  = false,
            Width     = 58,
            Height    = 36,
            TextAlign = ContentAlignment.MiddleRight,
            Margin    = new Padding(8, 0, 4, 0)
        };

        private DateTimePicker MakeDatePicker(DateTime val) => new DateTimePicker
        {
            Format  = DateTimePickerFormat.Short,
            Value   = val,
            Width   = 128,
            Height  = 36,
            Font    = FontLabel,
            Margin  = new Padding(0, 0, 8, 0)
        };

        private ComboBox MakeModeCombo()
        {
            var cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 110,
                Height        = 36,
                Font          = FontLabel,
                Margin        = new Padding(0, 0, 8, 0)
            };
            cb.Items.AddRange(new string[] { "unsent", "sent" });
            cb.SelectedIndex = 0;
            return cb;
        }

        private TextBox MakeSearchBox()
        {
            return new TextBox
            {
                Width = 280,
                Height = 36,
                Font = FontLabel,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 6, 0)
            };
        }

        private Button MakeSearchButton()
        {
            var btn = new Button
            {
                Text = "🔍",
                Width = 42,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = ColorText,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = ColorBorder;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 250, 252);
            return btn;
        }

        private static void SetCueBanner(TextBox textBox, string cue)
        {
            try
            {
                SendMessage(textBox.Handle, 0x1501, (IntPtr)1, cue); // EM_SETCUEBANNER
            }
            catch
            {
                // ignore
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void StyleDataGrid(DataGridView grid)
        {
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor         = Color.White,
                ForeColor         = Color.FromArgb(30, 41, 59),
                SelectionBackColor= Color.FromArgb(238, 242, 255),
                SelectionForeColor= Color.FromArgb(30, 41, 59),
                Font              = new Font("Segoe UI", 8.5f),
                Padding           = new Padding(6, 4, 6, 4)
            };
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor          = Color.FromArgb(248, 250, 252),
                ForeColor          = Color.FromArgb(30, 41, 59),
                SelectionBackColor = Color.FromArgb(238, 242, 255),
                SelectionForeColor = Color.FromArgb(30, 41, 59),
                Font               = new Font("Segoe UI", 8.5f),
                Padding            = new Padding(6, 4, 6, 4)
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Padding   = new Padding(6, 6, 6, 6),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            grid.ColumnHeadersHeight       = 38;
            grid.RowTemplate.Height        = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        private static void ConfigureGridColumns(DataGridView grid)
        {
            grid.Columns.Clear();
            void Add(string name, string header, string prop, int w = 110)
                => grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name             = name,
                    HeaderText       = header,
                    DataPropertyName = prop,
                    Width            = w,
                    MinimumWidth     = 60,
                    Resizable        = DataGridViewTriState.True
                });

            Add("Room_no",         "Room",            nameof(Record.RoomNo),         70);
            Add("Descr1",          "Description",     nameof(Record.Descr1),        180);
            Add("Rent_no",         "Rent",          nameof(Record.RentNo),         80);
            Add("Date",            "Date",            nameof(Record.Date),           100);
            Add("Amount",          "Amount",          nameof(Record.Amount),          90);
            Add("Type",            "Transaction Type",            nameof(Record.Type),            90);
            Add("DebitAccount1",   "Debit Account 1",   nameof(Record.DebitAccount1),  150);
            Add("DebitAccount2",   "Debit Account 2",   nameof(Record.DebitAccount2),  150);
            Add("CreditAccount1",  "Credit Account 1",  nameof(Record.CreditAccount1), 150);
            Add("CreditAccount2",  "Credit Account 2",  nameof(Record.CreditAccount2), 150);
            Add("DrcostCenterCode","Dr Cost Center",    nameof(Record.DrCostCenterCode),150);
            Add("Crcostcentercode","Cr Cost Center",    nameof(Record.CrCostCenterCode),150);
            Add("Ser",             "Serial",         nameof(Record.Ser),             90);

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        }

        private void ConfigureJournalGrid()
        {
            if (_dataGrid == null) return;
            if (InvokeRequired) { Invoke(() => ConfigureJournalGrid()); return; }
            ConfigureGridColumns(_dataGrid);
        }

        private void ConfigureErrorGrid()
        {
            if (_dataGrid == null) return;
            if (InvokeRequired) { Invoke(() => ConfigureErrorGrid()); return; }
            ConfigureErrorGridColumns(_dataGrid);
        }

        private static void ConfigureErrorGridColumns(DataGridView grid)
        {
            grid.Columns.Clear();
            void Add(string name, string header, string prop, int w = 110)
                => grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name             = name,
                    HeaderText       = header,
                    DataPropertyName = prop,
                    Width            = w,
                    MinimumWidth     = 60,
                    Resizable        = DataGridViewTriState.True
                });

            Add("Room_no",  "Room",      nameof(ErrorRecord.RoomNo), 70);
            Add("Descr1",   "Description", nameof(ErrorRecord.Descr1), 180);
            Add("Rent_no",  "Rent #",    nameof(ErrorRecord.RentNo), 80);
            Add("Date_OF_DB", "Date",    nameof(ErrorRecord.DateOfDb), 120);
            Add("Amount",   "Amount",    nameof(ErrorRecord.Amount), 90);
            Add("Type",     "Type",      nameof(ErrorRecord.Type), 70);
            Add("DebitAccount1",  "Debit Acct 1", nameof(ErrorRecord.DebitAccount1), 110);
            Add("DebitAccount2",  "Debit Acct 2", nameof(ErrorRecord.DebitAccount2), 110);
            Add("CreditAccount1", "Credit Acct 1", nameof(ErrorRecord.CreditAccount1), 110);
            Add("CreditAccount2", "Credit Acct 2", nameof(ErrorRecord.CreditAccount2), 110);
            Add("DrcostCenter",   "Dr Cost Ctr", nameof(ErrorRecord.DrCostCenter), 100);
            Add("CrcostCenter",   "Cr Cost Ctr", nameof(ErrorRecord.CrCostCenter), 100);
            Add("Ser",      "Serial",    nameof(ErrorRecord.Ser), 70);
            Add("ERORR",    "Error",     nameof(ErrorRecord.Error), 280);
            Add("failed_requests_timestamp", "Failed At", nameof(ErrorRecord.FailedRequestsTimestamp), 140);

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private async Task OnFetchUnified()
        {
            if (_page == "errors")
            {
                await OnErrors();
                return;
            }

            await OnFetch();
        }

        private async Task OnFetch()
        {
            try
            {
                SetStatus("Fetching records...", ColorSidebarText);
                ShowLoading("Fetching records");
                ToggleUi(false);
                var from  = _fromPicker!.Value.Date;
                var to    = _toPicker!.Value.Date;
                var mode  = (_modeBox?.SelectedItem as string) ?? "unsent";
                ConfigureJournalGrid();
                var rows  = await Task.Run(() => _backend.FetchRecords(from, to, mode));
                _dataGrid!.DataSource = rows;
                int total = await Task.Run(() => _backend.CountRecords(from, to, mode));
                _countLabel!.Text = $"Showing {rows.Count} of {total} records   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}   •   Mode: {mode}";
                if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
                SetStatus($"Fetched {rows.Count} records", ColorSuccess);
            }
            catch (Exception ex)
            {
                SetStatus("Fetch failed", ColorDanger);
                ShowError("Error fetching records", ex.Message);
            }
            finally { HideLoading(); ToggleUi(true); }
        }

        private async Task OnErrors()
        {
            try
            {
                SetStatus("Loading errors...", ColorSidebarText);
                ShowLoading("Loading errors");
                ToggleUi(false);
                var from = _fromPicker!.Value.Date;
                var to = _toPicker!.Value.Date;

                ConfigureErrorGrid();
                var rows = await Task.Run(() => _backend.FetchErrorRecords(from, to));
                _dataGrid!.DataSource = rows;
                int total = await Task.Run(() => _backend.CountErrorRecords(from, to));
                _countLabel!.Text = $"Search results: {rows.Count} of {total}   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}";
                if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
                SetStatus($"Loaded {rows.Count} errors", ColorSuccess);
            }
            catch (System.Exception ex)
            {
                SetStatus("Load errors failed", ColorDanger);
                ShowError("Error loading errors", ex.Message);
            }
            finally { HideLoading(); ToggleUi(true); }
        }

        private async Task OnErrorSearch()
        {
            if (_page != "errors")
            {
                await NavigateTo("errors");
            }

            var serial = _errorSearchBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(serial))
            {
                await OnErrors();
                return;
            }

            try
            {
                SetStatus("Searching errors...", ColorSidebarText);
                ShowLoading("Searching errors");
                ToggleUi(false);
                var from = _fromPicker!.Value.Date;
                var to = _toPicker!.Value.Date;

                ConfigureErrorGrid();
                var rows = await Task.Run(() => _backend.SearchErrorRecords(from, to, serial));
                _dataGrid!.DataSource = rows;
                int total = await Task.Run(() => _backend.CountErrorRecordsSearch(from, to, serial));
                _countLabel!.Text = $"Search Results for ({serial}): {rows.Count} of {total}   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}";
                if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
                SetStatus($"Found {rows.Count} errors", ColorSuccess);
            }
            catch (System.Exception ex)
            {
                SetStatus("Search errors failed", ColorDanger);
                ShowError("Error searching errors", ex.Message);
            }
            finally { HideLoading(); ToggleUi(true); }
        }

        private async Task OnSend()
        {
            var confirm = MessageBox.Show(
                "Send all unsent records in the selected date range to the API?",
                "Confirm Send", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                SetStatus("Sending records...", ColorSidebarText);
                ShowLoading("Sending records");
                ToggleUi(false);
                var from = _fromPicker!.Value.Date;
                var to   = _toPicker!.Value.Date;

                if (_modeBox != null) _modeBox.SelectedItem = "unsent";
                var rows  = await Task.Run(() => _backend.FetchRecords(from, to, "unsent"));
                _dataGrid!.DataSource = rows;
                int total = await Task.Run(() => _backend.CountRecords(from, to, "unsent"));
                _countLabel!.Text = $"Showing {rows.Count} of {total} records   •   Mode: unsent";

                var result = await Task.Run(() => _backend.SendRange(from, to));
                var isOk   = result.ToUpper().StartsWith("OK");
                SetStatus(isOk ? "Send completed" : "Send completed with errors", isOk ? ColorSuccess : ColorDanger);

                if (_sendResultLabel != null)
                {
                    _sendResultLabel.ForeColor = isOk ? ColorSuccess : ColorDanger;
                    _sendResultLabel.Text = FormatSendResultForUi(result);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Send failed", ColorDanger);
                ShowError("Error sending range", ex.Message);
            }
            finally { HideLoading(); ToggleUi(true); }
        }

        private static string FormatSendResultForUi(string result)
        {
            if (string.IsNullOrWhiteSpace(result)) return string.Empty;
            var singleLine = result.Replace("\r", " ").Replace("\n", " ").Trim();
            const int max = 90;
            return singleLine.Length <= max ? singleLine : singleLine.Substring(0, max) + "...";
        }

        private void OnChangeDb()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Access Database|*.mdb;*.accdb",
                Title  = "Select Main Database"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _backend.SetDbPath(ofd.FileName);
                SetStatus($"Database: {System.IO.Path.GetFileName(ofd.FileName)}", ColorSuccess);
                MessageBox.Show($"Database set to:\n{ofd.FileName}", "Database Changed",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnSettings()
        {
            if (!PromptForSettingsPassword()) return;
            using var dlg = new Form
            {
                Width           = 540,
                Height          = 380,
                Text            = "Settings — EazyRent Plus",
                StartPosition   = FormStartPosition.CenterParent,
                BackColor       = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false,
                Font            = new Font("Segoe UI", 9f)
            };

            int y = 20;
            Label MkLbl(string t) { var l = new Label { Left = 24, Top = y, Width = 490, Text = t, ForeColor = ColorTextMuted, Font = FontLabel }; return l; }
            TextBox MkTxt(string v) { var tb = new TextBox { Left = 24, Top = y + 18, Width = 490, Text = v, Font = FontLabel, BorderStyle = BorderStyle.FixedSingle }; return tb; }

            dlg.Controls.Add(MkLbl("Main DB Path (.accdb / .mdb)")); var txtDb = MkTxt(_backend.DatabasePath); txtDb.Width = 400; dlg.Controls.Add(txtDb);
            var browseDb = new Button { Text = "Browse...", Left = 428, Top = y + 16, Width = 86, Height = 26, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = FontLabel };
            browseDb.FlatAppearance.BorderColor = ColorBorder;
            browseDb.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "Access DB|*.accdb;*.mdb", Title = "Select Main DB" };
                if (ofd.ShowDialog(dlg) == DialogResult.OK) txtDb.Text = ofd.FileName;
            };
            dlg.Controls.Add(browseDb);
            y += 56;

            dlg.Controls.Add(MkLbl("Login URL")); var txtLogin = MkTxt(_backend.LoginUrl); dlg.Controls.Add(txtLogin); y += 56;
            dlg.Controls.Add(MkLbl("Import URL")); var txtImport = MkTxt(_backend.ImportUrl); dlg.Controls.Add(txtImport); y += 56;
            dlg.Controls.Add(MkLbl("Error DB Path (.accdb / .mdb)")); var txtError = MkTxt(_backend.ErrorDbPath); txtError.Width = 400; dlg.Controls.Add(txtError);

            var browse = new Button { Text = "Browse...", Left = 428, Top = y + 16, Width = 86, Height = 26, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = FontLabel };
            browse.FlatAppearance.BorderColor = ColorBorder;
            browse.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "Access DB|*.accdb;*.mdb", Title = "Select Error DB" };
                if (ofd.ShowDialog(dlg) == DialogResult.OK) txtError.Text = ofd.FileName;
            };
            dlg.Controls.Add(browse);

            var ok = new Button { Text = "Save Settings", Left = 320, Top = 250, Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = ColorAccent, ForeColor = Color.White, Font = FontBold };
            ok.FlatAppearance.BorderSize = 0;
            var cancel = new Button { Text = "Cancel", Left = 440, Top = 250, Width = 74, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = FontLabel };
            cancel.FlatAppearance.BorderColor = ColorBorder;
            ok.Click     += (s, e) => { dlg.Tag = Tuple.Create(txtDb.Text, txtLogin.Text, txtImport.Text, txtError.Text); dlg.DialogResult = DialogResult.OK; dlg.Close(); };
            cancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
            dlg.Controls.Add(ok);
            dlg.Controls.Add(cancel);

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var t = (Tuple<string, string, string, string>?)dlg.Tag;
                var dbPath = t?.Item1 ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(dbPath))
                    _backend.SetDbPath(dbPath);

                _backend.SetLoginUrl(t?.Item2  ?? string.Empty);
                _backend.SetImportUrl(t?.Item3 ?? string.Empty);
                _backend.SetErrorDbPath(t?.Item4 ?? string.Empty);
                SetStatus("Settings saved", ColorSuccess);
            }
        }

        private static bool PromptForSettingsPassword()
        {
            const string required = "4050032";

            while (true)
            {
                using var dlg = new Form
                {
                    Width = 360,
                    Height = 170,
                    Text = "Enter Password",
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    BackColor = Color.White,
                    Font = new Font("Segoe UI", 9f)
                };

                var lbl = new Label
                {
                    Left = 18,
                    Top = 18,
                    Width = 310,
                    Text = "Password required to open Settings:",
                    ForeColor = Color.FromArgb(100, 116, 139)
                };

                var txt = new TextBox
                {
                    Left = 18,
                    Top = 45,
                    Width = 310,
                    UseSystemPasswordChar = true
                };

                var ok = new Button { Text = "OK", Left = 176, Top = 85, Width = 72, Height = 28 };
                var cancel = new Button { Text = "Cancel", Left = 256, Top = 85, Width = 72, Height = 28 };
                ok.Click += (s, e) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
                cancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };

                dlg.Controls.Add(lbl);
                dlg.Controls.Add(txt);
                dlg.Controls.Add(ok);
                dlg.Controls.Add(cancel);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;

                var result = dlg.ShowDialog();
                if (result != DialogResult.OK) return false;

                if (txt.Text == required) return true;

                MessageBox.Show("Wrong password. Try again.", "Incorrect Password",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void SetStatus(string msg, Color color)
        {
            if (_statusLabel == null) return;
            if (InvokeRequired) { Invoke(() => SetStatus(msg, color)); return; }
            _statusLabel.Text      = $"  {msg}";
            _statusLabel.ForeColor = color;
        }

        private static void ShowError(string title, string msg)
            => MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        private void ToggleUi(bool enable)
        {
            if (InvokeRequired) { Invoke(() => ToggleUi(enable)); return; }
            foreach (Control c in this.Controls)
            {
                if (_loadingOverlay != null && ReferenceEquals(c, _loadingOverlay)) continue;
                c.Enabled = enable;
            }
        }

        private void ShowLoading(string message)
        {
            if (_loadingOverlay == null) return;
            if (InvokeRequired) { Invoke(() => ShowLoading(message)); return; }
            _loadingOverlay.Show(message);
        }

        private void HideLoading()
        {
            if (_loadingOverlay == null) return;
            if (InvokeRequired) { Invoke(() => HideLoading()); return; }
            _loadingOverlay.HideOverlay();
        }

        private async Task NavigateTo(string page)
        {
            _page = page == "errors" ? "errors" : "home";
            ApplyPageUi();
            await Task.CompletedTask;
        }

        private void ApplyPageUi()
        {
            if (InvokeRequired) { Invoke(() => ApplyPageUi()); return; }

            var isErrors = _page == "errors";
            if (_lastAppliedPage != _page)
            {
                ClearGridAndStats();
                _lastAppliedPage = _page;
            }

            if (_sendBtn != null) _sendBtn.Visible = !isErrors;
            if (_modeBox != null) _modeBox.Visible = !isErrors;
            if (_modeLabel != null) _modeLabel.Visible = !isErrors;
            if (_errorSearchRow != null) _errorSearchRow.Visible = isErrors;

            if (_fetchBtn != null) _fetchBtn.Text = isErrors ? "Fetch Errors" : "Fetch";

            if (_dataGrid != null)
            {
                if (isErrors) ConfigureErrorGridColumns(_dataGrid);
                else ConfigureGridColumns(_dataGrid);
            }

            SetSidebarActive(_homeSideBtn, !isErrors);
            SetSidebarActive(_errorsSideBtn, isErrors);

            if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
            SetStatus(isErrors ? "Errors view" : "Ready", ColorSidebarText);
        }

        private void ClearGridAndStats()
        {
            if (_dataGrid != null) _dataGrid.DataSource = null;
            if (_countLabel != null) _countLabel.Text = string.Empty;
            if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
            if (_errorSearchBox != null) _errorSearchBox.Text = string.Empty;
        }

        private static void SetSidebarActive(Button? btn, bool active)
        {
            if (btn == null) return;
            btn.BackColor = active ? ColorAccent : Color.Transparent;
            btn.ForeColor = active ? Color.White : ColorSidebarText;
            btn.Font = active ? FontBold : FontLabel;
            btn.FlatAppearance.MouseOverBackColor = active
                ? ColorAccentHover
                : Color.FromArgb(30, 255, 255, 255);
        }
    }

    // Flicker-free panel using double buffering
    internal sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.AllPaintingInWmPaint  |
                          ControlStyles.UserPaint, true);
        }
    }
}
