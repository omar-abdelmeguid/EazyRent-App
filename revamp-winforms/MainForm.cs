using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EazyRentRevamp
{
    public partial class MainForm : Form
    {
        private Panel? _resultCard;
        private Label? _resultCardText;
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
        private Button? _settingsSideBtn;
        private Label? _logoLabel;
        private Label? _logoSubLabel;
        private Label? _versionLabel;
        private ComboBox? _langBox;
        private Label? _fromLabel;
        private Label? _toLabel;
        private Label? _heroTitleLabel;
        private Label? _heroSubtitleLabel;
        private Label? _heroBadgeLabel;
        private bool _applyingLanguage;
        private string _page = "home"; // home | errors
        private TextBox? _errorSearchBox;
        private Button? _errorSearchBtn;
        private FlowLayoutPanel? _errorSearchRow;
        private string _lastAppliedPage = "";

        // Design tokens
        private static readonly Color ColorBg          = Color.FromArgb(242, 246, 244);
        private static readonly Color ColorSidebar     = Color.FromArgb(18,  32,  47);
        private static readonly Color ColorSidebarText = Color.FromArgb(154, 169, 184);
        private static readonly Color ColorAccent      = Color.FromArgb(13,  148, 136);
        private static readonly Color ColorAccentHover = Color.FromArgb(15,  118, 110);
        private static readonly Color ColorCard        = Color.White;
        private static readonly Color ColorBorder      = Color.FromArgb(219, 227, 231);
        private static readonly Color ColorText        = Color.FromArgb(15,  23,  42);
        private static readonly Color ColorTextMuted   = Color.FromArgb(96,  108, 125);
        private static readonly Color ColorSuccess     = Color.FromArgb(22,  163,  74);
        private static readonly Color ColorDanger      = Color.FromArgb(220,  38,  38);
        private static readonly Color ColorHeaderBg    = Color.White;

        private static readonly Font FontTitle    = new Font("Segoe UI", 13f, FontStyle.Bold);
        private static readonly Font FontSubtitle = new Font("Segoe UI", 9f,  FontStyle.Regular);
        private static readonly Font FontLabel    = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        private static readonly Font FontBold     = new Font("Segoe UI", 9f,  FontStyle.Bold);
        private static readonly Font FontMono     = new Font("Consolas", 8.5f, FontStyle.Regular);

        public MainForm()
        {
            _backend = new BackendService();
            L.Set(_backend.Language);
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
            this.Text            = L.Str(StringKey.AppTitle);
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

            _logoLabel = new Label
            {
                Text      = L.Str(StringKey.AppTitle),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                Bounds    = new Rectangle(0, 14, 220, 42),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _logoSubLabel = new Label
            {
                Text      = L.Str(StringKey.SidebarSubtitle),
                ForeColor = ColorSidebarText,
                Font      = FontSubtitle,
                Bounds    = new Rectangle(0, 52, 220, 22),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // FIX 2: sidebar "Home" instead of "Journal Import"
            _homeSideBtn = CreateSidebarButton(L.Str(StringKey.SidebarHome), true, 118);
            _errorsSideBtn = CreateSidebarButton(L.Str(StringKey.SidebarErrors), false, 166);
            _settingsSideBtn = CreateSidebarButton(L.Str(StringKey.SidebarSettings), false, 214);
            _settingsSideBtn.Click += (s, e) => OnSettings();
            _homeSideBtn.Click += async (s, e) => await NavigateTo("home");
            _errorsSideBtn.Click += async (s, e) => await NavigateTo("errors");

            _sidebarPanel.Controls.Add(_homeSideBtn);
            _sidebarPanel.Controls.Add(_errorsSideBtn);
            _sidebarPanel.Controls.Add(_settingsSideBtn);

            // Language selector (sidebar)
            var languageHost = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36 + 30,
                Padding = new Padding(20, 0, 20, 30)
            };
            _langBox = MakeLanguageCombo();
            _langBox.Dock = DockStyle.Bottom;
            _langBox.Height = 36;
            languageHost.Controls.Add(_langBox);
            _sidebarPanel.Controls.Add(languageHost);

            _versionLabel = new Label
            {
                Text      = L.Str(StringKey.VersionLabel),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font      = new Font("Segoe UI", 8f),
                Dock      = DockStyle.Bottom,
                Height    = 32,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _sidebarPanel.Controls.Add(_logoLabel);
            _sidebarPanel.Controls.Add(_logoSubLabel);
            _sidebarPanel.Controls.Add(_versionLabel);

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
                       // Pre-create brushes ONCE — put these lines just before marqueeClip.Paint +=
var brushText  = new SolidBrush(textColor);
var brushSepBg = new SolidBrush(sepBg);
var brushSepFg = new SolidBrush(sepFg);

// Then replace the inner loop content:
marqueeClip.Paint += (s, e) =>
{
    var g = e.Graphics;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    int x = _marqueeX;

    for (int cycle = 0; cycle < 2; cycle++)
    {
        foreach (var (msg, mw) in _marqueeMessages.Zip(msgWidths, (a, b) => (a, b)))
        {
            // Message text
                var textY = (marqueeClip.Height - msgFont.Height) / 2;
                g.DrawString(msg, msgFont, brushText, x + padMsg, textY); // ← reused
                x += mw;

                // Separator wall
                var sepRect = new Rectangle(x, 0, sepW, marqueeClip.Height);
                g.FillRectangle(brushSepBg, sepRect); // ← reused

                // House shape
                int mx = x + sepW / 2;
                int my = marqueeClip.Height / 2;
                int hw = 7, hh = 5, rh = 4;

                g.FillPolygon(brushSepFg, new[]   // ← reused
                {
                    new Point(mx,      my - hh - rh),
                    new Point(mx - hw, my - hh),
                    new Point(mx + hw, my - hh),
                });
                g.FillRectangle(brushSepFg, mx - hw, my - hh, hw * 2, hh + 3); // ← reused
                g.FillRectangle(brushSepBg, mx - 2,  my - 1,  5,      hh + 2); // ← reused

                x += sepW;
            }
        }
    };

    // Clean up the 3 brush objects when panel is destroyed
    marqueeClip.Disposed += (s, e) =>
    {
        brushText.Dispose();
        brushSepBg.Dispose();
        brushSepFg.Dispose();
        msgFont.Dispose();
    };
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

            _headerPanel.Height = 124;
            _headerPanel.BackColor = ColorHeaderBg;
            _headerPanel.Padding = new Padding(20, 18, 20, 18);
            _headerPanel.Paint += HeroHeaderPaint;

            var heroLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74f));
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));

            var heroTextHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            _heroTitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                ForeColor = ColorText,
                TextAlign = L.AlignNearMiddle()
            };
            _heroSubtitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = ColorTextMuted,
                TextAlign = L.AlignNearMiddle()
            };
            heroTextHost.Controls.Add(_heroSubtitleLabel);
            heroTextHost.Controls.Add(_heroTitleLabel);

            var badgeHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 10)
            };
            _heroBadgeLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 168,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ColorAccent,
                BackColor = Color.FromArgb(232, 250, 247),
                TextAlign = ContentAlignment.MiddleCenter
            };
            badgeHost.Controls.Add(_heroBadgeLabel);

            heroLayout.Controls.Add(heroTextHost, 0, 0);
            heroLayout.Controls.Add(badgeHost, 1, 0);
            _headerPanel.Controls.Add(heroLayout);

            // FIX 4: Filter bar now holds dates + mode + all 4 buttons
            var filterCard = new Panel
            {
                Height    = 104,
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
                Padding       = new Padding(18, 12, 18, 0)
            };

            _fromLabel = MakeFilterLabel(L.Str(StringKey.From));
            filterFlow.Controls.Add(_fromLabel);
            _fromPicker = MakeDatePicker(DateTime.Today.AddDays(-7));
            filterFlow.Controls.Add(_fromPicker);
            _toLabel = MakeFilterLabel(L.Str(StringKey.To));
            filterFlow.Controls.Add(_toLabel);
            _toPicker = MakeDatePicker(DateTime.Today);
            filterFlow.Controls.Add(_toPicker);

            _modeLabel = MakeFilterLabel(L.Str(StringKey.Status));
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
            _fetchBtn = CreateActionButton(L.Str(StringKey.Fetch),      Color.White, ColorAccent, false, ColorAccent);
            _sendBtn  = CreateActionButton(L.Str(StringKey.SendRange), ColorAccent, Color.White, true);
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
            SetCueBanner(_errorSearchBox, L.Str(StringKey.SearchCue));

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
                Text      = L.Str(StringKey.NoRecordsLoaded),
                ForeColor = ColorTextMuted,
                Font      = FontLabel,
                Dock      = DockStyle.Fill,
                TextAlign = L.AlignNearMiddle()
            };
            _sendResultLabel = new Label
            {
                Text      = string.Empty,
                ForeColor = ColorTextMuted,
                Font      = FontLabel,
                Dock      = DockStyle.Fill,
                TextAlign = L.AlignFarMiddle()
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
                Height    = 32,
                BackColor = ColorSidebar
            };
            _statusLabel = new Label
            {
                Text      = L.Str(StringKey.Ready),
                ForeColor = ColorSidebarText,
                Font      = new Font("Segoe UI", 8f),
                Dock      = DockStyle.Fill,
                TextAlign = L.AlignNearMiddle(),
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
            BuildResultCard(_contentPanel!);
            ApplyPageUi();
            ApplyLanguageUi();
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

        private void HeroHeaderPaint(object? sender, PaintEventArgs e)
        {
            var panel = (Panel)sender!;
            var rect = panel.ClientRectangle;
            using var brush = new LinearGradientBrush(
                rect,
                Color.FromArgb(251, 255, 254),
                Color.FromArgb(232, 245, 241),
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, rect);

            using var glow = new SolidBrush(Color.FromArgb(34, ColorAccent));
            e.Graphics.FillEllipse(glow, rect.Width - 180, 6, 140, 140);
            e.Graphics.FillEllipse(glow, -24, -38, 110, 110);

            using var pen = new Pen(ColorBorder);
            e.Graphics.DrawLine(pen, 0, rect.Height - 1, rect.Width, rect.Height - 1);
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
                Bounds    = new Rectangle(14, y, 192, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0),
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
                Margin    = new Padding(6, 0, 0, 0),
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
            TextAlign = L.IsRtl ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight,
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

        private sealed class ModeItem
        {
            public ModeItem(string value) { Value = value; }
            public string Value { get; }
            public override string ToString()
                => Value == "sent" ? L.Str(StringKey.ModeSent) : L.Str(StringKey.ModeUnsent);
        }

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
            cb.Items.AddRange(new object[] { new ModeItem("unsent"), new ModeItem("sent") });
            cb.SelectedIndex = 0;
            return cb;
        }

        private sealed class LanguageItem
        {
            public LanguageItem(AppLanguage language) { Language = language; }
            public AppLanguage Language { get; }
            public override string ToString() => L.LanguageName(Language);
        }

        private ComboBox MakeLanguageCombo()
        {
            var cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Height = 36,
                Font = FontLabel,
                Margin = new Padding(0, 0, 8, 0),
                BackColor = ColorSidebar,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24
            };

            cb.Items.AddRange(new object[] { new LanguageItem(AppLanguage.English), new LanguageItem(AppLanguage.Arabic) });
            cb.SelectedItem = cb.Items.Cast<object>().FirstOrDefault(i => i is LanguageItem li && li.Language == L.Current);

            cb.DrawItem += (s, e) =>
            {
                e.DrawBackground();
                if (e.Index < 0) return;

                var itemText = cb.Items[e.Index]?.ToString() ?? string.Empty;
                var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                using var bg = new SolidBrush(isSelected ? ColorAccent : ColorSidebar);
                using var fg = new SolidBrush(isSelected ? Color.White : ColorSidebarText);
                e.Graphics.FillRectangle(bg, e.Bounds);
                e.Graphics.DrawString(itemText, cb.Font, fg, e.Bounds.Left + 6, e.Bounds.Top + 3);
                e.DrawFocusRectangle();
            };

            cb.SelectedIndexChanged += (s, e) =>
            {
                if (_applyingLanguage) return;
                if (cb.SelectedItem is not LanguageItem li) return;
                _backend.SetLanguage(li.Language);
                L.Set(li.Language);
                ApplyLanguageUi();
            };

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
                Width = 74,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorAccent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = ColorAccent;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ColorAccentHover;
            btn.Text = L.IsRtl ? "بحث" : "Search";
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

        private void StyleDataGrid(DataGridView grid)
        {
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor         = Color.White,
                ForeColor         = Color.FromArgb(30, 41, 59),
                SelectionBackColor= Color.FromArgb(238, 242, 255),
                SelectionForeColor= Color.FromArgb(30, 41, 59),
                Font              = new Font("Segoe UI", 8.5f),
                Padding           = new Padding(6, 4, 6, 4),
                Alignment         = L.IsRtl ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft
            };
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor          = Color.FromArgb(248, 250, 252),
                ForeColor          = Color.FromArgb(30, 41, 59),
                SelectionBackColor = Color.FromArgb(238, 242, 255),
                SelectionForeColor = Color.FromArgb(30, 41, 59),
                Font               = new Font("Segoe UI", 8.5f),
                Padding            = new Padding(6, 4, 6, 4),
                Alignment          = L.IsRtl ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Padding   = new Padding(6, 6, 6, 6),
                Alignment = L.IsRtl ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft
            };
            grid.ColumnHeadersHeight       = 38;
            grid.RowTemplate.Height        = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        private void ConfigureGridColumns(DataGridView grid)
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

            var room = L.IsRtl ? "الغرفة" : "Room";
            var desc = L.IsRtl ? "الوصف" : "Description";
            var rent = L.IsRtl ? "الإيجار" : "Rent";
            var date = L.IsRtl ? "التاريخ" : "Date";
            var amount = L.IsRtl ? "المبلغ" : "Amount";
            var type = L.IsRtl ? "نوع العملية" : "Transaction Type";
            var dr1 = L.IsRtl ? "حساب مدين 1" : "Debit Account 1";
            var dr2 = L.IsRtl ? "حساب مدين 2" : "Debit Account 2";
            var cr1 = L.IsRtl ? "حساب دائن 1" : "Credit Account 1";
            var cr2 = L.IsRtl ? "حساب دائن 2" : "Credit Account 2";
            var drcc = L.IsRtl ? "مركز تكلفة مدين" : "Dr Cost Center";
            var crcc = L.IsRtl ? "مركز تكلفة دائن" : "Cr Cost Center";
            var serial = L.IsRtl ? "مسلسل" : "Serial";

            Add("Room_no",         room,            nameof(Record.RoomNo),         70);
            Add("Descr1",          desc,            nameof(Record.Descr1),        180);
            Add("Rent_no",         rent,            nameof(Record.RentNo),         80);
            Add("Date",            date,            nameof(Record.Date),           100);
            Add("Amount",          amount,          nameof(Record.Amount),          90);
            Add("Type",            type,            nameof(Record.Type),            110);
            Add("DebitAccount1",   dr1,             nameof(Record.DebitAccount1),  150);
            Add("DebitAccount2",   dr2,             nameof(Record.DebitAccount2),  150);
            Add("CreditAccount1",  cr1,             nameof(Record.CreditAccount1), 150);
            Add("CreditAccount2",  cr2,             nameof(Record.CreditAccount2), 150);
            Add("DrcostCenterCode",drcc,            nameof(Record.DrCostCenterCode),150);
            Add("Crcostcentercode",crcc,            nameof(Record.CrCostCenterCode),150);
            Add("Ser",             serial,          nameof(Record.Ser),             90);

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

        private void ConfigureErrorGridColumns(DataGridView grid)
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

            var room = L.IsRtl ? "الغرفة" : "Room";
            var desc = L.IsRtl ? "الوصف" : "Description";
            var rent = L.IsRtl ? "الإيجار" : "Rent Number";
            var date = L.IsRtl ? "التاريخ" : "Date";
            var amount = L.IsRtl ? "المبلغ" : "Amount";
            var type = L.IsRtl ? "النوع" : "Type";
            var dr1 = L.IsRtl ? "مدين 1" : "Debit Account 1";
            var dr2 = L.IsRtl ? "مدين 2" : "Debit Account 2";
            var cr1 = L.IsRtl ? "دائن 1" : "Credit Account 1";
            var cr2 = L.IsRtl ? "دائن 2" : "Credit Account 2";
            var drcc = L.IsRtl ? "مركز تكلفة مدين" : "Dr Cost Center";
            var crcc = L.IsRtl ? "مركز تكلفة دائن" : "Cr Cost Center";
            var serial = L.IsRtl ? "مسلسل" : "Serial";
            var err = L.IsRtl ? "الخطأ" : "Error";
            var failedAt = L.IsRtl ? "وقت الفشل" : "Failed At";

            Add("Room_no",  room,      nameof(ErrorRecord.RoomNo), 70);
            Add("Descr1",   desc,      nameof(ErrorRecord.Descr1), 180);
            Add("Rent_no",  rent,      nameof(ErrorRecord.RentNo), 80);
            Add("Date_OF_DB", date,    nameof(ErrorRecord.DateOfDb), 120);
            Add("Amount",   amount,    nameof(ErrorRecord.Amount), 90);
            Add("Type",     type,      nameof(ErrorRecord.Type), 70);
            Add("DebitAccount1",  dr1, nameof(ErrorRecord.DebitAccount1), 110);
            Add("DebitAccount2",  dr2, nameof(ErrorRecord.DebitAccount2), 110);
            Add("CreditAccount1", cr1, nameof(ErrorRecord.CreditAccount1), 110);
            Add("CreditAccount2", cr2, nameof(ErrorRecord.CreditAccount2), 110);
            Add("DrcostCenter",   drcc, nameof(ErrorRecord.DrCostCenter), 100);
            Add("CrcostCenter",   crcc, nameof(ErrorRecord.CrCostCenter), 100);
            Add("Ser",      serial,    nameof(ErrorRecord.Ser), 70);
            Add("ERORR",    err,       nameof(ErrorRecord.Error), 280);
            Add("failed_requests_timestamp", failedAt, nameof(ErrorRecord.FailedRequestsTimestamp), 140);

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        }

        private void UpdateHeroContent()
        {
            if (_heroTitleLabel != null)
                _heroTitleLabel.Text = _page == "errors"
                    ? (L.IsRtl ? "مراجعة سجل الأخطاء" : "Review Error Activity")
                    : (L.IsRtl ? "مزامنة قيود الإيجار بسهولة" : "Move Rental Journals With Confidence");

            if (_heroSubtitleLabel != null)
                _heroSubtitleLabel.Text = _page == "errors"
                    ? (L.IsRtl ? "ابحث في الطلبات الفاشلة وتتبع أسبابها بسرعة." : "Search failed requests, inspect details, and track issues without leaving the dashboard.")
                    : (L.IsRtl ? "راقب السجلات غير المرسلة وأرسلها ضمن واجهة أوضح وأسرع." : "Review unsent records, manage date ranges, and push imports through a cleaner operational workspace.");

            if (_heroBadgeLabel != null)
                _heroBadgeLabel.Text = _page == "errors"
                    ? (L.IsRtl ? "وضع الأخطاء" : "Errors View")
                    : (L.IsRtl ? "جاهز للإرسال" : "Ready To Send");
        }

        private void ApplyLanguageUi()
        {
            _applyingLanguage = true;
            try
            {
                // Hide summary bar when switching language
                HideResultCard();

                this.RightToLeft = L.IsRtl ? RightToLeft.Yes : RightToLeft.No;
                this.RightToLeftLayout = L.IsRtl;

                this.Text = L.Str(StringKey.AppTitle);

                if (_logoLabel != null) _logoLabel.Text = L.Str(StringKey.AppTitle);
                if (_logoSubLabel != null) _logoSubLabel.Text = L.Str(StringKey.SidebarSubtitle);
                if (_versionLabel != null) _versionLabel.Text = L.Str(StringKey.VersionLabel);

                if (_homeSideBtn != null) _homeSideBtn.Text = L.Str(StringKey.SidebarHome);
                if (_errorsSideBtn != null) _errorsSideBtn.Text = L.Str(StringKey.SidebarErrors);
                if (_settingsSideBtn != null) _settingsSideBtn.Text = L.Str(StringKey.SidebarSettings);

                var filterLabelAlign = L.IsRtl ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                if (_fromLabel != null) _fromLabel.Text = L.Str(StringKey.From);
                if (_toLabel != null) _toLabel.Text = L.Str(StringKey.To);
                if (_fromLabel != null) _fromLabel.TextAlign = filterLabelAlign;
                if (_toLabel != null) _toLabel.TextAlign = filterLabelAlign;

                if (_modeLabel != null)
                {
                    _modeLabel.Text = L.Str(StringKey.Status);
                    _modeLabel.TextAlign = filterLabelAlign;
                }
                UpdateHeroContent();
                if (_fetchBtn != null) _fetchBtn.Text = _page == "errors" ? L.Str(StringKey.FetchErrors) : L.Str(StringKey.Fetch);
                if (_sendBtn != null) _sendBtn.Text = L.Str(StringKey.SendRange);
                if (_errorSearchBtn != null) _errorSearchBtn.Text = L.IsRtl ? "بحث" : "Search";

                if (_countLabel != null)
                {
                    _countLabel.TextAlign = L.AlignNearMiddle();
                    if (string.IsNullOrWhiteSpace(_countLabel.Text)) _countLabel.Text = L.Str(StringKey.NoRecordsLoaded);
                }

                if (_sendResultLabel != null) _sendResultLabel.TextAlign = L.AlignFarMiddle();
                if (_statusLabel != null)
                {
                    _statusLabel.TextAlign = L.AlignNearMiddle();
                    if (string.IsNullOrWhiteSpace(_statusLabel.Text)) _statusLabel.Text = L.Str(StringKey.Ready);
                }

                if (_errorSearchBox != null) SetCueBanner(_errorSearchBox, L.Str(StringKey.SearchCue));

                if (_langBox != null)
                {
                    var item = _langBox.Items.Cast<object>().FirstOrDefault(i => i is LanguageItem li && li.Language == L.Current);
                    if (item != null) _langBox.SelectedItem = item;
                }

                if (_dataGrid != null)
                {
                    StyleDataGrid(_dataGrid);
                    if (_page == "errors") ConfigureErrorGridColumns(_dataGrid);
                    else ConfigureGridColumns(_dataGrid);
                    _dataGrid.Refresh();
                }
            }
            finally
            {
                _applyingLanguage = false;
            }
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
                SetStatus(L.Str(StringKey.FetchingRecords), ColorSidebarText);
                ShowLoading(L.Str(StringKey.FetchingRecordsShort));
                ToggleUi(false);
                var from  = _fromPicker!.Value.Date;
                var to    = _toPicker!.Value.Date;
                var mode  = (_modeBox?.SelectedItem as ModeItem)?.Value ?? "unsent";
                ConfigureJournalGrid();
                var rows  = await Task.Run(() => _backend.FetchRecords(from, to, mode));
                _dataGrid!.DataSource = rows;
                int total = await Task.Run(() => _backend.CountRecords(from, to, mode));
                _countLabel!.Text = FormatRecordsSummary(rows.Count, total, from, to, mode, includeDates: true);
                if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
                SetStatus(L.Format(StringKey.FetchedRecords, rows.Count), ColorSuccess);
            }
            catch (Exception ex)
            {
                SetStatus(L.Str(StringKey.FetchFailed), ColorDanger);
                ShowError(L.Str(StringKey.ErrorFetchingTitle), ex.Message);
            }
            finally { HideLoading(); ToggleUi(true); }
        }

        private async Task OnErrors()
        {
            try
            {
                SetStatus(L.Str(StringKey.LoadingErrors), ColorSidebarText);
                ShowLoading(L.Str(StringKey.LoadingErrorsShort));
                ToggleUi(false);
                var from = _fromPicker!.Value.Date;
                var to = _toPicker!.Value.Date;

                ConfigureErrorGrid();
                var rows = await Task.Run(() => _backend.FetchErrorRecords(from, to));
                _dataGrid!.DataSource = rows;
                int total = await Task.Run(() => _backend.CountErrorRecords(from, to));
                _countLabel!.Text = FormatErrorsSummary(rows.Count, total, from, to);
                if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
                SetStatus(L.Format(StringKey.LoadedErrors, rows.Count), ColorSuccess);
            }
            catch (System.Exception ex)
            {
                SetStatus(L.Str(StringKey.LoadErrorsFailed), ColorDanger);
                ShowError(L.Str(StringKey.ErrorLoadingErrorsTitle), ex.Message);
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
                SetStatus(L.Str(StringKey.SearchingErrors), ColorSidebarText);
                ShowLoading(L.Str(StringKey.SearchingErrorsShort));
                ToggleUi(false);
                var from = _fromPicker!.Value.Date;
                var to = _toPicker!.Value.Date;

                ConfigureErrorGrid();
                var rows = await Task.Run(() => _backend.SearchErrorRecords(from, to, serial));
                _dataGrid!.DataSource = rows;
                int total = await Task.Run(() => _backend.CountErrorRecordsSearch(from, to, serial));
                _countLabel!.Text = FormatErrorSearchSummary(serial, rows.Count, total, from, to);
                if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
                SetStatus(L.Format(StringKey.FoundErrors, rows.Count), ColorSuccess);
            }
            catch (System.Exception ex)
            {
                SetStatus(L.Str(StringKey.SearchErrorsFailed), ColorDanger);
                ShowError(L.Str(StringKey.ErrorSearchingErrorsTitle), ex.Message);
            }
            finally { HideLoading(); ToggleUi(true); }
        }

        private async Task OnSend()
        {
            var confirm = MessageBox.Show(
                L.Str(StringKey.ConfirmSendBody),
                L.Str(StringKey.ConfirmSendTitle),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                SetStatus(L.Str(StringKey.SendingRecords), ColorSidebarText);
                ShowLoadingProgress(L.Str(StringKey.SendingRecordsShort));
                ToggleUi(false);
                var from = _fromPicker!.Value.Date;
                var to   = _toPicker!.Value.Date;

                SelectMode("unsent");
                var rows = await Task.Run(() => _backend.FetchRecords(from, to, "unsent"));
                _dataGrid!.DataSource = rows;

                var progress = new Progress<int>(p => SetLoadingProgress(p));
                var result = await Task.Run(() => _backend.SendRange(from, to, progress));

                // Show inline card instead of MessageBox
                ShowResultCard(result);

                SetStatus(result.IsOk ? L.Str(StringKey.SendCompleted) : L.Str(StringKey.SendCompletedWithErrors),
                        result.IsOk ? ColorSuccess : ColorDanger);
            }
            catch (Exception ex)
            {
                SetStatus(L.Str(StringKey.SendFailed), ColorDanger);
                ShowError(L.Str(StringKey.ErrorSendingRangeTitle), ex.Message);
            }
            finally { HideLoading(); ToggleUi(true); }
        }

        private void ShowResultCard(BackendService.SendRangeResult r)
        {
            if (_resultCard == null || _resultCardText == null) return;

            // Requested UI enhancement: show the summary in yellow and visually segment sections.
            var color = Color.FromArgb(120, 53, 15);

            const string Arrow = "→";
            const string Ok = "✔";
            const string Fail = "✖";
            const string Skip = "⟲";
            const string Warn = "⚠";
            const string Wall = "   │   ";

            var icon = !r.IsOk || r.HasWarnings ? Warn : Ok;

            var statementLabel = L.Str(StringKey.ResultStatementLabel);
            var glLabel        = L.Str(StringKey.ResultGlLabel);
            var totalLabel     = L.Str(StringKey.ResultTotalLabel);
            var skippedLabel   = L.Str(StringKey.ResultSkippedLabel);

            _resultCardText.Text =
                $"{icon}  {statementLabel} {Arrow} {Ok} {r.SuccessStatement}   {Fail} {r.FailStatement}   {Skip} {r.SkipStatement} {skippedLabel}" +
                Wall +
                $"{glLabel} {Arrow} {Ok} {r.SuccessGl}   {Fail} {r.FailGl}   {Skip} {r.SkipGl} {skippedLabel}" +
                Wall +
                $"{totalLabel} {Arrow} {Ok} {r.TotalSuccess}   {Fail} {r.TotalFail}   {Skip} {r.TotalSkip} {skippedLabel}" +
                (r.ErrorMessage != null ? $"     {Warn} {r.ErrorMessage}" : "");

            _resultCard.BackColor = Color.FromArgb(254, 243, 199);

            _resultCardText.ForeColor = color;
            _resultCard.Height = 42;
            _resultCard.BringToFront();
        }

        private void HideResultCard()
        {
            if (_resultCard == null || _resultCardText == null) return;
            _resultCardText.Text = string.Empty;
            _resultCard.Height = 0;
        }

        private static string FormatSendResultForUi(string result)
        {
            if (string.IsNullOrWhiteSpace(result)) return string.Empty;
            var singleLine = result.Replace("\r", " ").Replace("\n", " ").Trim();
            const int max = 90;
            return singleLine.Length <= max ? singleLine : singleLine.Substring(0, max) + "...";
        }

        private void SelectMode(string value)
        {
            if (_modeBox == null) return;
            var item = _modeBox.Items.Cast<object>().FirstOrDefault(i => i is ModeItem mi && mi.Value == value);
            if (item != null) _modeBox.SelectedItem = item;
        }

        private static string ModeText(string modeValue)
            => modeValue == "sent" ? L.Str(StringKey.ModeSent) : L.Str(StringKey.ModeUnsent);

        private static string FormatRecordsSummary(int shown, int total, DateTime from, DateTime to, string modeValue, bool includeDates)
        {
            var modeText = ModeText(modeValue);

            if (!includeDates)
            {
                return L.IsRtl
                    ? $"عرض {shown} من {total} سجل   •   الحالة: {modeText}"
                    : $"Showing {shown} of {total} records   •   Mode: {modeText}";
            }

            return L.IsRtl
                ? $"عرض {shown} من {total} سجل   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}   •   الحالة: {modeText}"
                : $"Showing {shown} of {total} records   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}   •   Mode: {modeText}";
        }

        private static string FormatErrorsSummary(int shown, int total, DateTime from, DateTime to)
        {
            return L.IsRtl
                ? $"عرض {shown} من {total} خطأ   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}"
                : $"Showing {shown} of {total} errors   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}";
        }

        private static string FormatErrorSearchSummary(string query, int shown, int total, DateTime from, DateTime to)
        {
            return L.IsRtl
                ? $"نتائج البحث عن ({query}): {shown} من {total}   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}"
                : $"Search Results for ({query}): {shown} of {total}   •   {from:dd MMM yyyy} → {to:dd MMM yyyy}";
        }

        private void OnSettings()
        {
            if (!PromptForSettingsPassword()) return;
            using var dlg = new Form
            {
                Width           = 540,
                Height          = 380,
                Text            = L.Str(StringKey.SettingsTitle),
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

            dlg.Controls.Add(MkLbl(L.Str(StringKey.MainDbPathLabel))); var txtDb = MkTxt(_backend.DatabasePath); txtDb.Width = 400; dlg.Controls.Add(txtDb);
            var browseDb = new Button { Text = L.Str(StringKey.Browse), Left = 428, Top = y + 16, Width = 86, Height = 26, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = FontLabel };
            browseDb.FlatAppearance.BorderColor = ColorBorder;
            browseDb.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "Access DB|*.accdb;*.mdb", Title = L.Str(StringKey.SelectMainDbTitle) };
                if (ofd.ShowDialog(dlg) == DialogResult.OK) txtDb.Text = ofd.FileName;
            };
            dlg.Controls.Add(browseDb);
            y += 56;

            dlg.Controls.Add(MkLbl(L.Str(StringKey.LoginUrlLabel))); var txtLogin = MkTxt(_backend.LoginUrl); dlg.Controls.Add(txtLogin); y += 56;
            dlg.Controls.Add(MkLbl(L.Str(StringKey.ImportUrlLabel))); var txtImport = MkTxt(_backend.ImportUrl); dlg.Controls.Add(txtImport); y += 56;
            dlg.Controls.Add(MkLbl(L.Str(StringKey.ErrorDbPathLabel))); var txtError = MkTxt(_backend.ErrorDbPath); txtError.Width = 400; dlg.Controls.Add(txtError);

            var browse = new Button { Text = L.Str(StringKey.Browse), Left = 428, Top = y + 16, Width = 86, Height = 26, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = FontLabel };
            browse.FlatAppearance.BorderColor = ColorBorder;
            browse.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "Access DB|*.accdb;*.mdb", Title = L.Str(StringKey.SelectErrorDbTitle) };
                if (ofd.ShowDialog(dlg) == DialogResult.OK) txtError.Text = ofd.FileName;
            };
            dlg.Controls.Add(browse);

            var ok = new Button { Text = L.Str(StringKey.SaveSettings), Left = 320, Top = 250, Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = ColorAccent, ForeColor = Color.White, Font = FontBold };
            ok.FlatAppearance.BorderSize = 0;
            var cancel = new Button { Text = L.Str(StringKey.Cancel), Left = 440, Top = 250, Width = 74, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = FontLabel };
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
                SetStatus(L.Str(StringKey.SettingsSaved), ColorSuccess);
            }
        }

        private static bool PromptForSettingsPassword()
        {
            var required = Obfuscation.GetSettingsPassword();

            while (true)
            {
                using var dlg = new Form
                {
                    Width = 360,
                    Height = 170,
                    Text = L.Str(StringKey.EnterPasswordTitle),
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
                    Text = L.Str(StringKey.PasswordRequiredBody),
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
                var cancel = new Button { Text = L.Str(StringKey.Cancel), Left = 256, Top = 85, Width = 72, Height = 28 };
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

                MessageBox.Show(L.Str(StringKey.WrongPasswordBody), L.Str(StringKey.IncorrectPasswordTitle),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        
        private void BuildResultCard(Panel parent)
        {
            _resultCard = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 0,
                BackColor = Color.FromArgb(240, 253, 244),
                Padding   = new Padding(20, 0, 20, 0)
            };
            _resultCard.Paint += (s, e) =>
            {
                using var pen = new Pen(ColorAccent);
                e.Graphics.DrawLine(pen, 0, 0, ((Panel)s!).Width, 0);
            };

            _resultCardText = new Label
            {
                Dock      = DockStyle.Fill,
                ForeColor = ColorText,
                Font      = FontLabel,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _resultCard.Controls.Add(_resultCardText);
            parent.Controls.Add(_resultCard);
        }
        
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

        private void ShowLoadingProgress(string message)
        {
            if (_loadingOverlay == null) return;
            if (InvokeRequired) { Invoke(() => ShowLoadingProgress(message)); return; }
            _loadingOverlay.ShowProgress(message);
        }

        private void SetLoadingProgress(int percent)
        {
            if (_loadingOverlay == null) return;
            if (InvokeRequired) { Invoke(() => SetLoadingProgress(percent)); return; }
            _loadingOverlay.SetProgress(percent);
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
                // Hide summary bar when navigating between pages
                HideResultCard();
                _lastAppliedPage = _page;
            }

            if (_sendBtn != null) _sendBtn.Visible = !isErrors;
            if (_modeBox != null) _modeBox.Visible = !isErrors;
            if (_modeLabel != null) _modeLabel.Visible = !isErrors;
            if (_errorSearchRow != null) _errorSearchRow.Visible = isErrors;

            if (_fetchBtn != null) _fetchBtn.Text = isErrors ? L.Str(StringKey.FetchErrors) : L.Str(StringKey.Fetch);
            UpdateHeroContent();

            if (_dataGrid != null)
            {
                if (isErrors) ConfigureErrorGridColumns(_dataGrid);
                else ConfigureGridColumns(_dataGrid);

                // Keep errors grid horizontally scrollable in a predictable (LTR) direction
                // so users can reach right-most columns like the timestamp.
                if (isErrors) _dataGrid.RightToLeft = RightToLeft.No;
            }

            SetSidebarActive(_homeSideBtn, !isErrors);
            SetSidebarActive(_errorsSideBtn, isErrors);

            if (_sendResultLabel != null) _sendResultLabel.Text = string.Empty;
            SetStatus(isErrors ? L.Str(StringKey.ErrorsView) : L.Str(StringKey.Ready), ColorSidebarText);
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
