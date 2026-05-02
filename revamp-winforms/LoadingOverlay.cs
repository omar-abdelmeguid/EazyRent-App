using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EazyRentRevamp
{
    internal sealed class LoadingOverlay : Panel
    {
        private readonly System.Windows.Forms.Timer _timer;
        private int _angle;
        private string _message = "Loading...";

        public LoadingOverlay()
        {
            Dock = DockStyle.Fill;
            Visible = false;
            BackColor = Color.FromArgb(110, 15, 23, 42); // translucent dark
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += (s, e) =>
            {
                _angle = (_angle + 6) % 360;
                Invalidate();
            };
        }

        public void Show(string message)
        {
            _message = string.IsNullOrWhiteSpace(message) ? "Loading..." : message.Trim();
            Visible = true;
            BringToFront();
            _timer.Start();
        }

        public void HideOverlay()
        {
            _timer.Stop();
            Visible = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int cardW = 360;
            const int cardH = 140;
            var cardX = (Width - cardW) / 2;
            var cardY = (Height - cardH) / 2;
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            {
                var shadowRect = cardRect;
                shadowRect.Offset(0, 6);
                FillRoundedRect(g, shadowRect, 16, shadow);
            }

            using (var cardBrush = new SolidBrush(Color.White))
            using (var border = new Pen(Color.FromArgb(226, 232, 240)))
            {
                FillRoundedRect(g, cardRect, 16, cardBrush);
                DrawRoundedRect(g, cardRect, 16, border);
            }

            // Spinner
            var spinnerCenter = new Point(cardRect.Left + 42, cardRect.Top + cardRect.Height / 2);
            const int r = 16;
            using (var track = new Pen(Color.FromArgb(226, 232, 240), 4))
            using (var arc = new Pen(Color.FromArgb(99, 102, 241), 4))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                arc.StartCap = LineCap.Round;
                arc.EndCap = LineCap.Round;

                var circle = new Rectangle(spinnerCenter.X - r, spinnerCenter.Y - r, r * 2, r * 2);
                g.DrawArc(track, circle, 0, 360);
                g.DrawArc(arc, circle, _angle, 110);
            }

            // Message
            using (var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var bodyFont = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
            using (var bodyBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                var titleX = cardRect.Left + 78;
                var titleY = cardRect.Top + 36;
                g.DrawString(_message, titleFont, titleBrush, titleX, titleY);
                g.DrawString("Please wait…", bodyFont, bodyBrush, titleX, titleY + 28);
            }
        }

        private static void FillRoundedRect(Graphics g, Rectangle rect, int radius, Brush brush)
        {
            using var path = RoundedRect(rect, radius);
            g.FillPath(brush, path);
        }

        private static void DrawRoundedRect(Graphics g, Rectangle rect, int radius, Pen pen)
        {
            using var path = RoundedRect(rect, radius);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
