using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MpcGrabber
{
    internal class FormatGrid : ScrollList
    {
        public List<FormatOption> Items = new List<FormatOption>();
        public int SelectedIndex = 0;
        public event EventHandler SelectionChanged;

        private int _hover = -1;
        private int _cardH = 62;
        private int _gap = 10;
        private int _cols = 4;
        private int _cardW = 150;

        public FormatGrid() { BackColor = T.Bg; }

        public void SetItems(List<FormatOption> items, int selected)
        {
            Items = items ?? new List<FormatOption>();
            SelectedIndex = Math.Min(Math.Max(selected, 0), Math.Max(0, Items.Count - 1));
            ScrollPos = 0;
            Recalc();
            Invalidate();
        }

        public void RefreshLayout() { Recalc(); Invalidate(); }

        private void Recalc()
        {
            if (Width <= 0) return;
            _cols = ColumnsFor(Width);
            _cardW = (Width - _gap * (_cols - 1)) / _cols;
            int rows = (Items.Count + _cols - 1) / _cols;
            ContentHeight = rows * (_cardH + _gap) - _gap;
        }

        public int HeightFor(int width)
        {
            if (Items.Count == 0) return 0;
            int cols = ColumnsFor(width);
            int rows = (Items.Count + cols - 1) / cols;
            return rows * (_cardH + _gap) - _gap;
        }

        private int ColumnsFor(int width)
        {
            return Math.Max(2, (width + _gap) / (150 + _gap));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Recalc();
        }

        private Rectangle CardRect(int i)
        {
            int col = i % _cols;
            int row = i / _cols;
            return new Rectangle(col * (_cardW + _gap), row * (_cardH + _gap) - ScrollPos, _cardW, _cardH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < Items.Count; i++) if (CardRect(i).Contains(e.Location)) h = i;
            if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (CardRect(i).Contains(e.Location))
                {
                    if (SelectedIndex != i)
                    {
                        SelectedIndex = i;
                        Invalidate();
                        if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                    }
                    break;
                }
            }
            base.OnMouseUp(e);
        }

        protected override void PaintContent(Graphics g, int offset)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Rectangle r = CardRect(i);
                if (r.Bottom < 0 || r.Top > Height) continue;
                bool sel = i == SelectedIndex;
                bool hov = i == _hover;

                Color fill = sel ? T.AccentTint : (hov ? T.Hover : T.Card2);
                T.FillRound(g, fill, r, 10);
                T.StrokeRound(g, sel ? T.Accent : (hov ? T.Border : T.BorderSoft), r, 10, sel ? 1.5f : 1f);

                T.Draw(g, Items[i].Title, T.H3, sel ? Color.White : T.Text,
                    new Rectangle(r.X + 13, r.Y + 10, r.Width - 26, 20), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                T.Draw(g, Items[i].Sub, T.Small, sel ? T.Mix(T.Accent, Color.White, 0.55) : T.Dim,
                    new Rectangle(r.X + 13, r.Y + 33, r.Width - 26, 18), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (Items[i].Badge != null)
                {
                    Size bs = T.Measure(Items[i].Badge, T.Tiny);
                    Rectangle br = new Rectangle(r.Right - bs.Width - 22, r.Y + 9, bs.Width + 12, 17);
                    T.FillRound(g, T.Mix(T.Accent, T.Card, 0.72), br, 8);
                    T.Draw(g, Items[i].Badge, T.Tiny, T.Mix(T.Accent, Color.White, 0.35), br, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    internal class EntryList : ScrollList
    {
        public List<PlaylistEntry> Items = new List<PlaylistEntry>();
        public event EventHandler Changed;

        private int _hover = -1;
        private const int RowH = 42;

        public EntryList() { BackColor = T.Bg; }

        public void SetItems(List<PlaylistEntry> items)
        {
            Items = items ?? new List<PlaylistEntry>();
            ScrollPos = 0;
            ContentHeight = Items.Count * RowH;
            Invalidate();
        }

        public int CheckedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Items.Count; i++) if (Items[i].Checked) n++;
                return n;
            }
        }

        public void SetAll(bool value)
        {
            for (int i = 0; i < Items.Count; i++) Items[i].Checked = value;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        private Rectangle RowRect(int i) { return new Rectangle(0, i * RowH - ScrollPos, Width - 8, RowH); }

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
                if (RowRect(i).Contains(e.Location))
                {
                    Items[i].Checked = !Items[i].Checked;
                    Invalidate();
                    if (Changed != null) Changed(this, EventArgs.Empty);
                    break;
                }
            }
            base.OnMouseUp(e);
        }

        protected override void PaintContent(Graphics g, int offset)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Rectangle r = RowRect(i);
                if (r.Bottom < 0 || r.Top > Height) continue;
                if (i == _hover) T.FillRound(g, T.Hover, new Rectangle(r.X, r.Y, r.Width, r.Height - 2), 8);

                Rectangle cb = new Rectangle(12, r.Y + 11, 20, 20);
                bool on = Items[i].Checked;
                T.FillRound(g, on ? T.Accent : T.Card2, cb, 6);
                if (!on) T.StrokeRound(g, T.Border, cb, 6, 1f);
                if (on)
                    T.Draw(g, "\uE73E", T.GlyphSm, Color.White, cb, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                string dur = T.FormatDuration(Items[i].Duration);
                int dw = dur.Length > 0 ? (int)T.Measure(dur, T.Small).Width + 12 : 0;
                if (dw > 0)
                    T.Draw(g, dur, T.Small, T.Faint, new Rectangle(r.Right - dw - 8, r.Y, dw, r.Height), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                T.Draw(g, (i + 1).ToString() + ".  " + Items[i].Title, T.Body, on ? T.Text : T.Dim,
                    new Rectangle(cb.Right + 10, r.Y, r.Width - cb.Right - 20 - dw, r.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    internal class QueueList : ScrollList
    {
        public List<DownloadJob> Jobs = new List<DownloadJob>();
        private const int RowH = 58;

        public QueueList() { BackColor = T.Bg; }

        public void Refresh2()
        {
            ContentHeight = Jobs.Count * RowH;
            Invalidate();
        }

        public void EnsureVisible(int index)
        {
            if (index < 0 || index >= Jobs.Count || Height <= 0) return;
            int top = index * RowH;
            int bottom = top + RowH;
            int viewTop = ScrollPos;
            int viewBottom = ScrollPos + Height;
            if (top < viewTop) ScrollPos = top;
            else if (bottom > viewBottom) ScrollPos = bottom - Height + 6;
            if (ScrollPos < 0) ScrollPos = 0;
            if (ScrollPos > MaxScroll) ScrollPos = MaxScroll;
            Invalidate();
        }

        protected override void PaintContent(Graphics g, int offset)
        {
            for (int i = 0; i < Jobs.Count; i++)
            {
                DownloadJob j = Jobs[i];
                Rectangle r = new Rectangle(0, i * RowH - ScrollPos, Width - 8, RowH);
                if (r.Bottom < 0 || r.Top > Height) continue;

                bool active = j.State == JobState.Downloading || j.State == JobState.Converting;
                Rectangle card = new Rectangle(r.X, r.Y + 2, r.Width, RowH - 8);
                T.FillRound(g, active ? T.AccentTint : T.Card2, card, 9);
                T.StrokeRound(g, active ? T.Accent : T.BorderSoft, card, 9, 1f);

                Color dot = T.Faint;
                if (active) dot = T.Accent;
                else if (j.State == JobState.Done) dot = T.Success;
                else if (j.State == JobState.Failed) dot = T.Danger;
                else if (j.State == JobState.Cancelled) dot = T.Warn;

                using (SolidBrush b = new SolidBrush(dot))
                    g.FillEllipse(b, r.X + 14, r.Y + 16, 7, 7);

                string q = j.QualityLabel == null ? "" : j.QualityLabel;
                int qw = q.Length > 0 ? 92 : 0;
                if (qw > 0)
                    T.Draw(g, q, T.SmallB, active ? T.Accent : T.Faint,
                        new Rectangle(r.Right - qw - 16, r.Y + 6, qw, 20),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                T.Draw(g, (i + 1) + ".  " + j.Title, T.Body, T.Text,
                    new Rectangle(r.X + 30, r.Y + 6, r.Width - 30 - qw - 34, 20),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                string status = j.Status;
                if (j.State == JobState.Downloading || j.State == JobState.Converting)
                {
                    status += "  " + (int)(j.Progress * 100) + "%";
                    if (j.Speed.Length > 0) status += "  \u00B7  " + j.Speed;
                    if (j.Eta.Length > 0) status += "  \u00B7  ETA " + j.Eta;
                }
                if (j.State == JobState.Failed && j.Error.Length > 0) status = "Failed \u2014 " + j.Error;

                T.Draw(g, status, T.Small, j.State == JobState.Failed ? T.Danger : T.Dim,
                    new Rectangle(r.X + 30, r.Y + 27, r.Width - qw - 48, 18),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (active)
                {
                    Rectangle track = new Rectangle(r.X + 30, r.Y + 47, r.Width - 46, 4);
                    T.FillRound(g, T.Border, track, 2);
                    int w = (int)(track.Width * j.Progress);
                    if (w > 2) T.FillRound(g, T.Accent, new Rectangle(track.X, track.Y, w, 4), 2);
                }
            }
        }
    }

    internal class HistoryList : ScrollList
    {
        public List<HistoryEntry> Items = new List<HistoryEntry>();
        public event Action<HistoryEntry> OpenRequested;
        public event Action<HistoryEntry> FolderRequested;
        public event Action<HistoryEntry> RedownloadRequested;
        public event Action<HistoryEntry> DeleteRequested;

        private const int RowH = 70;
        private int _hover = -1;
        private int _hoverBtn = -1;

        public HistoryList() { BackColor = T.Bg; }

        public void Refresh2()
        {
            ContentHeight = Items.Count * RowH;
            Invalidate();
        }

        private Rectangle RowRect(int i) { return new Rectangle(0, i * RowH - ScrollPos, Width - 6, RowH); }

        private Rectangle BtnRect(Rectangle row, int index)
        {
            return new Rectangle(row.Right - 42 * (4 - index) - 10, row.Y + 18, 32, 32);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1, b = -1;
            for (int i = 0; i < Items.Count; i++)
            {
                Rectangle r = RowRect(i);
                if (r.Contains(e.Location))
                {
                    h = i;
                    for (int k = 0; k < 4; k++) if (BtnRect(r, k).Contains(e.Location)) b = k;
                }
            }
            if (h != _hover || b != _hoverBtn) { _hover = h; _hoverBtn = b; Cursor = b >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; _hoverBtn = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Rectangle r = RowRect(i);
                if (!r.Contains(e.Location)) continue;
                HistoryEntry h = Items[i];
                for (int k = 0; k < 4; k++)
                {
                    if (!BtnRect(r, k).Contains(e.Location)) continue;
                    if (k == 0 && OpenRequested != null) OpenRequested(h);
                    if (k == 1 && FolderRequested != null) FolderRequested(h);
                    if (k == 2 && RedownloadRequested != null) RedownloadRequested(h);
                    if (k == 3 && DeleteRequested != null) DeleteRequested(h);
                    return;
                }
                if (OpenRequested != null) OpenRequested(h);
                return;
            }
            base.OnMouseUp(e);
        }

        protected override void PaintContent(Graphics g, int offset)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                HistoryEntry h = Items[i];
                Rectangle r = RowRect(i);
                if (r.Bottom < 0 || r.Top > Height) continue;

                T.FillRound(g, i == _hover ? T.Hover : T.Card2, new Rectangle(r.X, r.Y + 3, r.Width, RowH - 10), 10);
                T.StrokeRound(g, T.BorderSoft, new Rectangle(r.X, r.Y + 3, r.Width, RowH - 10), 10, 1f);

                Rectangle badge = new Rectangle(r.X + 14, r.Y + 15, 40, 40);
                bool mp3 = h.Kind == "MP3";
                T.FillRound(g, mp3 ? Color.FromArgb(24, 48, 40) : T.AccentTint, badge, 10);
                T.Draw(g, mp3 ? "MP3" : "MP4", T.SmallB, mp3 ? T.Success : T.Accent, badge,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                int textW = r.Width - 14 - 56 - 190;
                T.Draw(g, h.Title, T.Body, T.Text, new Rectangle(r.X + 66, r.Y + 12, textW, 20),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                string chan = h.Channel == null ? "" : h.Channel;
                string meta = (chan.Length > 0 ? chan + "  \u00B7  " : "") + (h.Quality ?? "") + "  \u00B7  " + T.FormatBytes(h.SizeBytes) + "  \u00B7  " + (h.FinishedAt ?? "");
                T.Draw(g, meta, T.Small, T.Dim, new Rectangle(r.X + 66, r.Y + 34, textW, 18),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                for (int k = 0; k < 4; k++)
                {
                    Rectangle br = BtnRect(r, k);
                    bool hb = i == _hover && k == _hoverBtn;
                    if (hb) T.FillRound(g, T.Press, br, 8);
                    string glyph;
                    if (k == 0) glyph = "\uE8E5";
                    else if (k == 1) glyph = "\uE8B7";
                    else if (k == 2) glyph = "\uE896";
                    else glyph = "\uE74D";
                    Color gc = hb ? T.Text : T.Faint;
                    if (k == 3 && hb) gc = T.Danger;
                    T.Draw(g, glyph, T.Glyph, gc, br, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }
}
