using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MpcGrabber
{
    internal class ChannelPicker : ScrollList
    {
        public List<SavedChannel> Items = new List<SavedChannel>();
        public int SelectedIndex = -1;
        public event EventHandler SelectionChanged;
        private int _hover = -1;
        private const int RowH = 54;

        public ChannelPicker() { BackColor = T.Bg; }

        public void SetItems(List<SavedChannel> items)
        {
            Items = items ?? new List<SavedChannel>();
            if (SelectedIndex >= Items.Count) SelectedIndex = Items.Count - 1;
            ScrollPos = 0;
            ContentHeight = Items.Count * RowH;
            Invalidate();
        }

        public void SelectChannel(string id)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (string.Equals(Items[i].Id, id, StringComparison.OrdinalIgnoreCase)) { SelectedIndex = i; Invalidate(); return; }
            }
        }

        private Rectangle RowRect(int i) { return new Rectangle(0, i * RowH - ScrollPos, Width - 4, RowH); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < Items.Count; i++) if (RowRect(i).Contains(e.Location)) h = i;
            if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (!RowRect(i).Contains(e.Location)) continue;
                if (SelectedIndex != i)
                {
                    SelectedIndex = i;
                    Invalidate();
                    if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                }
                break;
            }
            base.OnMouseUp(e);
        }

        protected override void PaintContent(Graphics g, int offset)
        {
            if (Items.Count == 0)
            {
                T.Draw(g, "No saved channels yet.", T.Small, T.Faint, new Rectangle(6, 4, Width - 12, 20),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                T.Draw(g, "Paste a channel link above and load it.", T.Tiny, T.Faint, new Rectangle(6, 24, Width - 12, 18),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                return;
            }
            for (int i = 0; i < Items.Count; i++)
            {
                Rectangle r = RowRect(i);
                if (r.Bottom < 0 || r.Top > Height) continue;
                bool sel = i == SelectedIndex;
                bool hov = i == _hover;
                Rectangle card = new Rectangle(r.X + 2, r.Y + 3, r.Width - 4, RowH - 10);
                T.FillRound(g, sel ? T.AccentTint : (hov ? T.Hover : T.Card), card, 9);
                T.StrokeRound(g, sel ? T.Accent : T.BorderSoft, card, 9, sel ? 1.4f : 1f);

                T.Draw(g, Items[i].Name ?? "", T.BodyB, sel ? T.Text : T.Dim,
                    new Rectangle(card.X + 12, card.Y + 6, card.Width - 24, 20),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                int n = Store.CountDownloaded(Items[i].Id);
                string sub = n == 0 ? "no downloads yet" : n + (n == 1 ? " download" : " downloads");
                T.Draw(g, sub, T.Tiny, sel ? T.Mix(T.Accent, Color.White, 0.35) : T.Faint,
                    new Rectangle(card.X + 12, card.Y + 26, card.Width - 24, 16),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }
    }

    internal class ChannelVideoList : ScrollList
    {
        private class Row
        {
            public int VideoIndex = -1;
            public string Header;
            public int Y;
            public int H;
        }

        public List<ChannelVideo> Items = new List<ChannelVideo>();
        public event EventHandler Changed;

        private const int RowH = 44;
        private const int HeadH = 28;
        private readonly List<Row> _rows = new List<Row>();
        private int _hover = -1;

        public ChannelVideoList() { BackColor = T.Bg; }

        public int CheckedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Items.Count; i++) if (Items[i].Checked && !Items[i].Downloaded) n++;
                return n;
            }
        }

        public void SetItems(List<ChannelVideo> items)
        {
            Items = items ?? new List<ChannelVideo>();
            ScrollPos = 0;
            Rebuild();
            Invalidate();
        }

        public void SetAll(bool value)
        {
            for (int i = 0; i < Items.Count; i++) if (!Items[i].Downloaded) Items[i].Checked = value;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public void RefreshLayouts()
        {
            Rebuild();
            Invalidate();
        }

        private void Rebuild()
        {
            _rows.Clear();
            int y = 0;
            int vids = 0, shorts = 0;
            for (int i = 0; i < Items.Count; i++) { if (Items[i].IsShort) shorts++; else vids++; }

            if (vids > 0)
            {
                Row h = new Row();
                h.Header = "VIDEOS  \u00B7  " + vids;
                h.Y = y; h.H = HeadH;
                _rows.Add(h);
                y += HeadH;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].IsShort) continue;
                    Row r = new Row();
                    r.VideoIndex = i; r.Y = y; r.H = RowH;
                    _rows.Add(r);
                    y += RowH;
                }
            }
            if (shorts > 0)
            {
                Row h = new Row();
                h.Header = "SHORTS  \u00B7  " + shorts;
                h.Y = y; h.H = HeadH;
                _rows.Add(h);
                y += HeadH;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (!Items[i].IsShort) continue;
                    Row r = new Row();
                    r.VideoIndex = i; r.Y = y; r.H = RowH;
                    _rows.Add(r);
                    y += RowH;
                }
            }
            ContentHeight = y;
        }

        private Row RowAt(Point p)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                if (r.VideoIndex < 0) continue;
                Rectangle rect = new Rectangle(0, r.Y - ScrollPos, Width - 6, r.H);
                if (rect.Contains(p)) return r;
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Row r = RowAt(e.Location);
            int h = r == null ? -1 : r.VideoIndex;
            if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Row r = RowAt(e.Location);
            if (r != null && r.VideoIndex >= 0)
            {
                ChannelVideo v = Items[r.VideoIndex];
                if (!v.Downloaded)
                {
                    v.Checked = !v.Checked;
                    Invalidate();
                    if (Changed != null) Changed(this, EventArgs.Empty);
                }
            }
            base.OnMouseUp(e);
        }

        protected override void PaintContent(Graphics g, int offset)
        {
            if (Items.Count == 0) return;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                int y = row.Y - ScrollPos;
                if (y + row.H < 0 || y > Height) continue;

                if (row.VideoIndex < 0)
                {
                    T.Draw(g, row.Header, T.SmallB, T.Faint, new Rectangle(4, y, Width - 12, row.H),
                        TextFormatFlags.Left | TextFormatFlags.Bottom);
                    continue;
                }

                ChannelVideo v = Items[row.VideoIndex];
                Rectangle r = new Rectangle(0, y, Width - 6, RowH);
                bool hov = row.VideoIndex == _hover;
                Rectangle card = new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, RowH - 8);
                bool on = v.Checked && !v.Downloaded;
                Color edge = v.IsShort ? T.Purple : T.Blue;
                Color tint = v.IsShort ? T.PurpleTint : T.BlueTint;
                T.FillRound(g, on ? tint : (hov ? T.Hover : T.Card), card, 9);
                T.StrokeRound(g, v.Downloaded ? T.BorderSoft : edge, card, 9, on ? 1.6f : 1.1f);

                Rectangle cb = new Rectangle(card.X + 12, card.Y + 10, 18, 18);
                if (v.Downloaded)
                {
                    T.FillRound(g, T.Card2, cb, 5);
                    T.StrokeRound(g, T.Border, cb, 5, 1f);
                    T.Draw(g, "\uE73E", T.GlyphSm, T.Success, cb, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    T.FillRound(g, v.Checked ? T.Accent : T.Card2, cb, 5);
                    if (!v.Checked) T.StrokeRound(g, T.Border, cb, 5, 1f);
                    if (v.Checked) T.Draw(g, "\uE73E", T.GlyphSm, T.OnAccent, cb, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                int x = cb.Right + 10;
                if (v.IsShort)
                {
                    Rectangle badge = new Rectangle(x, card.Y + 11, 52, 18);
                    T.FillRound(g, T.PurpleTint, badge, 4);
                    T.StrokeRound(g, T.Purple, badge, 4, 1f);
                    T.Draw(g, "SHORT", T.Tiny, T.Purple, badge, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    x += 60;
                }

                string date = v.Timestamp > 0 ? "" : "recent";
                if (v.Timestamp > 0)
                {
                    DateTime local = DateTimeOffset.FromUnixTimeSeconds(v.Timestamp).ToLocalTime().DateTime;
                    date = v.HasTime ? local.ToString("MM/dd/yy HH:mm") : local.ToString("MM/dd/yy");
                }

                int rightW = 108;
                if (date.Length > 0)
                    T.Draw(g, date, T.Small, T.Faint, new Rectangle(card.Right - rightW - 10, card.Y + 4, rightW, 16),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                string dur = T.FormatDuration(v.Duration);
                string tail = v.Downloaded ? "downloaded" : dur;
                T.Draw(g, tail, T.Tiny, v.Downloaded ? T.Success : T.Dim,
                    new Rectangle(card.Right - rightW - 10, card.Y + 21, rightW, 14),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                Color titleColor = v.Downloaded ? T.Faint : T.Text;
                T.Draw(g, v.Title, T.Body, titleColor,
                    new Rectangle(x, card.Y + 4, card.Right - x - rightW - 16, 36),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
            }
        }
    }
}
