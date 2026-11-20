using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MpcGrabber
{
    internal static class T
    {
        public static readonly Color Bg = Color.FromArgb(14, 16, 20);
        public static readonly Color Sidebar = Color.FromArgb(10, 12, 16);
        public static readonly Color Card = Color.FromArgb(22, 26, 33);
        public static readonly Color Card2 = Color.FromArgb(18, 21, 27);
        public static readonly Color Hover = Color.FromArgb(27, 32, 41);
        public static readonly Color Press = Color.FromArgb(32, 38, 49);
        public static readonly Color Border = Color.FromArgb(36, 42, 55);
        public static readonly Color BorderSoft = Color.FromArgb(28, 34, 46);
        public static readonly Color Text = Color.FromArgb(233, 236, 242);
        public static readonly Color Dim = Color.FromArgb(154, 163, 181);
        public static readonly Color Faint = Color.FromArgb(106, 115, 134);
        public static readonly Color Accent = Color.FromArgb(91, 124, 250);
        public static readonly Color AccentHi = Color.FromArgb(114, 143, 255);
        public static readonly Color AccentLo = Color.FromArgb(72, 100, 220);
        public static readonly Color AccentTint = Color.FromArgb(28, 38, 68);
        public static readonly Color Success = Color.FromArgb(53, 208, 127);
        public static readonly Color Danger = Color.FromArgb(242, 85, 90);
        public static readonly Color Warn = Color.FromArgb(240, 169, 59);

        public static readonly Font H1 = new Font("Segoe UI", 20f, FontStyle.Bold);
        public static readonly Font H2 = new Font("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font H3 = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        public static readonly Font Body = new Font("Segoe UI", 9.75f);
        public static readonly Font BodyB = new Font("Segoe UI", 9.75f, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 8.75f);
        public static readonly Font SmallB = new Font("Segoe UI", 8.75f, FontStyle.Bold);
        public static readonly Font Tiny = new Font("Segoe UI", 8f);
        public static readonly Font Glyph = new Font("Segoe MDL2 Assets", 10.5f);
        public static readonly Font GlyphSm = new Font("Segoe MDL2 Assets", 9f);
        public static readonly Font GlyphBig = new Font("Segoe MDL2 Assets", 14f);

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 1) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, Rectangle r, int radius)
        {
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Color c, Rectangle r, int radius, float width)
        {
            Rectangle rr = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Round(rr, radius))
            using (Pen pen = new Pen(c, width))
            {
                pen.Alignment = PenAlignment.Inset;
                g.DrawPath(pen, p);
            }
        }

        public static void Draw(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPadding);
        }

        public static void TextLeft(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            Draw(g, s, f, c, r, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        public static Size Measure(string s, Font f)
        {
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static string FormatBytes(double bytes)
        {
            if (bytes <= 0) return "";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double v = bytes;
            while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
            if (v >= 100) return string.Format("{0:0} {1}", v, units[i]);
            return string.Format("{0:0.#} {1}", v, units[i]);
        }

        public static string FormatDuration(double seconds)
        {
            if (seconds <= 0) return "";
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1) return string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0}:{1:00}", t.Minutes, t.Seconds);
        }

        public static void EnableDoubleBuffer(Control c)
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(c, true, null);
        }
    }
}
