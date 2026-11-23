using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MpcGrabber
{
    internal class EmptyState : DcControl
    {
        public EmptyState() { BackColor = T.Bg; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = T.Round(r, 14))
            using (Pen pen = new Pen(T.Border, 1.2f))
            {
                pen.DashStyle = DashStyle.Dash;
                pen.DashPattern = new float[] { 4f, 4f };
                g.DrawPath(pen, p);
            }

            int cy = Height / 2;
            Rectangle icon = new Rectangle(Width / 2 - 26, cy - 76, 52, 52);
            T.FillRound(g, T.Card2, icon, 14);
            T.Draw(g, "\uE896", T.GlyphBig, T.Accent, icon, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            T.Draw(g, "Paste a link to get started", T.H3, T.Text,
                new Rectangle(0, cy - 14, Width, 22), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            T.Draw(g, "Or drop a URL anywhere in this window \u2014 qualities will appear here.",
                T.Small, T.Faint, new Rectangle(0, cy + 12, Width, 20), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal class InfoCard : DcControl
    {
        public MediaInfo Info;
        public Image Thumb;

        public InfoCard() { BackColor = T.Bg; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);
            T.FillRound(g, T.Card, r, 12);
            T.StrokeRound(g, T.BorderSoft, r, 12, 1f);

            if (Info == null)
            {
                T.Draw(g, "\uE714", T.GlyphBig, T.Faint, new Rectangle(20, 0, 34, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                T.Draw(g, "No link loaded yet", T.BodyB, T.Dim, new Rectangle(58, 12, Width - 80, 22),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                T.Draw(g, "Paste a video or playlist URL above, then press Fetch.", T.Small, T.Faint,
                    new Rectangle(58, 34, Width - 80, 20), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                return;
            }

            int x = 12;
            Rectangle tr = new Rectangle(x, 10, 128, 72);
            if (Thumb != null)
            {
                using (GraphicsPath p = T.Round(tr, 8))
                {
                    g.SetClip(p);
                    g.DrawImage(Thumb, tr);
                    g.ResetClip();
                }
            }
            else
            {
                T.FillRound(g, T.Card2, tr, 8);
                T.Draw(g, Info.IsPlaylist ? "\uE8FD" : "\uE714", T.GlyphBig, T.Faint, tr,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            int tx = tr.Right + 16;
            int tw = Width - tx - 16;

            string title = string.IsNullOrEmpty(Info.Title) ? "(untitled)" : Info.Title;
            T.Draw(g, title, T.H3, T.Text, new Rectangle(tx, 12, tw, 24),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            string meta = "";
            if (!string.IsNullOrEmpty(Info.Uploader)) meta += Info.Uploader;
            if (Info.IsPlaylist) meta += (meta.Length > 0 ? "  \u00B7  " : "") + Info.Entries.Count + " items";
            else if (Info.Duration > 0) meta += (meta.Length > 0 ? "  \u00B7  " : "") + T.FormatDuration(Info.Duration);
            if (Info.ViewCount > 0) meta += (meta.Length > 0 ? "  \u00B7  " : "") + T.FormatBytes(Info.ViewCount) + " views";

            T.Draw(g, meta, T.Small, T.Dim, new Rectangle(tx, 38, tw, 20),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            T.Draw(g, Info.IsPlaylist ? "Playlist" : "Video", T.Tiny, T.Faint, new Rectangle(tx, 60, tw, 16),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    internal class LogoBox : DcControl
    {
        private static Image _banner;

        public LogoBox() { BackColor = T.Sidebar; }

        private static Image Banner()
        {
            if (_banner == null)
            {
                try
                {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("MPCGrabber.banner.png"))
                        if (s != null) _banner = Image.FromStream(s);
                }
                catch { }
            }
            return _banner;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Image b = Banner();
            if (b == null)
            {
                T.Draw(g, "NomadTube", T.H2, T.Text, new Rectangle(16, 18, Width - 32, 40),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                return;
            }
            int w = Math.Min(Width - 30, b.Width);
            int h = (int)Math.Round(b.Height * (w / (double)b.Width));
            g.DrawImage(b, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h));
        }
    }


    internal partial class MainForm : Form
    {
        private DownloadService _svc = new DownloadService();
        private MediaInfo _media;
        private ToolTip _tip = new ToolTip();
        private bool _fetching;

        private Panel _sidebar, _content, _pageDownload, _pageChannels, _pageHistory, _pageSettings, _pageQueue;
        private NavBar _nav;
        private Pill _cookiePill;

        // download page
        private RoundedPanel _urlCard;
        private TextBox _txtUrl;
        private EmptyState _empty;
        private const string PlaceholderText = "Paste a YouTube (or any yt-dlp) link";
        private bool _phActive = true;
        private readonly string _startupUrl = "";
        private bool _autoDownload;
        private string _startupChannel = "";
        private FlatButton _btnPaste, _btnFetch, _btnCancel, _btnFolder, _btnChange;
        private DownloadButton _btnDownload;
        private InfoCard _infoCard;
        private Label _lblQuality;
        private FormatGrid _grid;
        private Label _lblPlaylist, _lblStatus;
        private Label _lblQueueSummary;
        private RoundedPanel _chanInput;
        private bool _loadingChannel;
        private TextBox _txtChannel;
        private bool _chanPhActive = true;
        private const string ChannelPlaceholder = "@handle or youtube.com/@handle";
        private FlatButton _btnLoadChannel, _btnChan24, _btnChan7, _btnChan30, _btnChanAll, _btnChanNone, _btnChanPaste;
        private DownloadButton _btnChanDownload;
        private LoadingOverlay _loading;
        private ToggleBox _chkUpdates;
        private FlatButton _btnCheckNow;
        private Label _lblUpdateStatus;
        private Label _lblVersion;
        private ChannelPicker _chanPicker;
        private ChannelVideoList _chanList;
        private Label _lblChanHeader, _lblChanSaved, _lblChanHint;
        private ChannelInfo _channel;
        private int _channelDays = 30;
        private FlatButton _btnQueueCancel, _btnQueueClear;
        private FlatButton _btnAll, _btnNone;
        private EntryList _entries;
        private QueueList _queue;
        private ProgressBarEx _progress;
        private Panel _footer;

        // history page
        private HistoryList _historyList;
        private Label _lblHistEmpty;
        private FlatButton _btnClearHistory;

        // settings page
        private FlatButton _btnSettChange, _btnSettOpenFolder, _btnClearHistory2;
        private Label _lblSettFolder, _lblCookies;
        private FlatButton[] _videoBtns;
        private FlatButton[] _audioBtns;
        private static readonly string[] _videoVals = { "best", "2160", "1440", "1080", "720", "480", "360" };
        private static readonly int[] _audioVals = { 320, 192, 128 };

        public MainForm(string[] args)
        {
            if (args != null && args.Length > 1 && (args[1].StartsWith("http", StringComparison.OrdinalIgnoreCase) || args[1].StartsWith("@") || args[1].IndexOf("youtube.com/", StringComparison.OrdinalIgnoreCase) >= 0))
                _startupUrl = args[1].Trim();
            if (args != null && args.Length > 2 && string.Equals(args[2], "--download", StringComparison.OrdinalIgnoreCase))
                _autoDownload = true;

            Text = "NomadTube";
            AutoScaleMode = AutoScaleMode.None;
            BackColor = T.Bg;
            ForeColor = T.Text;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1300, 840);
            MinimumSize = new Size(1060, 560);
            FormBorderStyle = FormBorderStyle.Sizable;
            KeyPreview = true;
            T.EnableDoubleBuffer(this);

            BuildSidebar();
            BuildContent();
            BuildDownloadPage();
            BuildHistoryPage();
            BuildSettingsPage();
            BuildQueuePage();
            BuildChannelsPage();

            _loading = new LoadingOverlay();
            _loading.Dock = DockStyle.Fill;
            Controls.Add(_loading);
            _loading.BringToFront();

            _svc.Changed += ServiceChanged;
            _svc.JobCompleted += ServiceJobCompleted;

            Load += MainForm_Load;
            KeyDown += MainForm_KeyDown;
            AllowDrop = true;
            DragEnter += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.Text)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += delegate(object s, DragEventArgs e)
            {
                string u = e.Data.GetData(DataFormats.Text) as string;
                if (string.IsNullOrWhiteSpace(u)) return;
                _phActive = false;
                _txtUrl.ForeColor = T.Text;
                _txtUrl.Text = u.Trim();
                _txtUrl.SelectionStart = _txtUrl.Text.Length;
                _nav.SelectedIndex = 0;
                ShowPage(0);
                BtnFetch_Click(null, null);
            };
            FormClosing += MainForm_FormClosing;
        }

        // ---------- chrome ----------
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                DwmSetWindowAttribute(Handle, 20, ref on, 4);
                DwmSetWindowAttribute(Handle, 19, ref on, 4);
                int caption = ColorTranslator.ToWin32(T.Bg);
                DwmSetWindowAttribute(Handle, 35, ref caption, 4);
                int text = ColorTranslator.ToWin32(T.Accent);
                DwmSetWindowAttribute(Handle, 36, ref text, 4);
                int round = 2;
                DwmSetWindowAttribute(Handle, 33, ref round, 4);
                UpdateDwmBorder();
            }
            catch { }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateDwmBorder();
        }

        private void UpdateDwmBorder()
        {
            if (!IsHandleCreated) return;
            try
            {
                // green frame while floating, black once maximized
                int color = WindowState == FormWindowState.Maximized ? ColorTranslator.ToWin32(T.Bg) : ColorTranslator.ToWin32(T.Accent);
                DwmSetWindowAttribute(Handle, 34, ref color, 4);
            }
            catch { }
        }

        // ---------- layout skeleton ----------
        private void BuildSidebar()
        {
            _sidebar = new Panel();
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = 212;
            _sidebar.BackColor = T.Sidebar;
            Controls.Add(_sidebar);

            LogoBox logo = new LogoBox();
            logo.Bounds = new Rectangle(0, 0, 212, 78);
            _sidebar.Controls.Add(logo);

            _nav = new NavBar();
            _nav.Bounds = new Rectangle(0, 92, 212, 250);
            _nav.Items.Add(new NavItem("\uE896", "Download"));
            _nav.Items.Add(new NavItem("\uE716", "Channels"));
            _nav.Items.Add(new NavItem("\uE81C", "History"));
            _nav.Items.Add(new NavItem("\uE713", "Settings"));
            _nav.Items.Add(new NavItem("\uE8FD", "Queue"));
            _nav.SelectionChanged += delegate { ShowPage(_nav.SelectedIndex); };
            _sidebar.Controls.Add(_nav);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 74;
            bottom.BackColor = T.Sidebar;
            _sidebar.Controls.Add(bottom);

            _cookiePill = new Pill();
            _cookiePill.Bounds = new Rectangle(14, 6, 184, 24);
            _cookiePill.BackColor = T.Sidebar;
            _cookiePill.BgColor = T.Card2;
            bottom.Controls.Add(_cookiePill);

            _lblVersion = new Label();
            _lblVersion.Text = "v" + Updater.CurrentShort + "  \u00B7  portable";
            _lblVersion.Font = T.Tiny;
            _lblVersion.ForeColor = T.Faint;
            _lblVersion.BackColor = T.Sidebar;
            _lblVersion.AutoSize = false;
            _lblVersion.Bounds = new Rectangle(18, 38, 180, 16);
            bottom.Controls.Add(_lblVersion);

            Panel edge = new Panel();
            edge.Dock = DockStyle.Right;
            edge.Width = 1;
            edge.BackColor = Color.FromArgb(22, 26, 34);
            _sidebar.Controls.Add(edge);
        }

        private void BuildContent()
        {
            _content = new Panel();
            _content.Dock = DockStyle.Fill;
            _content.BackColor = T.Bg;
            _content.Padding = new Padding(28, 24, 28, 20);
            Controls.Add(_content);
            _content.BringToFront();

            _pageDownload = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg };
            _pageHistory = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg, Visible = false };
            _pageSettings = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg, Visible = false };
            _pageChannels = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg, Visible = false };
            _pageQueue = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg, Visible = false };
            _content.Controls.Add(_pageDownload);
            _content.Controls.Add(_pageHistory);
            _content.Controls.Add(_pageSettings);
            _content.Controls.Add(_pageChannels);
            _content.Controls.Add(_pageQueue);
        }

        private void ShowPage(int index)
        {
            _pageDownload.Visible = index == 0;
            _pageChannels.Visible = index == 1;
            _pageHistory.Visible = index == 2;
            _pageSettings.Visible = index == 3;
            _pageQueue.Visible = index == 4;
            if (index == 0) LayoutDownloadPage();
            if (index == 1) { LayoutChannelsPage(); _chanPicker.SetItems(Store.Channels); }
            if (index == 2) RefreshHistory();
            if (index == 3) RefreshSettings();
            if (index == 4) { LayoutQueuePage(); RefreshQueuePage(); }
        }

        // ---------- helpers ----------
        private static Label MkLabel(string text, Font f, Color c, Color bg)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = f;
            l.ForeColor = c;
            l.BackColor = bg;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private static FlatButton MkButton(string text, string glyph, BtnVariant v, int w, Color bg)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Glyph = glyph;
            b.Variant = v;
            b.Width = w;
            b.Height = 36;
            b.BackColor = bg;
            return b;
        }

        // ---------- download page ----------
        private void BuildDownloadPage()
        {
            Label title = MkLabel("Download", T.H1, T.Text, T.Bg);
            title.Bounds = new Rectangle(0, 0, 400, 36);
            _pageDownload.Controls.Add(title);

            Label sub = MkLabel("Paste a link, choose a quality, press Download.", T.Small, T.Faint, T.Bg);
            sub.Bounds = new Rectangle(2, 38, 600, 18);
            _pageDownload.Controls.Add(sub);

            _urlCard = new RoundedPanel();
            _urlCard.Radius = 12;
            _urlCard.Fill = T.Card;
            _urlCard.BorderColor = T.Border;
            _urlCard.BackColor = T.Bg;
            _pageDownload.Controls.Add(_urlCard);

            Label linkIcon = MkLabel("\uE71B", T.Glyph, T.Faint, T.Card);
            linkIcon.Bounds = new Rectangle(14, 0, 24, 52);
            linkIcon.TextAlign = ContentAlignment.MiddleCenter;
            _urlCard.Controls.Add(linkIcon);

            _txtUrl = new TextBox();
            _txtUrl.BorderStyle = BorderStyle.None;
            _txtUrl.BackColor = T.Card;
            _txtUrl.ForeColor = T.Faint;
            _txtUrl.Font = new Font("Segoe UI", 10.5f);
            _txtUrl.Text = PlaceholderText;
            _txtUrl.Bounds = new Rectangle(44, 16, 400, 22);
            _txtUrl.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (!e.Control && !_phActive) BtnFetch_Click(null, null);
                }
                else if (e.Control && e.KeyCode == Keys.V)
                {
                    e.SuppressKeyPress = true;
                    DoPaste();
                }
            };
            _txtUrl.KeyPress += delegate(object s, KeyPressEventArgs e)
            {
                if (_phActive)
                {
                    _phActive = false;
                    _txtUrl.Text = "";
                    _txtUrl.ForeColor = T.Text;
                }
            };
            _txtUrl.TextChanged += delegate
            {
                if (_phActive && _txtUrl.Text != PlaceholderText)
                {
                    _phActive = false;
                    _txtUrl.ForeColor = T.Text;
                }
            };
            _txtUrl.GotFocus += delegate
            {
                _txtUrl.SelectionStart = _txtUrl.Text.Length;
                _urlCard.BorderColor = T.Accent;
                _urlCard.Invalidate();
            };
            _txtUrl.LostFocus += delegate
            {
                if (_txtUrl.Text.Trim().Length == 0)
                {
                    _phActive = true;
                    _txtUrl.Text = PlaceholderText;
                    _txtUrl.ForeColor = T.Faint;
                }
                _urlCard.BorderColor = T.Border;
                _urlCard.Invalidate();
            };
            _urlCard.Controls.Add(_txtUrl);
            _txtUrl.GotFocus += delegate { _urlCard.BorderColor = T.Accent; _urlCard.Invalidate(); };
            _txtUrl.LostFocus += delegate { _urlCard.BorderColor = T.Border; _urlCard.Invalidate(); };

            _btnPaste = MkButton("Paste", null, BtnVariant.Ghost, 78, T.Card);
            _btnPaste.Height = 34;
            _btnPaste.Click += delegate { DoPaste(); };
            _urlCard.Controls.Add(_btnPaste);

            _btnFetch = MkButton("Fetch", null, BtnVariant.Primary, 88, T.Card);
            _btnFetch.Height = 34;
            _btnFetch.Click += BtnFetch_Click;
            _urlCard.Controls.Add(_btnFetch);

            _infoCard = new InfoCard();
            _pageDownload.Controls.Add(_infoCard);

            _lblQuality = MkLabel("Quality", T.H3, T.Text, T.Bg);
            _pageDownload.Controls.Add(_lblQuality);

            _grid = new FormatGrid();
            _grid.SelectionChanged += delegate { };
            _pageDownload.Controls.Add(_grid);

            _empty = new EmptyState();
            _pageDownload.Controls.Add(_empty);

            _lblPlaylist = MkLabel("Playlist", T.H3, T.Text, T.Bg);
            _pageDownload.Controls.Add(_lblPlaylist);

            _btnAll = MkButton("All", null, BtnVariant.Ghost, 52, T.Bg);
            _btnAll.Height = 26;
            _btnAll.Font = T.Small;
            _btnAll.Click += delegate { _entries.SetAll(true); _btnDownload.Count = _entries.CheckedCount; };
            _pageDownload.Controls.Add(_btnAll);

            _btnNone = MkButton("None", null, BtnVariant.Ghost, 58, T.Bg);
            _btnNone.Height = 26;
            _btnNone.Font = T.Small;
            _btnNone.Click += delegate { _entries.SetAll(false); _btnDownload.Count = _entries.CheckedCount; };
            _pageDownload.Controls.Add(_btnNone);

            _entries = new EntryList();
            _entries.Changed += delegate { if (_btnDownload != null) _btnDownload.Count = _entries.CheckedCount; };
            _pageDownload.Controls.Add(_entries);

            _footer = new Panel();
            _footer.BackColor = T.Bg;
            _footer.Dock = DockStyle.Bottom;
            _footer.Height = 84;
            _pageDownload.Controls.Add(_footer);

            Divider div = new Divider();
            div.Dock = DockStyle.Top;
            div.Height = 1;
            _footer.Controls.Add(div);

            _btnFolder = MkButton("Downloads", "\uE8B7", BtnVariant.Ghost, 300, T.Bg);
            _btnFolder.Height = 34;
            _btnFolder.LeftAlign = true;
            _btnFolder.Font = T.Small;
            _btnFolder.Click += delegate { OpenFolder(Store.Current.DownloadFolder); };
            _tip.SetToolTip(_btnFolder, "Open the download folder");
            _footer.Controls.Add(_btnFolder);

            _btnChange = MkButton("Change", null, BtnVariant.Soft, 74, T.Bg);
            _btnChange.Height = 34;
            _btnChange.Click += delegate { ChangeDownloadFolder(); };
            _footer.Controls.Add(_btnChange);

            _lblStatus = MkLabel("", T.Small, T.Dim, T.Bg);
            _footer.Controls.Add(_lblStatus);

            _progress = new ProgressBarEx();
            _progress.BackColor = T.Bg;
            _progress.Height = 8;
            _footer.Controls.Add(_progress);

            _btnCancel = MkButton("Cancel", null, BtnVariant.Ghost, 84, T.Bg);
            _btnCancel.Height = 40;
            _btnCancel.Visible = false;
            _btnCancel.Click += delegate { _svc.CancelCurrent(); };
            _footer.Controls.Add(_btnCancel);

            _btnDownload = new DownloadButton();
            _btnDownload.Label = "Download";
            _btnDownload.Bounds = new Rectangle(0, 0, 190, 46);
            _btnDownload.Click += BtnDownload_Click;
            _footer.Controls.Add(_btnDownload);

            _pageDownload.Resize += delegate { LayoutDownloadPage(); };
        }

        private void LayoutDownloadPage()
        {
            if (_pageDownload == null || _footer == null) return;
            int W = _pageDownload.ClientSize.Width;
            int H = _pageDownload.ClientSize.Height - _footer.Height;

            _urlCard.Bounds = new Rectangle(0, 64, W, 52);
            int inputW = Math.Max(120, W - 44 - 12 - 78 - 8 - 88 - 12);
            _txtUrl.Width = inputW;
            _btnPaste.Left = W - 12 - 88 - 8 - 78;
            _btnPaste.Top = 9;
            _btnFetch.Left = W - 12 - 88;
            _btnFetch.Top = 9;

            bool hasMedia = _media != null;
            bool hasJobs = _svc.Jobs.Count > 0;
            _infoCard.Visible = hasMedia;
            _empty.Visible = !hasMedia && !hasJobs;
            _infoCard.Bounds = new Rectangle(0, 128, W, 92);

            bool playlist = hasMedia && _media.IsPlaylist;
            bool hasFormats = _grid.Items.Count > 0;

            int y = 128 + 92 + 16;
            if (!hasMedia)
            {
                y = 128;
                if (!hasJobs) _empty.Bounds = new Rectangle(0, 128, W, Math.Max(180, H - 128 - 8));
            }

            _lblPlaylist.Visible = playlist;
            _entries.Visible = playlist;
            _btnAll.Visible = playlist;
            _btnNone.Visible = playlist;
            if (playlist)
            {
                _lblPlaylist.Bounds = new Rectangle(0, y, 300, 20);
                _btnNone.Left = W - 58;
                _btnNone.Top = y - 3;
                _btnAll.Left = W - 58 - 6 - 52;
                _btnAll.Top = y - 3;
                _entries.Bounds = new Rectangle(0, y + 26, W, 168);
                y += 26 + 168 + 14;
            }

            _lblQuality.Visible = hasFormats;
            _grid.Visible = hasFormats;
            if (hasFormats)
            {
                _lblQuality.Bounds = new Rectangle(0, y, 300, 20);
                y += 24;
                int gh = Math.Max(62, _grid.HeightFor(W));
                _grid.Bounds = new Rectangle(0, y, W, gh);
                _grid.RefreshLayout();
                y += gh;
            }


            LayoutFooter(W);
        }

        private void LayoutFooter(int W)
        {
            _btnFolder.Bounds = new Rectangle(0, 8, 260, 34);
            _btnChange.Bounds = new Rectangle(266, 8, 74, 34);
            _lblStatus.Bounds = new Rectangle(0, 50, Math.Max(200, W - 340), 18);
            _progress.Bounds = new Rectangle(Math.Max(0, W - 320), 54, 320, 8);
            _btnDownload.Bounds = new Rectangle(W - 194, 2, 194, 48);
            _btnCancel.Bounds = new Rectangle(W - 150 - 10 - 84, 6, 84, 40);
            _btnCancel.Visible = _svc.Busy;
            _progress.Visible = _svc.Busy;
            if (!_svc.Busy) _btnDownload.Enabled = _media != null;
        }

        // ---------- actions ----------
        private int DefaultFormatIndex(List<FormatOption> items)
        {
            if (items == null || items.Count == 0) return 0;
            string pref = Store.Current.VideoQuality;
            if (string.IsNullOrEmpty(pref) || pref == "best") return 0;
            int want;
            if (!int.TryParse(pref, out want)) return 0;

            int exact = -1;
            int fallback = -1;
            int fallbackH = -1;
            for (int i = 0; i < items.Count; i++)
            {
                string title = items[i].Title;
                if (title == null || !title.EndsWith("p", StringComparison.Ordinal)) continue;
                int h;
                if (!int.TryParse(title.Substring(0, title.Length - 1), out h)) continue;
                if (h == want) { exact = i; break; }
                if (h < want && h > fallbackH) { fallbackH = h; fallback = i; }
            }
            if (exact >= 0) return exact;
            if (fallback >= 0) return fallback;
            return 0;
        }

        private async void BtnFetch_Click(object sender, EventArgs e)
        {
            if (_fetching) return;
            if (!File.Exists(YtDlp.ExePath))
            {
                SetStatus("yt-dlp.exe was not found next to MPCGrabber.exe.", T.Danger);
                return;
            }
            string url = GetUrl();
            if (url.Length == 0)
            {
                SetStatus("Paste a link first.", T.Warn);
                return;
            }

            if (LooksLikeChannelUrl(url))
            {
                _chanPhActive = false;
                _txtChannel.ForeColor = T.Text;
                _txtChannel.Text = url;
                _nav.SelectedIndex = 1;
                ShowPage(1);
                BtnLoadChannel_Click(null, null);
                return;
            }
            _fetching = true;
            _loading.ShowOverlay("Reading link\u2026");
            _btnFetch.Enabled = false;
            SetStatus("Reading the link\u2026", T.Dim);
            try
            {
                MediaInfo info = await YtDlp.FetchAsync(url);
                _media = info;
                _grid.SetItems(info.Formats, DefaultFormatIndex(info.Formats));
                if (info.IsPlaylist)
                {
                    _entries.SetItems(info.Entries);
                    _entries.SetAll(true);
                }
                LayoutDownloadPage();
                _infoCard.Info = info;
                _infoCard.Thumb = null;
                _infoCard.Invalidate();
                LoadThumb(info.ThumbnailUrl);
                SetStatus(info.IsPlaylist ? "Playlist loaded \u2014 uncheck anything you don't want." : "Ready to download.", T.Dim);
                if (_btnDownload != null) _btnDownload.Count = info.IsPlaylist ? info.Entries.Count : 0;
                if (_autoDownload)
                {
                    _autoDownload = false;
                    BtnDownload_Click(null, null);
                }
            }
            catch (Exception ex)
            {
                _media = null;
                _grid.SetItems(new System.Collections.Generic.List<FormatOption>(), 0);
                _infoCard.Info = null;
                _infoCard.Invalidate();
                LayoutDownloadPage();
                SetStatus(Shorten(ex.Message, 180), T.Danger);
            }
            finally
            {
                _fetching = false;
                _loading.HideOverlay();
                _btnFetch.Enabled = true;
            }
        }

        private async void LoadThumb(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                // YouTube hands out .webp posters, which GDI+ cannot decode: ask for the .jpg twin
                string u = url;
                if (u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    u = u.Substring(0, u.Length - 5) + ".jpg";
                    u = u.Replace("/vi_webp/", "/vi/");
                }
                WebClient wc = new WebClient();
                wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                byte[] data = await wc.DownloadDataTaskAsync(u);
                using (MemoryStream ms = new MemoryStream(data))
                {
                    Image img = Image.FromStream(ms);
                    if (IsDisposed) return;
                    _infoCard.Thumb = img;
                    _infoCard.Invalidate();
                }
            }
            catch { }
        }

        private void BtnDownload_Click(object sender, EventArgs e)
        {
            if (_media == null)
            {
                SetStatus("Fetch a link first.", T.Warn);
                return;
            }
            if (_grid.Items.Count == 0) return;
            int idx = Math.Max(0, Math.Min(_grid.SelectedIndex, _grid.Items.Count - 1));
            FormatOption fmt = _grid.Items[idx];

            int count = 0;
            if (_media.IsPlaylist)
            {
                for (int i = 0; i < _media.Entries.Count; i++)
                {
                    PlaylistEntry pe = _media.Entries[i];
                    if (!pe.Checked) continue;
                    _svc.Enqueue(pe.Url, pe.Title, fmt, Store.Current.DownloadFolder);
                    count++;
                }
                if (count == 0) { SetStatus("No playlist items selected.", T.Warn); return; }
            }
            else
            {
                _svc.Enqueue(_media.Url, _media.Title, fmt, Store.Current.DownloadFolder);
                count = 1;
            }
            if (count > 1)
            {
                _nav.SelectedIndex = 4;
                ShowPage(4);
            }
            else
            {
                _btnDownload.Enabled = false;
                LayoutDownloadPage();
            }
            SetStatus(count == 1 ? "Download started." : "Queued " + count + " downloads.", T.Dim);
        }

        private void ServiceChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate { UpdateQueueUi(); });
            }
            catch { }
        }

        private void ServiceJobCompleted(HistoryEntry h)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    Store.AddHistory(h);
                    if (_historyList != null) { _historyList.Items = Store.History; _historyList.Refresh2(); }
                    RefreshHistory();
                    SetStatus("Saved: " + Path.GetFileName(h.FilePath), T.Success);
                });
            }
            catch { }
        }

        private void ScrollQueueToActive()
        {
            int idx = -1;
            for (int i = 0; i < _svc.Jobs.Count; i++)
            {
                JobState st = _svc.Jobs[i].State;
                if (st == JobState.Downloading || st == JobState.Converting) { idx = i; break; }
            }
            if (idx < 0)
            {
                for (int i = 0; i < _svc.Jobs.Count; i++)
                    if (_svc.Jobs[i].State == JobState.Queued) { idx = i; break; }
            }
            if (idx >= 0) _queue.EnsureVisible(idx);
        }

        private void UpdateQueueUi()
        {
            _queue.Refresh2();
            ScrollQueueToActive();
            RefreshQueuePage();

            DownloadJob cur = null;
            for (int i = _svc.Jobs.Count - 1; i >= 0; i--)
            {
                DownloadJob j = _svc.Jobs[i];
                if (j.State == JobState.Downloading || j.State == JobState.Converting) { cur = j; break; }
            }
            if (cur != null)
            {
                _progress.Value = cur.Progress;
                string s = "Downloading";
                if (cur.Speed.Length > 0) s += "  " + cur.Speed;
                if (cur.Eta.Length > 0) s += "  \u00B7  ETA " + cur.Eta;
                SetStatus(s, T.Dim);
                _btnCancel.Visible = true;
                _btnDownload.Enabled = false;
            }
            else
            {
                bool anyActive = false;
                int queued = 0;
                for (int i = 0; i < _svc.Jobs.Count; i++)
                {
                    if (_svc.Jobs[i].State == JobState.Queued) queued++;
                    if (_svc.Jobs[i].State == JobState.Downloading) anyActive = true;
                }
                _btnCancel.Visible = false;
                _btnDownload.Enabled = _media != null;
                if (queued == 0 && !anyActive) _progress.Value = 0;
                if (queued > 0) SetStatus(queued + " queued\u2026", T.Dim);
            }
            LayoutFooter(_pageDownload.ClientSize.Width);
        }
        private void DoPaste()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;
                string t = Clipboard.GetText().Trim();
                if (t.Length == 0) return;
                _phActive = false;
                _txtUrl.ForeColor = T.Text;
                _txtUrl.Text = t;
                _txtUrl.Focus();
                _txtUrl.SelectionStart = _txtUrl.Text.Length;
            }
            catch { }
        }

        private string GetUrl()
        {
            if (_phActive) return "";
            if (_txtUrl.Text == PlaceholderText) return "";
            return _txtUrl.Text.Trim();
        }

        private void SetStatus(string s, Color c)
        {
            if (_lblStatus == null) return;
            _lblStatus.Text = s;
            _lblStatus.ForeColor = c;
        }

        private static string Shorten(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= n ? s : s.Substring(0, n - 1) + "\u2026";
        }

        private void ChangeDownloadFolder()
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Where should downloads go?";
                fbd.SelectedPath = Store.Current.DownloadFolder;
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    Store.Current.DownloadFolder = fbd.SelectedPath;
                    Store.SaveSettings();
                    UpdateFolderChip();
                }
            }
        }

        private void UpdateFolderChip()
        {
            string p = Store.Current.DownloadFolder;
            _btnFolder.Text = p;
            _btnFolder.Width = 220;
            _tip.SetToolTip(_btnFolder, p);
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (Directory.Exists(path)) System.Diagnostics.Process.Start(path);
            }
            catch { }
        }

        // ---------- history page ----------
        private void BuildHistoryPage()
        {
            Label title = MkLabel("History", T.H1, T.Text, T.Bg);
            title.Bounds = new Rectangle(0, 0, 400, 36);
            _pageHistory.Controls.Add(title);

            Label sub = MkLabel("Everything you've downloaded with MPC Grabber.", T.Small, T.Faint, T.Bg);
            sub.Bounds = new Rectangle(2, 38, 600, 18);
            _pageHistory.Controls.Add(sub);

            _btnClearHistory = MkButton("Clear history", null, BtnVariant.Ghost, 118, T.Bg);
            _btnClearHistory.Height = 34;
            _btnClearHistory.Click += delegate { ClearHistory(); };
            _pageHistory.Controls.Add(_btnClearHistory);

            _historyList = new HistoryList();
            _historyList.Items = Store.History;
            _historyList.OpenRequested += delegate(HistoryEntry h) { OpenFile(h); };
            _historyList.FolderRequested += delegate(HistoryEntry h) { RevealInExplorer(h.FilePath); };
            _historyList.RedownloadRequested += delegate(HistoryEntry h) { Redownload(h); };
            _historyList.DeleteRequested += delegate(HistoryEntry h) { DeleteHistory(h); };
            _pageHistory.Controls.Add(_historyList);

            _lblHistEmpty = MkLabel("Nothing here yet \u2014 your downloads will show up in this list.", T.Body, T.Faint, T.Bg);
            _lblHistEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _pageHistory.Controls.Add(_lblHistEmpty);

            _pageHistory.Resize += delegate
            {
                _btnClearHistory.Left = _pageHistory.ClientSize.Width - 118;
                _btnClearHistory.Top = 4;
                _historyList.Bounds = new Rectangle(0, 76, _pageHistory.ClientSize.Width, Math.Max(100, _pageHistory.ClientSize.Height - 76));
                _lblHistEmpty.Bounds = new Rectangle(0, 76, _pageHistory.ClientSize.Width, Math.Max(100, _pageHistory.ClientSize.Height - 76));
            };
        }

        private void RefreshHistory()
        {
            if (_historyList == null) return;
            _historyList.Items = Store.History;
            _historyList.Refresh2();
            bool empty = Store.History.Count == 0;
            _lblHistEmpty.Visible = empty;
            _historyList.Visible = !empty;
            _btnClearHistory.Visible = !empty;
        }

        private void ClearHistory()
        {
            if (Store.History.Count == 0) return;
            if (MessageBox.Show(this, "Remove all history entries?\nFiles on disk are not deleted.", "Clear history",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Store.History.Clear();
            Store.SaveHistory();
            RefreshHistory();
        }

        private void OpenFile(HistoryEntry h)
        {
            try
            {
                if (File.Exists(h.FilePath)) System.Diagnostics.Process.Start(h.FilePath);
                else MessageBox.Show(this, "That file no longer exists:\n" + h.FilePath, "Missing file", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot open", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private static void RevealInExplorer(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                if (File.Exists(path)) System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else if (Directory.Exists(Path.GetDirectoryName(path))) System.Diagnostics.Process.Start(Path.GetDirectoryName(path));
            }
            catch { }
        }

        private void Redownload(HistoryEntry h)
        {
            FormatOption fmt = new FormatOption();
            if (h.Kind == "MP3")
            {
                fmt.AudioBitrate = Store.Current.AudioBitrate;
                fmt.Title = "MP3 " + Store.Current.AudioBitrate;
            }
            else
            {
                fmt.VideoFmt = "bv*+ba/b";
                fmt.Title = "Best";
            }
            _svc.Enqueue(h.Url, h.Title, fmt, Store.Current.DownloadFolder);
            _nav.SelectedIndex = 0;
            ShowPage(0);
            SetStatus("Re-downloading " + Shorten(h.Title, 60), T.Dim);
        }

        private void DeleteHistory(HistoryEntry h)
        {
            Store.History.Remove(h);
            Store.SaveHistory();
            RefreshHistory();
        }

        // ---------- settings page ----------
        private void BuildSettingsPage()
        {
            Label title = MkLabel("Settings", T.H1, T.Text, T.Bg);
            title.Bounds = new Rectangle(0, 0, 400, 36);
            _pageSettings.Controls.Add(title);

            Label sub = MkLabel("Everything is stored next to the .exe \u2014 nothing is installed.", T.Small, T.Faint, T.Bg);
            sub.Bounds = new Rectangle(2, 38, 700, 18);
            _pageSettings.Controls.Add(sub);

            // --- download folder ---
            RoundedPanel card = new RoundedPanel();
            card.Fill = T.Card;
            card.Radius = 12;
            card.Bounds = new Rectangle(0, 78, 760, 92);
            _pageSettings.Controls.Add(card);

            Label lbl1 = MkLabel("Download folder", T.H3, T.Text, T.Card);
            lbl1.Bounds = new Rectangle(18, 12, 300, 20);
            card.Controls.Add(lbl1);

            _lblSettFolder = MkLabel("", T.Small, T.Dim, T.Card);
            _lblSettFolder.Bounds = new Rectangle(18, 38, 480, 18);
            card.Controls.Add(_lblSettFolder);

            _btnSettChange = MkButton("Change", null, BtnVariant.Soft, 90, T.Card);
            _btnSettChange.Height = 32;
            _btnSettChange.Click += delegate { ChangeDownloadFolder(); RefreshSettings(); };
            card.Controls.Add(_btnSettChange);

            _btnSettOpenFolder = MkButton("Open folder", "\uE8B7", BtnVariant.Ghost, 130, T.Card);
            _btnSettOpenFolder.Height = 32;
            _btnSettOpenFolder.Click += delegate { OpenFolder(Store.Current.DownloadFolder); };
            card.Controls.Add(_btnSettOpenFolder);

            // --- defaults ---
            RoundedPanel cardDefaults = new RoundedPanel();
            cardDefaults.Fill = T.Card;
            cardDefaults.Radius = 12;
            cardDefaults.Bounds = new Rectangle(0, 182, 760, 150);
            _pageSettings.Controls.Add(cardDefaults);

            Label lblVideo = MkLabel("Default video quality", T.H3, T.Text, T.Card);
            lblVideo.Bounds = new Rectangle(18, 12, 300, 20);
            cardDefaults.Controls.Add(lblVideo);

            Label lblVideoHint = MkLabel("preselected after every fetch", T.Tiny, T.Faint, T.Card);
            lblVideoHint.TextAlign = ContentAlignment.MiddleRight;
            lblVideoHint.Bounds = new Rectangle(540, 13, 200, 18);
            cardDefaults.Controls.Add(lblVideoHint);

            _videoBtns = new FlatButton[_videoVals.Length];
            for (int i = 0; i < _videoVals.Length; i++)
            {
                string v = _videoVals[i];
                FlatButton b = MkButton(i == 0 ? "Best" : v + "p", null, BtnVariant.Soft, 92, T.Card);
                b.Height = 30;
                b.Font = T.SmallB;
                b.Left = 18 + i * 100;
                b.Top = 38;
                b.Click += delegate
                {
                    Store.Current.VideoQuality = v;
                    Store.SaveSettings();
                    UpdateSettingPills();
                };
                cardDefaults.Controls.Add(b);
                _videoBtns[i] = b;
            }

            Label lblAudio = MkLabel("Default MP3 bitrate", T.H3, T.Text, T.Card);
            lblAudio.Bounds = new Rectangle(18, 78, 300, 20);
            cardDefaults.Controls.Add(lblAudio);

            Label lblAudioHint = MkLabel("used when you pick an MP3 format", T.Tiny, T.Faint, T.Card);
            lblAudioHint.TextAlign = ContentAlignment.MiddleRight;
            lblAudioHint.Bounds = new Rectangle(540, 79, 200, 18);
            cardDefaults.Controls.Add(lblAudioHint);

            _audioBtns = new FlatButton[_audioVals.Length];
            for (int i = 0; i < _audioVals.Length; i++)
            {
                int v = _audioVals[i];
                FlatButton b = MkButton(v + " kbps", null, BtnVariant.Soft, 100, T.Card);
                b.Height = 30;
                b.Font = T.SmallB;
                b.Left = 18 + i * 108;
                b.Top = 104;
                b.Click += delegate
                {
                    Store.Current.AudioBitrate = v;
                    Store.SaveSettings();
                    UpdateSettingPills();
                };
                cardDefaults.Controls.Add(b);
                _audioBtns[i] = b;
            }

            // --- cookies ---
            RoundedPanel card2 = new RoundedPanel();
            card2.Fill = T.Card;
            card2.Radius = 12;
            card2.Bounds = new Rectangle(0, 344, 760, 108);
            _pageSettings.Controls.Add(card2);

            Label lbl3 = MkLabel("Cookies", T.H3, T.Text, T.Card);
            lbl3.Bounds = new Rectangle(18, 12, 300, 20);
            card2.Controls.Add(lbl3);

            _lblCookies = MkLabel("", T.Small, T.Dim, T.Card);
            _lblCookies.Bounds = new Rectangle(18, 38, 700, 62);
            card2.Controls.Add(_lblCookies);

            // --- history ---
            RoundedPanel card3 = new RoundedPanel();
            card3.Fill = T.Card;
            card3.Radius = 12;
            card3.Bounds = new Rectangle(0, 464, 760, 80);
            _pageSettings.Controls.Add(card3);

            Label lbl4 = MkLabel("History", T.H3, T.Text, T.Card);
            lbl4.Bounds = new Rectangle(18, 12, 300, 20);
            card3.Controls.Add(lbl4);

            Label lbl5 = MkLabel("Removes the download list only \u2014 the files stay on disk.", T.Small, T.Dim, T.Card);
            lbl5.Bounds = new Rectangle(18, 36, 460, 18);
            card3.Controls.Add(lbl5);

            _btnClearHistory2 = MkButton("Clear history", null, BtnVariant.Ghost, 118, T.Card);
            _btnClearHistory2.Height = 30;
            _btnClearHistory2.Top = 25;
            _btnClearHistory2.Click += delegate { ClearHistory(); };
            card3.Controls.Add(_btnClearHistory2);

            RoundedPanel card4 = new RoundedPanel();
            card4.Fill = T.Card;
            card4.Radius = 12;
            card4.Bounds = new Rectangle(0, 556, 760, 96);
            _pageSettings.Controls.Add(card4);

            Label lblUpd = MkLabel("Updates", T.H3, T.Text, T.Card);
            lblUpd.Bounds = new Rectangle(18, 12, 300, 20);
            card4.Controls.Add(lblUpd);

            _chkUpdates = new ToggleBox();
            _chkUpdates.Text = "Check GitHub for a newer release on launch";
            _chkUpdates.Bounds = new Rectangle(18, 38, 430, 22);
            _chkUpdates.Checked = Store.Current.CheckUpdates;
            _chkUpdates.CheckedChanged += delegate { Store.Current.CheckUpdates = _chkUpdates.Checked; Store.SaveSettings(); };
            card4.Controls.Add(_chkUpdates);

            _lblUpdateStatus = MkLabel("", T.Tiny, T.Faint, T.Card);
            _lblUpdateStatus.Bounds = new Rectangle(18, 66, 430, 18);
            card4.Controls.Add(_lblUpdateStatus);

            _btnCheckNow = MkButton("Check now", null, BtnVariant.Ghost, 110, T.Card);
            _btnCheckNow.Height = 30;
            _btnCheckNow.Top = 34;
            _btnCheckNow.Click += delegate { CheckForUpdates(true); };
            card4.Controls.Add(_btnCheckNow);

            Label about = MkLabel("", T.Small, T.Faint, T.Bg);
            about.Name = "about";
            about.Bounds = new Rectangle(4, 668, 760, 50);
            _pageSettings.Controls.Add(about);

            _pageSettings.Resize += delegate
            {
                int w = Math.Min(760, _pageSettings.ClientSize.Width);
                card4.Width = w;
                _btnCheckNow.Left = w - 128;
                card.Width = w;
                cardDefaults.Width = w;
                card2.Width = w;
                card3.Width = w;
                _lblSettFolder.Width = Math.Max(200, w - 280);
                _lblCookies.Width = Math.Max(200, w - 36);
                lblVideoHint.Left = w - 220;
                lblAudioHint.Left = w - 220;
                _btnSettChange.Left = w - 108;
                _btnSettOpenFolder.Left = w - 108 - 140;
                _btnClearHistory2.Left = w - 136;
                about.Width = _pageSettings.ClientSize.Width;
            };
        }

        private void UpdateSettingPills()
        {
            for (int i = 0; i < _videoBtns.Length; i++)
                _videoBtns[i].Variant = Store.Current.VideoQuality == _videoVals[i] ? BtnVariant.Primary : BtnVariant.Soft;
            for (int i = 0; i < _audioBtns.Length; i++)
                _audioBtns[i].Variant = Store.Current.AudioBitrate == _audioVals[i] ? BtnVariant.Primary : BtnVariant.Soft;
        }

        private void RefreshSettings()
        {
            _lblSettFolder.Text = Store.Current.DownloadFolder;
            string cookies = YtDlp.FindCookies();
            if (cookies != null)
            {
                _lblCookies.Text = "Using " + Path.GetFileName(cookies) + "\r\n" + cookies + "\r\nEdit or replace that file to update your login cookies.";
                _lblCookies.ForeColor = T.Dim;
            }
            else
            {
                _lblCookies.Text = "No cookies file found next to the .exe.\r\nCreate a cookies.txt (Netscape format) there if a video needs you to be signed in.";
                _lblCookies.ForeColor = T.Warn;
            }
            UpdateSettingPills();
            if (_chkUpdates != null) _chkUpdates.Checked = Store.Current.CheckUpdates;
            if (_lblUpdateStatus != null && _lblUpdateStatus.Text.Length == 0) _lblUpdateStatus.Text = "current build v" + Updater.CurrentShort;
            Control[] found = _pageSettings.Controls.Find("about", true);
            if (found.Length > 0)
            {
                found[0].Text = "yt-dlp " + _ytVersion + (YtDlp.HasFfmpeg ? "  \u00B7  ffmpeg found" : "  \u00B7  ffmpeg NOT found") +
                                "\r\nHistory and settings live beside this .exe (portable \u2014 no registry, no AppData).";
            }
        }

        private string _ytVersion = "";

        // ---------- channels page ----------
        private void BuildChannelsPage()
        {
            Label title = MkLabel("Channels", T.H1, T.Text, T.Bg);
            title.Bounds = new Rectangle(0, 0, 400, 36);
            _pageChannels.Controls.Add(title);

            Label sub = MkLabel("Load a channel, pick a timeframe, tick what you want.", T.Small, T.Faint, T.Bg);
            sub.Bounds = new Rectangle(2, 38, 700, 18);
            _pageChannels.Controls.Add(sub);

            _chanInput = new RoundedPanel();
            _chanInput.Radius = 12;
            _chanInput.Fill = T.Card;
            _chanInput.BorderColor = T.Border;
            _chanInput.BackColor = T.Bg;
            _pageChannels.Controls.Add(_chanInput);

            Label linkIcon = MkLabel("\uE716", T.Glyph, T.Faint, T.Card);
            linkIcon.Bounds = new Rectangle(14, 0, 24, 50);
            linkIcon.TextAlign = ContentAlignment.MiddleCenter;
            _chanInput.Controls.Add(linkIcon);

            _txtChannel = new TextBox();
            _txtChannel.BorderStyle = BorderStyle.None;
            _txtChannel.BackColor = T.Card;
            _txtChannel.ForeColor = T.Faint;
            _txtChannel.Font = new Font("Segoe UI", 10.5f);
            _txtChannel.Text = ChannelPlaceholder;
            _txtChannel.Bounds = new Rectangle(44, 15, 300, 22);
            _txtChannel.GotFocus += delegate
            {
                if (_chanPhActive) { _chanPhActive = false; _txtChannel.Text = ""; _txtChannel.ForeColor = T.Text; }
                _chanInput.BorderColor = T.Accent;
                _chanInput.Invalidate();
            };
            _txtChannel.LostFocus += delegate
            {
                if (_txtChannel.Text.Trim().Length == 0)
                {
                    _chanPhActive = true;
                    _txtChannel.Text = ChannelPlaceholder;
                    _txtChannel.ForeColor = T.Faint;
                }
                _chanInput.BorderColor = T.Border;
                _chanInput.Invalidate();
            };
            _txtChannel.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BtnLoadChannel_Click(null, null); }
            };
            _txtChannel.TextChanged += delegate
            {
                if (_chanPhActive && _txtChannel.Text != ChannelPlaceholder) { _chanPhActive = false; _txtChannel.ForeColor = T.Text; }
            };
            _chanInput.Controls.Add(_txtChannel);

            _btnChanPaste = MkButton("Paste", null, BtnVariant.Ghost, 76, T.Card);
            _btnChanPaste.Height = 34;
            _btnChanPaste.Click += delegate { DoPasteChannel(); };
            _chanInput.Controls.Add(_btnChanPaste);

            _btnLoadChannel = MkButton("Fetch", null, BtnVariant.Primary, 92, T.Card);
            _btnLoadChannel.Height = 34;
            _btnLoadChannel.Click += BtnLoadChannel_Click;
            _chanInput.Controls.Add(_btnLoadChannel);

            _btnChan24 = MkButton("Last 24 hr", null, BtnVariant.Soft, 100, T.Bg);
            _btnChan7 = MkButton("7 days", null, BtnVariant.Soft, 92, T.Bg);
            _btnChan30 = MkButton("30 days", null, BtnVariant.Soft, 92, T.Bg);
            FlatButton[] radios = { _btnChan24, _btnChan7, _btnChan30 };
            int[] dayVals = { 1, 7, 30 };
            for (int i = 0; i < 3; i++)
            {
                FlatButton b = radios[i];
                int d = dayVals[i];
                b.Height = 28;
                b.Font = T.SmallB;
                b.Top = 134;
                b.Left = i * 104;
                b.Click += delegate
                {
                    if (_channelDays == d) return;
                    _channelDays = d;
                    Store.Current.ChannelDays = d;
                    Store.SaveSettings();
                    UpdateChannelRadios();
                };
                _pageChannels.Controls.Add(b);
            }

            _lblChanHint = MkLabel("videos first, then shorts", T.Tiny, T.Faint, T.Bg);
            _lblChanHint.Bounds = new Rectangle(320, 138, 520, 18);
            _pageChannels.Controls.Add(_lblChanHint);

            _lblChanSaved = MkLabel("SAVED CHANNELS", T.Tiny, T.Faint, T.Bg);
            _pageChannels.Controls.Add(_lblChanSaved);

            _chanPicker = new ChannelPicker();
            _chanPicker.SelectionChanged += delegate
            {
                if (_chanPicker.SelectedIndex < 0 || _chanPicker.SelectedIndex >= Store.Channels.Count) return;
                SavedChannel sc = Store.Channels[_chanPicker.SelectedIndex];
                _chanPhActive = false;
                _txtChannel.ForeColor = T.Text;
                _txtChannel.Text = sc.Url;
                BtnLoadChannel_Click(null, null);
            };
            _pageChannels.Controls.Add(_chanPicker);

            _lblChanHeader = MkLabel("", T.BodyB, T.Dim, T.Bg);
            _pageChannels.Controls.Add(_lblChanHeader);

            _btnChanAll = MkButton("All", null, BtnVariant.Ghost, 54, T.Bg);
            _btnChanAll.Height = 24;
            _btnChanAll.Font = T.Small;
            _btnChanAll.Click += delegate { _chanList.SetAll(true); UpdateChanCount(); };
            _pageChannels.Controls.Add(_btnChanAll);

            _btnChanNone = MkButton("None", null, BtnVariant.Ghost, 58, T.Bg);
            _btnChanNone.Height = 24;
            _btnChanNone.Font = T.Small;
            _btnChanNone.Click += delegate { _chanList.SetAll(false); UpdateChanCount(); };
            _pageChannels.Controls.Add(_btnChanNone);

            _chanList = new ChannelVideoList();
            _chanList.Changed += delegate { UpdateChanCount(); };
            _pageChannels.Controls.Add(_chanList);

            _btnChanDownload = new DownloadButton();
            _btnChanDownload.Label = "Download";
            _btnChanDownload.Enabled = false;
            _btnChanDownload.Click += BtnChanDownload_Click;
            _pageChannels.Controls.Add(_btnChanDownload);

            _pageChannels.Resize += delegate { LayoutChannelsPage(); };
            _channelDays = Store.Current.ChannelDays;
            UpdateChannelRadios();
            UpdateChanCount();
        }

        private void LayoutChannelsPage()
        {
            if (_pageChannels == null || _chanInput == null) return;
            int w = _pageChannels.ClientSize.Width;
            int h = _pageChannels.ClientSize.Height;

            _chanInput.Bounds = new Rectangle(0, 74, w - 134, 50);
            int cardW = w - 134;
            _btnLoadChannel.Left = cardW - 8 - 92;
            _btnLoadChannel.Top = 8;
            _btnChanPaste.Left = _btnLoadChannel.Left - 8 - 76;
            _btnChanPaste.Top = 8;
            _txtChannel.Width = Math.Max(120, _btnChanPaste.Left - 44 - 12);
            int top = 178;
            _lblChanSaved.Bounds = new Rectangle(0, top, 220, 18);
            _chanPicker.Bounds = new Rectangle(0, top + 22, 218, Math.Max(80, h - top - 30));

            int rx = 238;
            int rw = Math.Max(200, w - rx);
            _lblChanHeader.Bounds = new Rectangle(rx, top, rw - 132, 18);
            _btnChanAll.Left = w - 122;
            _btnChanAll.Top = top - 2;
            _btnChanNone.Left = w - 62;
            _btnChanNone.Top = top - 2;

            _chanList.Bounds = new Rectangle(rx, top + 26, rw, Math.Max(120, h - top - 26 - 58));
            _chanList.RefreshLayouts();

            _btnChanDownload.Bounds = new Rectangle(w - 194, h - 52, 194, 48);
        }

        private void UpdateChannelRadios()
        {
            _btnChan24.Variant = _channelDays == 1 ? BtnVariant.Primary : BtnVariant.Soft;
            _btnChan7.Variant = _channelDays == 7 ? BtnVariant.Primary : BtnVariant.Soft;
            _btnChan30.Variant = _channelDays == 30 ? BtnVariant.Primary : BtnVariant.Soft;
        }

        private string GetChannelInput()
        {
            if (_chanPhActive) return "";
            if (_txtChannel.Text == ChannelPlaceholder) return "";
            return _txtChannel.Text.Trim();
        }

        private void UpdateChanCount()
        {
            if (_chanList == null || _btnChanDownload == null) return;
            int n = _chanList.CheckedCount;
            _btnChanDownload.Count = n;
            _btnChanDownload.Enabled = n > 0;
            if (_channel == null)
            {
                _lblChanHeader.Text = "Load a channel to list recent uploads.";
                return;
            }
            string tf = _channelDays == 1 ? "last 24 hours" : "last " + _channelDays + " days";
            _lblChanHeader.Text = string.Format("{0}  \u00B7  {1} videos  \u00B7  {2} shorts  \u00B7  {3}",
                _channel.Name, _channel.VideoCount, _channel.ShortCount, tf);
        }

        private async void BtnLoadChannel_Click(object sender, EventArgs e)
        {
            if (_loadingChannel) return;
            string input = GetChannelInput();
            if (input.Length == 0)
            {
                _lblChanHint.Text = "Paste a channel URL or @handle first.";
                _lblChanHint.ForeColor = T.Warn;
                return;
            }
            _loadingChannel = true;
            _loading.ShowOverlay("Loading channel\u2026");
            _btnLoadChannel.Enabled = false;
            _btnLoadChannel.Text = "Loading\u2026";
            _lblChanHint.Text = "Reading channel\u2026";
            _lblChanHint.ForeColor = T.Faint;
            try
            {
                ChannelInfo info = await YtDlp.FetchChannelAsync(input, _channelDays);
                _channel = info;
                Store.UpsertChannel(info.Id, info.Name, info.Url);
                MarkChannelDownloads(info);
                _chanPicker.SetItems(Store.Channels);
                _chanPicker.SelectChannel(info.Id);
                _chanList.SetItems(info.Videos);
                UpdateChanCount();
                _lblChanHint.ForeColor = T.Faint;
                _lblChanHint.Text = info.Videos.Count == 0
                    ? "No uploads in this timeframe."
                    : "Uses your default video quality from Settings.";
            }
            catch (Exception ex)
            {
                _channel = null;
                _chanList.SetItems(new List<ChannelVideo>());
                _lblChanHeader.Text = "Could not load that channel.";
                _lblChanHint.Text = Shorten(ex.Message, 160);
                _lblChanHint.ForeColor = T.Danger;
                UpdateChanCount();
            }
            finally
            {
                _loadingChannel = false;
                _loading.HideOverlay();
                _btnLoadChannel.Enabled = true;
                _btnLoadChannel.Text = "Fetch";
                LayoutChannelsPage();
            }
        }

        private void MarkChannelDownloads(ChannelInfo info)
        {
            for (int i = 0; i < info.Videos.Count; i++)
            {
                ChannelVideo v = info.Videos[i];
                v.Downloaded = false;
                for (int h = 0; h < Store.History.Count; h++)
                {
                    HistoryEntry e = Store.History[h];
                    if (string.Equals(e.ChannelId, info.Id, StringComparison.OrdinalIgnoreCase) && e.Url == v.Url)
                    {
                        v.Downloaded = true;
                        break;
                    }
                }
                v.Checked = !v.Downloaded;
            }
        }

        private FormatOption ChannelFormat()
        {
            string q = Store.Current.VideoQuality;
            FormatOption fo = new FormatOption();
            if (string.IsNullOrEmpty(q) || q == "best")
            {
                fo.VideoFmt = "bv*+ba/b";
                fo.Title = "Best";
            }
            else
            {
                fo.VideoFmt = "bv*[height<=" + q + "]+ba/b[height<=" + q + "]/b";
                fo.Title = q + "p";
            }
            return fo;
        }

        private void BtnChanDownload_Click(object sender, EventArgs e)
        {
            if (_channel == null) return;
            FormatOption fmt = ChannelFormat();
            string template = "%(channel)s - %(title)s - %(upload_date>%m%d%y)s-%(upload_time>%H%M)s.%(ext)s";
            int count = 0;
            for (int i = 0; i < _channel.Videos.Count; i++)
            {
                ChannelVideo v = _channel.Videos[i];
                if (!v.Checked || v.Downloaded) continue;
                _svc.Enqueue(v.Url, v.Title, fmt, Store.Current.DownloadFolder, _channel.Id, _channel.Name, template);
                v.Checked = false;
                count++;
            }
            if (count == 0) return;
            UpdateChanCount();
            if (count > 1)
            {
                _nav.SelectedIndex = 4;
                ShowPage(4);
            }
        }

        // ---------- queue page ----------
        private void BuildQueuePage()
        {
            Label title = MkLabel("Queue", T.H1, T.Text, T.Bg);
            title.Bounds = new Rectangle(0, 0, 400, 36);
            _pageQueue.Controls.Add(title);

            _lblQueueSummary = MkLabel("Nothing queued yet.", T.Small, T.Faint, T.Bg);
            _lblQueueSummary.Bounds = new Rectangle(2, 38, 620, 18);
            _pageQueue.Controls.Add(_lblQueueSummary);

            _btnQueueClear = MkButton("Clear finished", null, BtnVariant.Ghost, 126, T.Bg);
            _btnQueueClear.Height = 34;
            _btnQueueClear.Top = 4;
            _btnQueueClear.Click += delegate
            {
                _svc.ClearFinished();
                LayoutQueuePage();
                UpdateQueueUi();
            };
            _pageQueue.Controls.Add(_btnQueueClear);

            _btnQueueCancel = MkButton("Cancel current", null, BtnVariant.Ghost, 132, T.Bg);
            _btnQueueCancel.Height = 34;
            _btnQueueCancel.Top = 4;
            _btnQueueCancel.Click += delegate { _svc.CancelCurrent(); };
            _pageQueue.Controls.Add(_btnQueueCancel);

            _queue = new QueueList();
            _queue.Jobs = _svc.Jobs;
            _pageQueue.Controls.Add(_queue);

            _pageQueue.Resize += delegate { LayoutQueuePage(); };
        }

        private void LayoutQueuePage()
        {
            if (_pageQueue == null) return;
            int w = _pageQueue.ClientSize.Width;
            int h = _pageQueue.ClientSize.Height;
            _btnQueueCancel.Left = w - 132;
            _btnQueueClear.Left = w - 132 - 8 - 126;
            _queue.Bounds = new Rectangle(0, 76, w, Math.Max(120, h - 84));
            bool any = _svc.Jobs.Count > 0;
            _queue.Visible = any;
            _btnQueueClear.Visible = any;
            ScrollQueueToActive();
        }

        private void RefreshQueuePage()
        {
            int total = _svc.Jobs.Count;
            int done = 0, queued = 0, failed = 0, running = 0;
            for (int i = 0; i < total; i++)
            {
                JobState st = _svc.Jobs[i].State;
                if (st == JobState.Done) done++;
                else if (st == JobState.Queued) queued++;
                else if (st == JobState.Failed || st == JobState.Cancelled) failed++;
                else running++;
            }
            if (total == 0)
                _lblQueueSummary.Text = "Nothing queued yet \u2014 start a download from the Download page.";
            else
                _lblQueueSummary.Text = string.Format("{0} item{1}  \u00B7  {2} done  \u00B7  {3} downloading  \u00B7  {4} queued{5}",
                    total, total == 1 ? "" : "s", done, running, queued, failed > 0 ? "  \u00B7  " + failed + " failed" : "");
            _btnQueueCancel.Enabled = running > 0;
        }
        // ---------- lifecycle ----------
        private async void MainForm_Load(object sender, EventArgs e)
        {
            Store.Load();
            string[] cmdArgs = Environment.GetCommandLineArgs();
            int chIdx = Array.IndexOf(cmdArgs, "--channel");
            if (chIdx >= 0 && chIdx + 1 < cmdArgs.Length) _startupChannel = cmdArgs[chIdx + 1];
            if (_startupChannel.Length > 0) { _nav.SelectedIndex = 1; ShowPage(1); }
            UpdateFolderChip();
            RefreshHistory();
            RefreshSettings();
            UpdateCookiePill();
            LayoutDownloadPage();
            if (_startupUrl.Length > 0)
            {
                _phActive = false;
                _txtUrl.ForeColor = T.Text;
                _txtUrl.Text = _startupUrl;
                _txtUrl.SelectionStart = _txtUrl.Text.Length;
                _txtUrl.SelectionLength = 0;
                BtnFetch_Click(null, null);
            }
            else
            {
                _txtUrl.Focus();
            }

            if (_startupChannel.Length > 0)
            {
                _chanPhActive = false;
                _txtChannel.ForeColor = T.Text;
                _txtChannel.Text = _startupChannel;
                BtnLoadChannel_Click(null, null);
            }
            _ytVersion = await YtDlp.VersionAsync();
            if (Store.Current.CheckUpdates) CheckForUpdates(false);
            RefreshSettings();
        }

        private void SeedDemoQueue()
        {
            for (int i = 0; i < 12; i++)
            {
                DownloadJob j = new DownloadJob();
                j.Title = "Demo video " + (i + 1) + " \u2014 a fairly long title to check the ellipsis behaviour";
                j.QualityLabel = i % 2 == 0 ? "1080p" : "MP3 320";
                if (i < 6) { j.State = JobState.Done; j.Progress = 1; j.Status = "Done"; }
                else if (i == 6) { j.State = JobState.Downloading; j.Progress = 0.42; j.Status = "Downloading"; j.Speed = "2.4MiB/s"; j.Eta = "00:42"; }
                else { j.State = JobState.Queued; j.Status = "Queued"; }
                _svc.Jobs.Add(j);
            }
        }
        private static bool LooksLikeChannelUrl(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            string t = s.Trim().ToLowerInvariant();
            if (t.StartsWith("@")) return true;
            if (t.Contains("/watch") || t.Contains("watch?v=") || t.Contains("list=") || t.Contains("youtu.be/")) return false;
            return t.Contains("youtube.com/@") || t.Contains("youtube.com/channel/") || t.Contains("youtube.com/c/") || t.Contains("youtube.com/user/");
        }

        private void DoPasteChannel()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;
                string t = Clipboard.GetText().Trim();
                if (t.Length == 0) return;
                _chanPhActive = false;
                _txtChannel.ForeColor = T.Text;
                _txtChannel.Text = t;
                _txtChannel.Focus();
                _txtChannel.SelectionStart = _txtChannel.Text.Length;
            }
            catch { }
        }

        private async void CheckForUpdates(bool manual)
        {
            if (manual && _lblUpdateStatus != null) _lblUpdateStatus.Text = "Checking GitHub\u2026";
            try
            {
                ReleaseInfo r = await Updater.CheckAsync();
                if (r == null)
                {
                    if (manual) _lblUpdateStatus.Text = "Could not read the release feed.";
                    return;
                }
                if (!Updater.IsNewer(r))
                {
                    if (_lblUpdateStatus != null) _lblUpdateStatus.Text = "Up to date \u00B7 v" + Updater.CurrentShort;
                    if (manual)
                        MessageBox.Show(this, "You're running the latest version (v" + Updater.CurrentShort + ").",
                            "No updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (_lblUpdateStatus != null) _lblUpdateStatus.Text = "Update available \u00B7 " + r.Tag;
                string msg = "A newer version is available: " + r.Tag +
                             "\n\nDownload it now? MPC Grabber will close and restart with the new build.";
                if (MessageBox.Show(this, msg, "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                    return;

                if (_lblUpdateStatus != null) _lblUpdateStatus.Text = "Downloading " + r.Tag + "\u2026";
                string err;
                Updater.DownloadAndRestart(r, out err);
                if (!string.IsNullOrEmpty(err))
                {
                    if (_lblUpdateStatus != null) _lblUpdateStatus.Text = "Update failed.";
                    MessageBox.Show(this, "Could not update: " + err, "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Application.Exit();
            }
            catch (Exception ex)
            {
                YtDlp.Log("UPDATE CHECK FAILED: " + ex.Message);
                if (manual && _lblUpdateStatus != null) _lblUpdateStatus.Text = "Update check failed.";
            }
        }
        private void UpdateCookiePill()
        {
            string c = YtDlp.FindCookies();
            if (c != null)
            {
                _cookiePill.Text = "cookies \u2713";
                _cookiePill.DotColor = T.Success;
                _cookiePill.TextColor = T.Dim;
                _tip.SetToolTip(_cookiePill, c);
            }
            else
            {
                _cookiePill.Text = "no cookies";
                _cookiePill.DotColor = T.Warn;
                _cookiePill.TextColor = T.Dim;
                _tip.SetToolTip(_cookiePill, "Add cookies.txt next to the exe for signed-in videos.");
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (_pageDownload.Visible) BtnDownload_Click(null, null);
            }
            else if (e.Control && e.KeyCode == Keys.L)
            {
                e.SuppressKeyPress = true;
                _txtUrl.Focus();
                _txtUrl.SelectionStart = _txtUrl.Text.Length;
            }
            else if (e.KeyCode == Keys.Escape && _svc.Busy)
            {
                _svc.CancelCurrent();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _svc.Shutdown();
        }
    }
}

