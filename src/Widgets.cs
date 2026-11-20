using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MpcGrabber
{
    internal class DcControl : Control
    {
        public DcControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = T.Bg;
        }
    }

    internal class RoundedPanel : DcControl
    {
        public int Radius = 12;
        public Color Fill = T.Card;
        public Color BorderColor = T.Border;
        public float BorderWidth = 1f;
        public bool ShowBorder = true;

        public RoundedPanel()
        {
            BackColor = T.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);
            T.FillRound(g, Fill, r, Radius);
            if (ShowBorder) T.StrokeRound(g, BorderColor, r, Radius, BorderWidth);
        }
    }

    public enum BtnVariant { Primary, Ghost, Soft, Danger, IconGhost }

    internal class FlatButton : DcControl
    {
        public BtnVariant Variant = BtnVariant.Ghost;
        public string Glyph = null;
        public int Radius = 10;
        public bool LeftAlign = false;
        public float GlyphSize = 10.5f;

        private bool _hover, _down;

        public FlatButton()
        {
            Cursor = Cursors.Hand;
            Font = T.BodyB;
            BackColor = T.Card;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);

            Color bg; Color fg; bool border = false;
            switch (Variant)
            {
                case BtnVariant.Primary:
                    bg = !Enabled ? T.Border : (_down ? T.AccentLo : (_hover ? T.AccentHi : T.Accent));
                    fg = !Enabled ? T.Faint : Color.White;
                    break;
                case BtnVariant.Soft:
                    bg = !Enabled ? T.Card2 : (_down ? T.Press : (_hover ? T.Hover : T.Card2));
                    fg = !Enabled ? T.Faint : T.Text;
                    border = true;
                    break;
                case BtnVariant.Danger:
                    bg = _down ? Color.FromArgb(40, 20, 24) : (_hover ? Color.FromArgb(38, 18, 22) : Color.Transparent);
                    fg = !Enabled ? T.Faint : T.Danger;
                    break;
                case BtnVariant.IconGhost:
                    bg = _down ? T.Press : (_hover ? T.Hover : Color.Transparent);
                    fg = !Enabled ? T.Faint : T.Dim;
                    break;
                default: // Ghost
                    bg = _down ? T.Press : (_hover ? T.Hover : Color.Transparent);
                    fg = !Enabled ? T.Faint : T.Text;
                    border = true;
                    break;
            }

            if (bg.A > 0) T.FillRound(g, bg, r, Radius);
            if (border) T.StrokeRound(g, _hover && Enabled ? T.Border : T.BorderSoft, r, Radius, 1f);

            Font gf = new Font("Segoe MDL2 Assets", GlyphSize);
            string text = Text == null ? "" : Text;
            Size ts = text.Length > 0 ? T.Measure(text, Font) : Size.Empty;
            int gap = (Glyph != null && text.Length > 0) ? 8 : 0;
            int gw = Glyph != null ? (int)Math.Ceiling(T.Measure(Glyph, gf).Width * 0.9) : 0;
            int total = gw + gap + ts.Width;

            int x = LeftAlign ? 16 : (Width - total) / 2;
            int cy = Height / 2;

            if (Glyph != null)
            {
                Rectangle gr = new Rectangle(x, cy - 11, gw + 4, 22);
                T.Draw(g, Glyph, gf, fg, gr, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                x += gw + gap;
            }
            if (text.Length > 0)
            {
                int avail = Math.Max(10, Width - x - 8);
                Rectangle tr = new Rectangle(x, cy - 11, Math.Min(ts.Width + 2, avail), 22);
                T.Draw(g, text, Font, fg, tr, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            gf.Dispose();
        }
    }

    internal class Pill : DcControl
    {
        public Color DotColor = T.Success;
        public Color TextColor = T.Dim;
        public Color BgColor = T.Card2;
        public bool ShowDot = true;

        public Pill()
        {
            Font = T.Small;
            BackColor = T.Card;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);
            T.FillRound(g, BgColor, r, Height / 2);
            string text = Text ?? "";
            int x = 12;
            if (ShowDot)
            {
                using (SolidBrush b = new SolidBrush(DotColor))
                    g.FillEllipse(b, x, Height / 2 - 3, 6, 6);
                x += 12;
            }
            Size ts = T.Measure(text, Font);
            T.Draw(g, text, Font, TextColor, new Rectangle(x, 0, Math.Max(ts.Width + 4, 8), Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    internal class ProgressBarEx : DcControl
    {
        private double _value;
        public double Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(1, value)); Invalidate(); }
        }
        public Color BarColor = T.Accent;

        public ProgressBarEx() { BackColor = T.Card; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int h = 6;
            Rectangle track = new Rectangle(0, (Height - h) / 2, Width, h);
            T.FillRound(g, T.BorderSoft, track, 3);
            int w = (int)(Width * _value);
            if (w > 0)
            {
                Rectangle fill = new Rectangle(0, (Height - h) / 2, Math.Max(w, 6), h);
                using (GraphicsPath p = T.Round(fill, 3))
                using (LinearGradientBrush lb = new LinearGradientBrush(fill, BarColor, T.Mix(BarColor, Color.White, 0.25), LinearGradientMode.Horizontal))
                    g.FillPath(lb, p);
            }
        }
    }

    internal class ScrollList : DcControl
    {
        protected int ContentHeight = 0;
        protected int ScrollPos = 0;
        protected bool ShowBar = true;
        private bool _dragging;
        private int _dragY;
        private int _dragScroll;

        public ScrollList() { BackColor = T.Card; }

        protected int MaxScroll { get { return Math.Max(0, ContentHeight - Height); } }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (MaxScroll > 0)
            {
                ScrollPos = Math.Max(0, Math.Min(MaxScroll, ScrollPos - e.Delta / 120 * 64));
                Invalidate();
            }
            base.OnMouseWheel(e);
        }

        protected Rectangle ThumbRect()
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            int trackH = Height - 8;
            int thumbH = Math.Max(28, (int)(trackH * (double)Height / ContentHeight));
            int y = 4 + (int)((trackH - thumbH) * (double)ScrollPos / MaxScroll);
            return new Rectangle(Width - 7, y, 4, thumbH);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Rectangle th = ThumbRect();
            if (th != Rectangle.Empty && e.X >= Width - 12)
            {
                _dragging = true;
                _dragY = e.Y;
                _dragScroll = ScrollPos;
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                int trackH = Height - 8;
                int thumbH = Math.Max(28, (int)(trackH * (double)Height / ContentHeight));
                int travel = Math.Max(1, trackH - thumbH);
                ScrollPos = Math.Max(0, Math.Min(MaxScroll, _dragScroll + (int)((e.Y - _dragY) * (double)MaxScroll / travel)));
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            Capture = false;
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintContent(g, ScrollPos);
            if (ShowBar && MaxScroll > 0)
            {
                Rectangle th = ThumbRect();
                if (th != Rectangle.Empty)
                {
                    using (GraphicsPath p = T.Round(th, 2))
                    using (SolidBrush b = new SolidBrush(T.Border))
                        g.FillPath(b, p);
                }
            }
        }

        protected virtual void PaintContent(Graphics g, int offset) { }
    }

    internal class NavItem
    {
        public string Glyph;
        public string Label;
        public object Tag;
        public NavItem(string glyph, string label) { Glyph = glyph; Label = label; }
    }

    internal class NavBar : DcControl
    {
        public List<NavItem> Items = new List<NavItem>();
        public int SelectedIndex = 0;
        public event EventHandler SelectionChanged;
        private int _hover = -1;

        public NavBar() { BackColor = T.Sidebar; }

        private Rectangle ItemRect(int i) { return new Rectangle(10, 10 + i * 46, Width - 20, 40); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < Items.Count; i++) if (ItemRect(i).Contains(e.Location)) h = i;
            if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (ItemRect(i).Contains(e.Location))
                {
                    SelectedIndex = i;
                    Invalidate();
                    if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                    break;
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < Items.Count; i++)
            {
                Rectangle r = ItemRect(i);
                bool sel = i == SelectedIndex;
                bool hov = i == _hover;
                if (sel) T.FillRound(g, T.AccentTint, r, 9);
                else if (hov) T.FillRound(g, T.Hover, r, 9);

                Color fg = sel ? T.Text : (hov ? T.Text : T.Dim);
                if (sel)
                {
                    using (GraphicsPath p = T.Round(new Rectangle(r.X, r.Y + 11, 3, 18), 2))
                    using (SolidBrush b = new SolidBrush(T.Accent))
                        g.FillPath(b, p);
                }
                T.Draw(g, Items[i].Glyph, T.Glyph, sel ? T.Accent : T.Faint,
                    new Rectangle(r.X + 16, r.Y, 24, r.Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                T.Draw(g, Items[i].Label, sel ? T.BodyB : T.Body, fg,
                    new Rectangle(r.X + 44, r.Y, r.Width - 50, r.Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }
    }

    internal class Divider : DcControl
    {
        public Divider() { Height = 1; BackColor = T.Bg; }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (Pen p = new Pen(T.BorderSoft))
                e.Graphics.DrawLine(p, 0, 0, Width, 0);
        }
    }
}
