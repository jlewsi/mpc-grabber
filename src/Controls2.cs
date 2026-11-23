using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MpcGrabber
{
    internal class LoadingOverlay : DcControl
    {
        private readonly Timer _timer;
        private float _angle;
        private string _message = "Loading\u2026";

        public LoadingOverlay()
        {
            BackColor = T.Bg;
            Visible = false;
            _timer = new Timer();
            _timer.Interval = 33;
            _timer.Tick += delegate { _angle = (_angle + 7f) % 360f; Invalidate(); };
        }

        public void ShowOverlay(string message)
        {
            _message = message;
            Visible = true;
            BringToFront();
            _timer.Start();
            Invalidate();
        }

        public void HideOverlay()
        {
            _timer.Stop();
            Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(T.Bg)) g.FillRectangle(b, ClientRectangle);

            int cx = Width / 2;
            int cy = Height / 2 - 24;
            int r = 52;

            using (Pen glow = new Pen(Color.FromArgb(40, T.Accent), 18f))
            {
                glow.StartCap = LineCap.Round;
                glow.EndCap = LineCap.Round;
                g.DrawArc(glow, cx - r + 9, cy - r + 9, (r - 9) * 2, (r - 9) * 2, _angle * 0.6f, 70f);
            }
            using (Pen track = new Pen(T.Border, 7f))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                g.DrawEllipse(track, cx - r, cy - r, r * 2, r * 2);
            }
            using (Pen arc = new Pen(T.Accent, 7f))
            {
                arc.StartCap = LineCap.Round;
                arc.EndCap = LineCap.Round;
                g.DrawArc(arc, cx - r, cy - r, r * 2, r * 2, _angle, 100f);
            }
            using (Pen arc2 = new Pen(Color.FromArgb(110, T.Accent), 4f))
            {
                arc2.StartCap = LineCap.Round;
                arc2.EndCap = LineCap.Round;
                g.DrawArc(arc2, cx - r + 14, cy - r + 14, (r - 14) * 2, (r - 14) * 2, -_angle * 1.7f, 60f);
            }

            using (SolidBrush dot = new SolidBrush(T.Accent))
                g.FillEllipse(dot, cx - 4, cy - 4, 8, 8);

            T.Draw(g, _message, T.H3, T.Text, new Rectangle(0, cy + r + 16, Width, 26),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            T.Draw(g, "reading from yt-dlp", T.Small, T.Faint, new Rectangle(0, cy + r + 42, Width, 20),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal class ToggleBox : DcControl
    {
        private bool _checked;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); } }
        }

        public ToggleBox() { BackColor = T.Card; Cursor = Cursors.Hand; }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _checked = !_checked;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle box = new Rectangle(0, (Height - 18) / 2, 18, 18);
            T.FillRound(g, _checked ? T.Accent : T.Card2, box, 4);
            if (!_checked) T.StrokeRound(g, T.Border, box, 4, 1f);
            else T.Draw(g, "\uE73E", T.GlyphSm, T.OnAccent, box, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            T.Draw(g, Text, T.Body, T.Text, new Rectangle(28, 0, Width - 30, Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class DownloadButton : DcControl
    {
        private bool _hover, _down;
        private int _count;

        public string Label = "Download";
        public int Count
        {
            get { return _count; }
            set { if (_count != value) { _count = value; Invalidate(); } }
        }

        public DownloadButton()
        {
            Cursor = Cursors.Hand;
            BackColor = T.Bg;
            Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color fill = _down ? Color.FromArgb(18, 38, 22) : (_hover ? Color.FromArgb(12, 28, 16) : T.Bg);
            using (GraphicsPath p = T.Round(r, 10))
            using (SolidBrush b = new SolidBrush(fill))
                g.FillPath(b, p);
            T.StrokeRound(g, Enabled ? (_hover ? T.AccentHi : T.Accent) : T.Faint, r, 10, 2f);

            Color labelColor = Enabled ? T.Accent : T.Faint;
            Color countColor = Enabled ? T.Danger : T.Faint;
            string count = Count > 0 ? " " + Count : "";
            Size ls = T.Measure(Label, Font);
            Size cs = count.Length > 0 ? T.Measure(count, Font) : Size.Empty;
            int total = ls.Width + cs.Width;
            int x = (Width - total) / 2;
            int cy = Height / 2;

            T.Draw(g, Label, Font, labelColor, new Rectangle(x, cy - 14, ls.Width + 4, 28),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (count.Length > 0)
                T.Draw(g, count, Font, countColor, new Rectangle(x + ls.Width, cy - 14, cs.Width + 6, 28),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}
