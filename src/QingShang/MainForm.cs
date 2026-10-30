using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using QingShang.Core;
using QingShang.UI;

namespace QingShang
{
    /// <summary>
    /// 主窗口。整窗 GDI+ 自绘 + 手写命中测试，
    /// 没有原生控件参与，所以外观、动画、圆角都能完全控住。
    /// </summary>
    public class MainForm : Form
    {
        private const int SpectrumBars = 56;

        private readonly PlayerEngine _engine = new PlayerEngine();
        private readonly AppSettings _settings;
        private readonly float _scale;

        private int S(float value) => (int)Math.Round(value * _scale);
        private float SF(float value) => value * _scale;
        private int RowHeight => S(54);

        // ---------- 布局 ----------
        private Rectangle _titleRect;
        private Rectangle _bodyRect;
        private Rectangle _sidebarRect;
        private Rectangle _listHeaderRect;
        private Rectangle _listRect;
        private Rectangle _heroRect;
        private Rectangle _discRect;
        private Rectangle _spectrumRect;
        private Rectangle _lyricRect;
        private Rectangle _controlRect;
        private Rectangle _seekRect;
        private Rectangle _timeNowRect;
        private Rectangle _timeTotalRect;
        private Rectangle _volumeRect;

        private readonly List<UIButton> _buttons = new List<UIButton>();

        // ---------- 交互状态 ----------
        private UIButton _hoverButton;
        private UIButton _pressedButton;
        private bool _draggingSeek;
        private bool _draggingVolume;
        private double _seekRatio;
        private double _volumeRatio = 0.8;
        private bool _seekPreviewing;

        private bool _draggingWindow;
        private Point _dragOrigin;
        private Point _windowOrigin;

        private int _selectedIndex = -1;
        private int _listScroll;
        private int _hoverRow = -1;
        private int _lastClickRow = -1;
        private DateTime _lastClickAt = DateTime.MinValue;

        private bool _miniMode;
        private Rectangle _miniPlayRect;
        private Rectangle _miniSeekRect;

        // ---------- 动画 ----------
        private readonly Timer _anim;
        private readonly float[] _spectrumTarget = new float[SpectrumBars];
        private readonly float[] _spectrumLevel = new float[SpectrumBars];
        private readonly float[] _spectrumSeed = new float[SpectrumBars];
        private readonly Random _random = new Random();
        private double _animTime;
        private float _discAngle;
        private Color _discColorA = Theme.Cyan;
        private Color _discColorB = Theme.CyanDeep;
        private string _discKey = string.Empty;

        // ---------- 歌词 ----------
        private Lyrics _lyrics = Lyrics.Empty;
        private int _lyricIndex = -1;
        private double _lyricScroll;
        private double _lyricTarget;

        private string _title = "还没有选歌";
        private string _artist = "把音乐拖进窗口，或点左侧 + 添加";
        private string _meta = string.Empty;
        private string _miniTitle = "清商音乐播放器";
        private string _miniArtist = "把音乐拖进来";

        private readonly string[] _startupArgs;
        private readonly ToolTip _tip = new ToolTip { InitialDelay = 700, ReshowDelay = 200 };

        public MainForm(string[] args)
        {
            _startupArgs = args ?? Array.Empty<string>();
            _settings = AppSettings.Load();
            _scale = DetectScale();
            Theme.Scale = _scale;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            Text = "清商音乐播放器";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(Theme.NormalWidth), S(Theme.NormalHeight));
            BackColor = Theme.Bg1;
            DoubleBuffered = true;
            KeyPreview = true;
            AllowDrop = true;
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(S(760), S(520));

            try
            {
                var stream = typeof(MainForm).Assembly.GetManifestResourceStream("QingShang.app.ico");
                if (stream != null) Icon = new Icon(stream);
            }
            catch
            {
                // 图标取不到不影响使用
            }

            _engine.SetVolume(_settings.Volume);
            _engine.Mode = _settings.Mode;
            _volumeRatio = _engine.Volume;

            for (int i = 0; i < SpectrumBars; i++)
                _spectrumSeed[i] = (float)(_random.NextDouble() * 6.28);

            _anim = new Timer { Interval = 33 };
            _anim.Tick += OnAnimTick;
            _anim.Start();

            WireEngine();
            LayoutAll();

            Load += OnFormLoad;
            FormClosing += OnFormClosing;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        // =====================================================================
        // 生命周期
        // =====================================================================

        private void OnFormLoad(object sender, EventArgs e)
        {
            ApplyRoundedRegion();
            if (_settings.X >= 0 && _settings.Y >= 0)
            {
                var screen = Screen.FromPoint(new Point(_settings.X, _settings.Y)).WorkingArea;
                if (screen.Contains(new Point(_settings.X + 60, _settings.Y + 20)))
                    Location = new Point(_settings.X, _settings.Y);
            }

            var files = _startupArgs.Where(a => !a.StartsWith("-")).ToArray();
            Diag.Write("startup args = [" + string.Join(" | ", files) + "]");
            if (files.Length > 0)
            {
                int count = _engine.AddFiles(files, true);
                Diag.Write("added from args: " + count);
                if (count == 0) LoadMusicLibraryIfEmpty();
            }
            else
            {
                LoadMusicLibraryIfEmpty();
            }

            Diag.Write("playlist now: " + _engine.Items.Count + " tracks");
            foreach (var t in _engine.Items.Take(5))
                Diag.Write("  - " + t.FilePath + "  title=" + t.Title + " artist=" + t.Artist);

            if (_startupArgs.Any(a => string.Equals(a, "--mini", StringComparison.OrdinalIgnoreCase)))
                ToggleMini();

            RefreshTransport();
        }

        private void LoadMusicLibraryIfEmpty()
        {
            if (_engine.Items.Count > 0) return;
            try
            {
                var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                if (Directory.Exists(music))
                    _engine.AddFiles(new[] { music }, true);
            }
            catch
            {
                // 忽略
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _settings.Volume = _engine.VolumeRaw;
            _settings.Muted = _engine.IsMuted;
            _settings.Mode = _engine.Mode;
            _settings.X = Location.X;
            _settings.Y = Location.Y;
            _settings.Save();

            _anim.Stop();
            _engine.Dispose();
        }

        private void ApplyRoundedRegion()
        {
            using (var path = Theme.RoundRect(new Rectangle(0, 0, Width, Height), SF(12)))
                Region = new Region(path);
        }

        private static float DetectScale()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    return Math.Max(0.8f, Math.Min(3f, g.DpiX / 96f));
            }
            catch
            {
                return 1f;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) return;
            LayoutAll();
            ApplyRoundedRegion();
            Invalidate();
        }

        // =====================================================================
        // 布局
        // =====================================================================

        private void LayoutAll()
        {
            _buttons.Clear();

            int w = ClientSize.Width;
            int h = ClientSize.Height;

            if (_miniMode)
            {
                LayoutMini(w, h);
                return;
            }

            _titleRect = new Rectangle(0, 0, w, S(Theme.TitleHeight));

            int controlH = S(Theme.ControlHeight);
            _bodyRect = new Rectangle(0, _titleRect.Height, w, h - _titleRect.Height - controlH);
            _controlRect = new Rectangle(0, h - controlH, w, controlH);

            // 侧栏
            int sideW = Math.Min(S(Theme.SidebarWidth), Math.Max(S(240), w / 3));
            _sidebarRect = new Rectangle(0, _bodyRect.Y, sideW, _bodyRect.Height);
            _listHeaderRect = new Rectangle(_sidebarRect.X, _sidebarRect.Y, sideW, S(46));
            _listRect = new Rectangle(_sidebarRect.X, _listHeaderRect.Bottom, sideW,
                _sidebarRect.Height - _listHeaderRect.Height);

            // 右侧
            int rightX = _sidebarRect.Right;
            int rightW = w - rightX;
            int lyricH = Math.Min(S(Theme.LyricHeight), _bodyRect.Height / 2);
            _heroRect = new Rectangle(rightX, _bodyRect.Y, rightW, _bodyRect.Height - lyricH);
            _lyricRect = new Rectangle(rightX, _heroRect.Bottom, rightW, lyricH);

            int discSize = Math.Min(S(184), Math.Min(_heroRect.Height - S(96), rightW / 3));
            discSize = Math.Max(S(96), discSize);
            _discRect = new Rectangle(rightX + S(32), _heroRect.Y + S(26), discSize, discSize);

            int infoX = _discRect.Right + S(26);
            int infoW = Math.Max(S(80), _heroRect.Right - infoX - S(30));
            _spectrumRect = new Rectangle(rightX + S(32), _discRect.Bottom + S(26),
                Math.Max(S(80), rightW - S(64)), S(64));

            // 控制条
            int pad = S(24);
            _timeNowRect = new Rectangle(pad, _controlRect.Y + S(12), S(52), S(22));
            _timeTotalRect = new Rectangle(w - pad - S(52), _controlRect.Y + S(12), S(52), S(22));
            _seekRect = new Rectangle(_timeNowRect.Right + S(4), _controlRect.Y + S(12),
                Math.Max(S(80), _timeTotalRect.X - _timeNowRect.Right - S(8)), S(22));

            int btnY = _controlRect.Y + S(44);
            int cx = pad;

            AddButton("prev", new Rectangle(cx, btnY + S(9), S(34), S(34)), "上一首");
            cx += S(34) + S(12);
            AddButton("play", new Rectangle(cx, btnY, S(52), S(52)), "播放 / 暂停（空格）", primary: true);
            cx += S(52) + S(12);
            AddButton("next", new Rectangle(cx, btnY + S(9), S(34), S(34)), "下一首");
            cx += S(34) + S(16);
            AddButton("mode", new Rectangle(cx, btnY + S(10), S(32), S(32)), "播放模式", toggle: true);

            _volumeRect = new Rectangle(w - pad - S(90), _controlRect.Y + S(59), S(90), S(22));
            AddButton("mute", new Rectangle(_volumeRect.X - S(34), _controlRect.Y + S(55), S(30), S(30)), "静音");

            // 标题栏按钮
            AddButton("close", new Rectangle(w - S(42), S(10), S(30), S(30)), "退出", accent: Theme.Rose);
            AddButton("minimize", new Rectangle(w - S(76), S(10), S(30), S(30)), "最小化");
            AddButton("minibtn", new Rectangle(w - S(110), S(10), S(30), S(30)), "迷你模式（双击标题栏切换）");

            // 侧栏按钮
            int hx = _sidebarRect.Right - S(12) - S(28);
            AddButton("clear", new Rectangle(hx, _listHeaderRect.Y + S(9), S(28), S(28)), "清空列表");
            hx -= S(30);
            AddButton("addFolder", new Rectangle(hx, _listHeaderRect.Y + S(9), S(28), S(28)), "添加文件夹");
            hx -= S(30);
            AddButton("addFile", new Rectangle(hx, _listHeaderRect.Y + S(9), S(28), S(28)), "添加音乐文件");
        }

        private void LayoutMini(int w, int h)
        {
            _miniPlayRect = new Rectangle(w - S(16) - S(28) - S(6) - S(34), S(12), S(34), S(34));
            _miniSeekRect = new Rectangle(S(16), S(56), w - S(32), S(16));

            AddButton("miniplay", _miniPlayRect, "播放 / 暂停", primary: true);
            AddButton("expand", new Rectangle(w - S(16) - S(28), S(15), S(28), S(28)), "回到完整界面");
            AddButton("miniclose", new Rectangle(w - S(16) - S(28) - S(6) - S(34) - S(6) - S(26), S(15), S(26), S(26)),
                "退出", accent: Theme.Rose);
        }

        private void AddButton(string name, Rectangle bounds, string tip,
            bool primary = false, bool toggle = false, Color? accent = null)
        {
            _buttons.Add(new UIButton
            {
                Name = name,
                Bounds = bounds,
                ToolTip = tip,
                Primary = primary,
                IsToggle = toggle,
                Accent = accent ?? Theme.Cyan
            });
        }

        // =====================================================================
        // 绘制
        // =====================================================================

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.SetupQuality(g);

            if (_miniMode)
            {
                DrawMini(g);
                return;
            }

            DrawBackground(g);
            DrawTitleBar(g);
            DrawSidebar(g);
            DrawHero(g);
            DrawLyrics(g);
            DrawControlBar(g);
            DrawBorder(g);
        }

        private void DrawBackground(Graphics g)
        {
            var full = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            using (var brush = Theme.VerticalGradient(full,
                       Color.FromArgb(0x16, 0x1E, 0x29),
                       Color.FromArgb(0x08, 0x0B, 0x10)))
                g.FillRectangle(brush, full);
        }

        private void DrawBorder(Graphics g)
        {
            using (var pen = new Pen(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 1f))
                Theme.DrawRound(g, pen, new RectangleF(0.5f, 0.5f, ClientSize.Width - 1f, ClientSize.Height - 1f), 12f);
        }

        private void DrawTitleBar(Graphics g)
        {
            using (var brush = Theme.VerticalGradient(_titleRect,
                       Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF)))
                g.FillRectangle(brush, _titleRect);

            // logo
            var logo = new RectangleF(S(18), S(12), S(26), S(26));
            using (var brush = Theme.VerticalGradient(logo, Theme.Lighten(Theme.Cyan, 0.15), Theme.CyanDeep))
                Theme.FillRound(g, brush, logo, SF(8));
            DrawIcon(g, "note", new RectangleF(logo.X, logo.Y, logo.Width, logo.Height), Color.FromArgb(0x08, 0x2A, 0x33), SF(15), true);

            var brandFont = Theme.Font(14.5f, FontStyle.Bold);
            Theme.DrawText(g, "清商", brandFont, Theme.TextHi,
                new RectangleF(S(54), S(12), S(120), S(26)));

            Theme.DrawText(g, "音乐播放器", Theme.Font(12.5f), Theme.Alpha(Theme.TextMid, 235),
                new RectangleF(S(54) + TextWidth(g, "清商", brandFont) + S(6), S(13), S(140), S(24)));

            foreach (var b in _buttons)
            {
                if (b.Name == "close" || b.Name == "minimize" || b.Name == "minibtn")
                    DrawButton(g, b, b.Name == "minibtn" ? "mini" : b.Name);
            }
        }

        private float TextWidth(Graphics g, string text, Font font)
        {
            return g.MeasureString(text, font, int.MaxValue, Theme.AlignLeft).Width;
        }

        private void DrawSidebar(Graphics g)
        {
            using (var brush = new SolidBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)))
                g.FillRectangle(brush, _sidebarRect);

            using (var pen = new Pen(Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF), 1f))
                g.DrawLine(pen, _sidebarRect.Right - 0.5f, _sidebarRect.Y, _sidebarRect.Right - 0.5f, _sidebarRect.Bottom);

            Theme.DrawText(g, "播放列表", Theme.Font(13f, FontStyle.Bold), Theme.TextHi,
                new RectangleF(_sidebarRect.X + S(18), _listHeaderRect.Y, S(100), _listHeaderRect.Height));

            var countFont = Theme.Font(11f);
            Theme.DrawText(g, _engine.Items.Count + " 首", countFont, Theme.TextLow,
                new RectangleF(_sidebarRect.X + S(88), _listHeaderRect.Y + S(1), S(80), _listHeaderRect.Height));

            foreach (var b in _buttons)
            {
                if (b.Name == "addFile" || b.Name == "addFolder" || b.Name == "clear")
                    DrawButton(g, b, b.Name == "addFile" ? "note" : b.Name == "addFolder" ? "folder" : "trash");
            }

            DrawPlaylist(g);
        }

        private void DrawPlaylist(Graphics g)
        {
            if (_engine.Items.Count == 0)
            {
                var center = new RectangleF(_listRect.X, _listRect.Y + _listRect.Height / 2f - 60, _listRect.Width, 120);
                DrawIcon(g, "music", new RectangleF(center.X + center.Width / 2 - 24, center.Y, 48, 48),
                    Color.FromArgb(0x30, Theme.TextMid.R, Theme.TextMid.G, Theme.TextMid.B), 48f, false);

                Theme.DrawText(g, "把音乐拖进来", Theme.Font(13f), Theme.TextMid,
                    new RectangleF(center.X, center.Y + 58, center.Width, 24), StringAlignment.Center);
                Theme.DrawText(g, "支持 mp3 / wav / wma / m4a", Theme.Font(11f), Theme.TextLow,
                    new RectangleF(center.X, center.Y + 82, center.Width, 20), StringAlignment.Center);
                return;
            }

            var state = g.Save();
            g.SetClip(_listRect);

            int total = _engine.Items.Count;
            int first = Math.Max(0, _listScroll / RowHeight);
            int visible = _listRect.Height / RowHeight + 2;

            for (int i = first; i < first + visible && i < total; i++)
            {
                int y = _listRect.Y + i * RowHeight - _listScroll;
                if (y > _listRect.Bottom || y + RowHeight < _listRect.Y) continue;
                DrawRow(g, i, y);
            }

            DrawScrollBar(g);
            g.Restore(state);
        }

        private void DrawRow(Graphics g, int index, int y)
        {
            var track = _engine.Items[index];
            var row = new Rectangle(_listRect.X + S(8), y + S(1), _listRect.Width - S(16), RowHeight - S(2));
            bool selected = index == _selectedIndex;
            bool isCurrent = index == _engine.CurrentIndex;
            bool hover = index == _hoverRow;

            if (selected)
            {
                using (var brush = Theme.HorizontalGradient(row,
                           Color.FromArgb(0x38, Theme.Cyan.R, Theme.Cyan.G, Theme.Cyan.B),
                           Color.FromArgb(0x00, Theme.Cyan.R, Theme.Cyan.G, Theme.Cyan.B)))
                    Theme.FillRound(g, brush, row, SF(8));
                using (var brush = new SolidBrush(Theme.Cyan))
                    Theme.FillRound(g, brush, new RectangleF(row.X, row.Y + RowHeight / 2f - SF(10), SF(2.6f), SF(18)), SF(1.3f));
            }
            else if (hover)
            {
                using (var brush = new SolidBrush(Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF)))
                    Theme.FillRound(g, brush, row, SF(8));
            }

            // 序号 / 播放指示
            if (isCurrent)
            {
                DrawPlayingBars(g, row.X + S(20), y + RowHeight / 2f);
            }
            else
            {
                Theme.DrawText(g, (index + 1).ToString("00"), Theme.Font(11f, FontStyle.Regular, true), Theme.TextLow,
                    new RectangleF(row.X + S(6), y, S(28), RowHeight), StringAlignment.Far);
            }

            Color titleColor = isCurrent ? Theme.Cyan : Theme.TextHi;
            var titleFont = Theme.Font(13.2f, isCurrent ? FontStyle.Bold : FontStyle.Regular);
            Theme.DrawText(g, track.DisplayTitle, titleFont, titleColor,
                new RectangleF(row.X + S(42), y + S(9), row.Width - S(96), S(20)));

            Theme.DrawText(g, track.DisplayArtist, Theme.Font(11f), Theme.TextMid,
                new RectangleF(row.X + S(42), y + S(29), row.Width - S(96), S(17)));

            Theme.DrawText(g, track.DurationText, Theme.Font(11f, FontStyle.Regular, true), Theme.TextLow,
                new RectangleF(row.Right - S(52), y, S(44), RowHeight), StringAlignment.Far);
        }

        private void DrawPlayingBars(Graphics g, float cx, float cy)
        {
            using (var brush = new SolidBrush(Theme.Cyan))
            {
                for (int i = 0; i < 3; i++)
                {
                    double phase = _animTime * 3.2 + i * 1.15;
                    float amp = (float)(0.35 + 0.65 * Math.Abs(Math.Sin(phase)));
                    float barH = SF(4) + amp * SF(11);
                    Theme.FillRound(g, brush,
                        new RectangleF(cx - SF(8) + i * SF(6), cy - barH / 2, SF(3), barH), SF(1.5f));
                }
            }
        }

        private void DrawScrollBar(Graphics g)
        {
            int total = _engine.Items.Count;
            int contentH = total * RowHeight;
            if (contentH <= _listRect.Height) return;

            float ratio = _listRect.Height / (float)contentH;
            int barH = Math.Max(S(28), (int)(_listRect.Height * ratio));
            int maxScroll = Math.Max(1, contentH - _listRect.Height);
            int barY = _listRect.Y + (int)((_listRect.Height - barH) * (_listScroll / (float)maxScroll));

            using (var brush = new SolidBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)))
                Theme.FillRound(g, brush, new RectangleF(_listRect.Right - SF(7), barY, SF(3.5f), barH), SF(1.75f));
        }

        private void DrawHero(Graphics g)
        {
            // 左上角柔光
            using (var brush = new GraphicsPath())
            {
                brush.AddEllipse(new RectangleF(_heroRect.X - 120, _heroRect.Y - 90, _heroRect.Width * 0.85f, _heroRect.Height * 1.5f));
                using (var pathGrad = new PathGradientBrush(brush))
                {
                    pathGrad.CenterColor = Color.FromArgb(0x16, Theme.Cyan.R, Theme.Cyan.G, Theme.Cyan.B);
                    pathGrad.SurroundColors = new[] { Color.FromArgb(0x00, Theme.Cyan.R, Theme.Cyan.G, Theme.Cyan.B) };
                    g.FillPath(pathGrad, brush);
                }
            }

            DrawDisc(g);

            int infoX = _discRect.Right + S(26);
            int infoW = Math.Max(S(80), _heroRect.Right - infoX - S(30));

            Theme.DrawText(g, _title, Theme.Font(24f, FontStyle.Bold), Theme.TextHi,
                new RectangleF(infoX, _discRect.Y + S(4), infoW, S(36)));

            Theme.DrawText(g, _artist, Theme.Font(12.5f), Theme.Alpha(Theme.Cyan, 230),
                new RectangleF(infoX, _discRect.Y + S(42), infoW, S(22)));

            Theme.DrawText(g, _meta, Theme.Font(11.5f), Theme.TextLow,
                new RectangleF(infoX, _discRect.Y + S(64), infoW, S(20)));

            DrawSpectrum(g);
        }

        private void DrawDisc(Graphics g)
        {
            var r = _discRect;
            float radius = Math.Min(r.Width, r.Height) / 2f - 2;
            var center = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);

            // 外光
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2));
                using (var grad = new PathGradientBrush(path))
                {
                    grad.CenterColor = Color.FromArgb(0x20, _discColorA.R, _discColorA.G, _discColorA.B);
                    grad.SurroundColors = new[] { Color.FromArgb(0x00, _discColorA.R, _discColorA.G, _discColorA.B) };
                    g.FillEllipse(grad, new RectangleF(center.X - radius - 1, center.Y - radius - 1, radius * 2 + 2, radius * 2 + 2));
                }
            }

            // 盘体
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2));
                using (var grad = new PathGradientBrush(path))
                {
                    grad.CenterColor = Color.FromArgb(0x1C, 0x24, 0x2E);
                    grad.SurroundColors = new[] { Color.FromArgb(0x06, 0x09, 0x0C) };
                    g.FillPath(grad, path);
                }
            }

            // 纹路
            using (var pen = new Pen(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF), 0.7f))
            {
                for (float rr = radius * 0.45f; rr < radius * 0.96f; rr += 3.4f)
                    g.DrawEllipse(pen, center.X - rr, center.Y - rr, rr * 2, rr * 2);
            }

            // 中心标签
            float label = radius * 0.44f;
            using (var brush = new LinearGradientBrush(
                       new RectangleF(center.X - label, center.Y - label, label * 2, label * 2),
                       _discColorA, _discColorB, 45f))
                g.FillEllipse(brush, center.X - label, center.Y - label, label * 2, label * 2);

            // 标签上的音符随盘旋转
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(_discAngle);
            DrawIcon(g, "note", new RectangleF(-label * 0.62f, -label * 0.62f, label * 1.24f, label * 1.24f),
                Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF), label * 1.2f, false);
            g.Restore(state);

            // 轴孔
            using (var brush = new SolidBrush(Color.FromArgb(0x0B, 0x10, 0x15)))
                g.FillEllipse(brush, center.X - radius * 0.035f, center.Y - radius * 0.035f,
                    radius * 0.07f, radius * 0.07f);

            // 高光
            using (var brush = new LinearGradientBrush(
                       new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2),
                       Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 130f))
            {
                var clip = new GraphicsPath();
                clip.AddEllipse(center.X - radius, center.Y - radius, radius * 2, radius * 2);
                var old = g.Clip;
                g.SetClip(clip, CombineMode.Intersect);
                g.FillRectangle(brush, center.X - radius, center.Y - radius, radius * 2, radius);
                g.Clip = old;
                clip.Dispose();
            }
        }

        private void DrawSpectrum(Graphics g)
        {
            var r = _spectrumRect;
            using (var pen = new Pen(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), 1f))
                g.DrawLine(pen, r.X, r.Bottom, r.Right, r.Bottom);

            float slot = r.Width / (float)SpectrumBars;
            float barW = Math.Max(2f, slot - 2.4f);

            // 两根点定死方向：底青顶橙，中段保持青，只有峰值才转暖
            using (var brush = new LinearGradientBrush(
                       new PointF(r.X, r.Bottom), new PointF(r.X, r.Top),
                       Theme.Cyan, Theme.Amber))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { Theme.CyanDeep, Theme.Cyan, Theme.Cyan, Theme.Amber },
                    Positions = new[] { 0f, 0.35f, 0.72f, 1f }
                };
                for (int i = 0; i < SpectrumBars; i++)
                {
                    float level = _spectrumLevel[i];
                    float barH = Math.Max(1.5f, level * (r.Height - 6));
                    float x = r.X + i * slot + (slot - barW) / 2;
                    Theme.FillRound(g, brush, new RectangleF(x, r.Bottom - 1 - barH, barW, barH),
                        Math.Min(barW / 2f, 2.4f));
                }
            }
        }

        private void DrawLyrics(Graphics g)
        {
            using (var brush = new SolidBrush(Color.FromArgb(0x18, 0, 0, 0)))
                g.FillRectangle(brush, _lyricRect);
            using (var pen = new Pen(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF), 1f))
                g.DrawLine(pen, _lyricRect.X, _lyricRect.Y + 0.5f, _lyricRect.Right, _lyricRect.Y + 0.5f);

            var state = g.Save();
            g.SetClip(_lyricRect);

            if (_lyrics.IsEmpty)
            {
                var iconSize = SF(28);
                DrawIcon(g, "note",
                    new RectangleF(_lyricRect.X + _lyricRect.Width / 2f - iconSize / 2f,
                        _lyricRect.Y + _lyricRect.Height / 2f - SF(48), iconSize, iconSize),
                    Color.FromArgb(0x33, Theme.TextMid.R, Theme.TextMid.G, Theme.TextMid.B), iconSize, false);

                Theme.DrawText(g, "暂无歌词", Theme.Font(15f), Theme.TextMid,
                    new RectangleF(_lyricRect.X, _lyricRect.Y + _lyricRect.Height / 2f - SF(10), _lyricRect.Width, S(24)),
                    StringAlignment.Center);
                Theme.DrawText(g, "把同名 .lrc 放在歌曲旁边就会自动加载", Theme.Font(11.5f), Theme.TextLow,
                    new RectangleF(_lyricRect.X, _lyricRect.Y + _lyricRect.Height / 2f + SF(16), _lyricRect.Width, S(20)),
                    StringAlignment.Center);
                g.Restore(state);
                return;
            }

            float lineH = SF(38);
            float centerY = _lyricRect.Y + _lyricRect.Height / 2f;
            var lines = _lyrics.Lines;

            int first = (int)Math.Floor(_lyricScroll) - 4;
            int last = (int)Math.Ceiling(_lyricScroll) + 4;

            for (int i = first; i <= last; i++)
            {
                if (i < 0 || i >= lines.Count) continue;

                double distance = Math.Abs(i - _lyricScroll);
                if (distance > 3.4) continue;

                bool current = i == _lyricIndex;
                float y = centerY + (float)((i - _lyricScroll) * lineH);

                float size;
                Color color;
                FontStyle style;

                if (current)
                {
                    size = 19f;
                    color = Theme.Cyan;
                    style = FontStyle.Bold;
                }
                else if (distance < 1.5)
                {
                    size = 14.5f;
                    style = FontStyle.Regular;
                    int a = (int)(0xB0 * (1 - distance / 2.2));
                    color = Color.FromArgb(Math.Max(0x20, a), Theme.TextMid);
                }
                else
                {
                    size = 13.5f;
                    style = FontStyle.Regular;
                    int a = (int)Math.Max(0x14, 0x90 * (1 - distance / 3.4));
                    color = Color.FromArgb(a, Theme.TextMid);
                }

                Theme.DrawText(g, lines[i].Text, Theme.Font(size, style), color,
                    new RectangleF(_lyricRect.X + S(12), y - lineH / 2f, _lyricRect.Width - S(24), lineH),
                    StringAlignment.Center);
            }

            g.Restore(state);
        }

        private void DrawControlBar(Graphics g)
        {
            using (var brush = new SolidBrush(Color.FromArgb(0x22, 0, 0, 0)))
                g.FillRectangle(brush, _controlRect);
            using (var pen = new Pen(Color.FromArgb(0x0E, 0xFF, 0xFF, 0xFF), 1f))
                g.DrawLine(pen, _controlRect.X, _controlRect.Y + 0.5f, _controlRect.Right, _controlRect.Y + 0.5f);

            double duration = _engine.Duration.TotalSeconds;
            var position = _seekPreviewing && _draggingSeek
                ? TimeSpan.FromSeconds(duration * _seekRatio)
                : _engine.Position;

            Theme.DrawText(g, Theme.FormatTime(position), Theme.Font(11.5f, FontStyle.Regular, true), Theme.TextMid,
                _timeNowRect, StringAlignment.Near);
            Theme.DrawText(g, Theme.FormatTime(_engine.Duration), Theme.Font(11.5f, FontStyle.Regular, true), Theme.TextMid,
                _timeTotalRect, StringAlignment.Far);

            double ratio = duration > 0 ? position.TotalSeconds / duration : 0;
            if (_draggingSeek) ratio = _seekRatio;
            DrawSeekBar(g, _seekRect, ratio, _draggingSeek);

            DrawTimeLabels(g);

            foreach (var b in _buttons)
            {
                if (b.Name == "play" || b.Name == "prev" || b.Name == "next")
                    DrawButton(g, b, ButtonIcon(b));
                else if (b.Name == "mode")
                {
                    b.Icon = ModeIcon();
                    b.Toggled = true;
                    DrawButton(g, b, b.Icon);
                }
                else if (b.Name == "mute")
                    DrawButton(g, b, _engine.IsMuted || _engine.VolumeRaw <= 1 ? "mute" : "volume");
            }

            DrawVolumeBar(g);
        }

        private void DrawTimeLabels(Graphics g)
        {
            // 时间已在 DrawControlBar 里画过，这里留空给以后的扩展
        }

        private void DrawSeekBar(Graphics g, Rectangle r, double ratio, bool active)
        {
            float cy = r.Y + r.Height / 2f;
            float pad = 6f;
            float trackW = r.Width - pad * 2;
            float h = active ? 5.5f : 4f;
            float radius = h / 2f;

            using (var brush = new SolidBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)))
                Theme.FillRound(g, brush, new RectangleF(r.X + pad, cy - radius, trackW, h), radius);

            float filled = (float)(trackW * Math.Max(0, Math.Min(1, ratio)));
            if (filled > 1)
            {
                using (var brush = Theme.HorizontalGradient(
                           new RectangleF(r.X + pad, cy - radius, Math.Max(trackW, 1), h),
                           Theme.CyanDeep, Theme.Cyan))
                    Theme.FillRound(g, brush, new RectangleF(r.X + pad, cy - radius, Math.Max(filled, h), h), radius);
            }

            float thumbX = r.X + pad + filled;
            float thumbR = active ? 7f : 5.5f;

            if (active || r.Contains(PointToClient(Cursor.Position)))
            {
                using (var brush = new SolidBrush(Color.FromArgb(0x38, Theme.Cyan.R, Theme.Cyan.G, Theme.Cyan.B)))
                    g.FillEllipse(brush, thumbX - thumbR - 5, cy - thumbR - 5, (thumbR + 5) * 2, (thumbR + 5) * 2);
            }

            using (var brush = new SolidBrush(Theme.Cyan))
                g.FillEllipse(brush, thumbX - thumbR, cy - thumbR, thumbR * 2, thumbR * 2);
            using (var brush = new SolidBrush(Color.FromArgb(0xF2, 0xFB, 0xFF)))
                g.FillEllipse(brush, thumbX - thumbR * 0.42f, cy - thumbR * 0.42f, thumbR * 0.84f, thumbR * 0.84f);
        }

        private void DrawVolumeBar(Graphics g)
        {
            var r = _volumeRect;
            float cy = r.Y + r.Height / 2f;
            float pad = 5f;
            float trackW = r.Width - pad * 2;
            float h = 3.5f;

            using (var brush = new SolidBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF)))
                Theme.FillRound(g, brush, new RectangleF(r.X + pad, cy - h / 2, trackW, h), h / 2);

            float filled = (float)(trackW * _volumeRatio);
            if (filled > 1)
            {
                using (var brush = new SolidBrush(Theme.Cyan))
                    Theme.FillRound(g, brush, new RectangleF(r.X + pad, cy - h / 2, filled, h), h / 2);
            }

            float thumbR = 4.5f;
            float thumbX = r.X + pad + filled;
            using (var brush = new SolidBrush(Theme.Cyan))
                g.FillEllipse(brush, thumbX - thumbR, cy - thumbR, thumbR * 2, thumbR * 2);
        }

        private void DrawButton(Graphics g, UIButton b, string icon)
        {
            var r = b.Bounds;
            bool hover = b == _hoverButton;
            bool pressed = b == _pressedButton;

            if (b.Primary)
            {
                float radius = Math.Min(r.Width, r.Height) / 2f;
                var center = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);

                using (var brush = new SolidBrush(Color.FromArgb(hover ? 0x3C : 0x22, b.Accent.R, b.Accent.G, b.Accent.B)))
                    g.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2, radius * 2);

                var fill = hover ? Theme.Lighten(b.Accent, 0.12) : b.Accent;
                using (var brush = new SolidBrush(fill))
                    g.FillEllipse(brush, center.X - radius * 0.82f, center.Y - radius * 0.82f,
                        radius * 1.64f, radius * 1.64f);

                DrawIcon(g, icon, new RectangleF(r.X, r.Y, r.Width, r.Height),
                    Color.FromArgb(0x05, 0x10, 0x14), r.Height * 0.40f, true);
                return;
            }

            if (hover || b.Toggled)
            {
                using (var brush = new SolidBrush(b.Toggled
                           ? Color.FromArgb(0x30, b.Accent.R, b.Accent.G, b.Accent.B)
                           : Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF)))
                    Theme.FillRound(g, brush, new RectangleF(r.X, r.Y, r.Width, r.Height), 7);
            }

            var color = b.Toggled ? b.Accent
                      : hover ? Theme.TextHi
                      : pressed ? b.Accent
                      : Theme.TextMid;

            DrawIcon(g, icon, new RectangleF(r.X, r.Y, r.Width, r.Height), color,
                Math.Min(r.Width, r.Height) * 0.56f, Icons.Filled(icon));
        }

        /// <summary>在给定矩形内居中绘制 24x24 网格里的图标。</summary>
        private void DrawIcon(Graphics g, string name, RectangleF bounds, Color color, float size, bool filled)
        {
            var path = Icons.Get(name);
            float scale = size / 24f;

            var b = path.GetBounds();
            float ox = bounds.X + (bounds.Width - b.Width * scale) / 2f - b.X * scale;
            float oy = bounds.Y + (bounds.Height - b.Height * scale) / 2f - b.Y * scale;

            var state = g.Save();
            g.TranslateTransform(ox, oy);
            g.ScaleTransform(scale, scale);

            if (filled)
            {
                using (var brush = new SolidBrush(color))
                    g.FillPath(brush, path);
            }
            else
            {
                using (var pen = new Pen(color, Math.Max(0.6f, 1.75f / scale))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                })
                    g.DrawPath(pen, path);
            }

            g.Restore(state);
        }

        private void DrawMini(Graphics g)
        {
            var full = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            using (var brush = Theme.VerticalGradient(full,
                       Color.FromArgb(0x16, 0x1E, 0x29), Color.FromArgb(0x08, 0x0B, 0x10)))
                g.FillRectangle(brush, full);

            Theme.DrawText(g, _miniTitle, Theme.Font(13.5f, FontStyle.Bold), Theme.TextHi,
                new RectangleF(S(16), S(14), ClientSize.Width - S(200), S(22)));
            Theme.DrawText(g, _miniArtist, Theme.Font(11f), Theme.TextLow,
                new RectangleF(S(16), S(34), ClientSize.Width - S(200), S(18)));

            foreach (var b in _buttons) DrawButton(g, b, ButtonIcon(b));

            using (var pen = new Pen(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 1f))
                Theme.DrawRound(g, pen, new RectangleF(0.5f, 0.5f, ClientSize.Width - 1f, ClientSize.Height - 1f), 12f);

            double duration = _engine.Duration.TotalSeconds;
            double ratio = duration > 0 ? _engine.Position.TotalSeconds / duration : 0;
            if (_draggingSeek) ratio = _seekRatio;
            DrawSeekBar(g, _miniSeekRect, ratio, _draggingSeek);
        }

        private string ButtonIcon(UIButton b)
        {
            switch (b.Name)
            {
                case "miniplay": return _engine.IsPlaying ? "pause" : "play";
                case "minibtn": return "mini";
                case "expand": return "expand";
                case "miniclose": return "close";
                case "play": return _engine.IsPlaying ? "pause" : "play";
                case "mode": return ModeIcon();
                case "mute": return _engine.IsMuted ? "mute" : "volume";
                case "addFile": return "note";
                case "addFolder": return "folder";
                case "clear": return "trash";
                default: return b.Name;
            }
        }

        private string ModeIcon()
        {
            switch (_engine.Mode)
            {
                case PlayMode.SingleLoop: return "repeatOne";
                case PlayMode.Shuffle: return "shuffle";
                case PlayMode.Sequential: return "list";
                default: return "repeatAll";
            }
        }

        // =====================================================================
        // 动画
        // =====================================================================

        private void OnAnimTick(object sender, EventArgs e)
        {
            bool playing = _engine.IsPlaying;
            _anim.Interval = playing || _lyricScroll != _lyricTarget ? 33 : 120;

            _animTime += 0.033;

            for (int i = 0; i < SpectrumBars; i++)
            {
                float target;
                if (playing)
                {
                    double x = i / (double)SpectrumBars;
                    double wave = Math.Sin(_animTime * 3.1 + _spectrumSeed[i]) * 0.34
                                + Math.Sin(_animTime * 5.7 - i * 0.31) * 0.24
                                + Math.Sin(_animTime * 1.6 + i * 1.13) * 0.22
                                + Math.Sin(_animTime * 9.3 + _spectrumSeed[i] * 2) * 0.10;
                    double tilt = Math.Pow(1.0 - x * 0.85, 0.62);
                    double punch = _random.NextDouble() * 0.18;
                    double pulse = 0.5 + 0.5 * Math.Sin(_animTime * 2.2);
                    target = (float)Math.Max(0.02, Math.Min(1.0, (wave * 0.5 + 0.5) * tilt * (0.62 + 0.38 * pulse) + punch * tilt));
                }
                else
                {
                    target = 0.012f;
                }

                _spectrumTarget[i] = target;
                _spectrumLevel[i] += (target - _spectrumLevel[i]) * (playing ? 0.42f : 0.10f);
            }

            if (playing) _discAngle = (_discAngle + 0.5f) % 360f;

            if (!_lyrics.IsEmpty)
            {
                double delta = _lyricTarget - _lyricScroll;
                if (Math.Abs(delta) > 0.0015)
                    _lyricScroll += delta * 0.16;
            }

            if (!_miniMode)
            {
                Invalidate(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
            }
            else
            {
                Invalidate();
            }
        }

        // =====================================================================
        // 引擎事件
        // =====================================================================

        private void WireEngine()
        {
            _engine.StateChanged += (s, e) => RefreshTransport();
            _engine.TrackChanged += (s, e) => OnTrackChanged();
            _engine.PositionChanged += (s, e) => OnPositionTick();
            _engine.PlaylistChanged += (s, e) =>
            {
                _listScroll = Math.Min(_listScroll, Math.Max(0, _engine.Items.Count * RowHeight - _listRect.Height));
                RefreshTransport();
            };
            _engine.ErrorOccurred += (s, message) =>
            {
                ShowToast(message);
            };
        }

        private void OnTrackChanged()
        {
            var track = _engine.Current;
            if (track == null)
            {
                _title = "还没有选歌";
                _artist = "把音乐拖进窗口，或点左侧 + 添加";
                _meta = string.Empty;
                _lyrics = Lyrics.Empty;
                _lyricIndex = -1;
                _lyricScroll = 0;
                _lyricTarget = 0;
                _miniTitle = "清商音乐播放器";
                _miniArtist = "把音乐拖进来";
            }
            else
            {
                _title = track.DisplayTitle;
                _artist = track.DisplayArtist;
                var album = string.IsNullOrWhiteSpace(track.Album) ? string.Empty : track.Album;
                bool albumUseful = album.Length > 0 &&
                                   !string.Equals(album, track.DisplayTitle, StringComparison.OrdinalIgnoreCase) &&
                                   !album.Contains(track.DisplayTitle) &&
                                   !track.DisplayTitle.Contains(album);

                _meta = albumUseful
                    ? album + "   ·   " + track.Extension + "   ·   " + track.DurationText
                    : track.Extension + "   ·   " + track.DurationText;

                _lyrics = Lyrics.LoadFor(track.FilePath);
                _lyricIndex = -1;
                _lyricScroll = 0;
                _lyricTarget = 0;

                _miniTitle = track.DisplayTitle;
                _miniArtist = track.DisplayArtist;

                UpdateDiscColors(track);
                ScrollToCurrent();
            }

            RefreshTransport();
        }

        private void UpdateDiscColors(Track track)
        {
            string key = track.Album + "|" + track.Artist + "|" + track.DisplayTitle;
            if (key == _discKey) return;
            _discKey = key;

            int hash = 17;
            foreach (var ch in key) hash = hash * 31 + ch;

            double hue = 178 + Math.Abs(hash % 78);    // 178..256：青 → 蓝，和主题同族
            _discColorA = FromHsv(hue, 0.62, 0.95);
            _discColorB = FromHsv((hue + 42) % 360, 0.78, 0.55);
            _discAngle = 0;
        }

        private static Color FromHsv(double hue, double s, double v)
        {
            int hi = (int)(hue / 60) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);
            double r, gg, b;
            switch (hi)
            {
                case 0: r = v; gg = t; b = p; break;
                case 1: r = q; gg = v; b = p; break;
                case 2: r = p; gg = v; b = t; break;
                case 3: r = p; gg = q; b = v; break;
                case 4: r = t; gg = p; b = v; break;
                default: r = v; gg = p; b = q; break;
            }
            return Color.FromArgb((int)(r * 255), (int)(gg * 255), (int)(b * 255));
        }

        private void OnPositionTick()
        {
            if (_lyrics.IsEmpty) return;

            int idx = _lyrics.IndexAt(_engine.Position);
            if (idx == _lyricIndex) return;

            _lyricIndex = idx;
            if (idx < 0)
            {
                _lyricTarget = 0;
            }
            else
            {
                double frac = 0;
                if (idx + 1 < _lyrics.Lines.Count)
                {
                    var t0 = _lyrics.Lines[idx].Time;
                    var t1 = _lyrics.Lines[idx + 1].Time;
                    double span = (t1 - t0).TotalSeconds;
                    if (span > 0.2)
                        frac = Math.Max(0, Math.Min(1, (_engine.Position - t0).TotalSeconds / span));
                }
                _lyricTarget = idx + frac;
            }
        }

        private void RefreshTransport()
        {
            Diag.Write("transport: playing=" + _engine.IsPlaying + " idx=" + _engine.CurrentIndex + " mode=" + _engine.Mode);
            _volumeRatio = _engine.IsMuted ? 0 : _engine.Volume;
            Invalidate();
        }

        private void ScrollToCurrent()
        {
            int index = _engine.CurrentIndex;
            if (index < 0 || _listRect.Height <= 0) return;

            int target = index * RowHeight - _listRect.Height / 2 + RowHeight / 2;
            int max = Math.Max(0, _engine.Items.Count * RowHeight - _listRect.Height);
            _listScroll = Math.Max(0, Math.Min(max, target));
        }

        // =====================================================================
        // 鼠标 / 键盘
        // =====================================================================

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_draggingWindow)
            {
                var screen = PointToScreen(e.Location);
                Location = new Point(_windowOrigin.X + (screen.X - _dragOrigin.X),
                                     _windowOrigin.Y + (screen.Y - _dragOrigin.Y));
                return;
            }

            if (_draggingSeek)
            {
                _seekRatio = RatioFromX(_miniMode ? _miniSeekRect : _seekRect, e.X);
                Invalidate();
                return;
            }

            if (_draggingVolume)
            {
                _volumeRatio = RatioFromX(_volumeRect, e.X);
                _engine.SetVolume((int)Math.Round(_volumeRatio * 1000));
                Invalidate();
                return;
            }

            var button = HitButton(e.Location);
            if (button != _hoverButton)
            {
                _hoverButton = button;
                _tip.Active = false;
                if (button != null && !string.IsNullOrEmpty(button.ToolTip))
                {
                    _tip.SetToolTip(this, button.ToolTip);
                    _tip.Active = true;
                }
                Invalidate();
            }

            int row = HitRow(e.Location);
            if (row != _hoverRow)
            {
                _hoverRow = row;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Right)
            {
                var row = HitRow(e.Location);
                if (row >= 0)
                {
                    _selectedIndex = row;
                    ShowRowMenu(row, e.Location);
                    Invalidate();
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            // 标题栏拖动 / 双击切换迷你
            if (e.Clicks == 2 && _titleRect.Contains(e.Location) && HitButton(e.Location) == null)
            {
                ToggleMini();
                return;
            }

            if (_titleRect.Contains(e.Location) && !_miniMode && HitButton(e.Location) == null)
            {
                _draggingWindow = true;
                _dragOrigin = PointToScreen(e.Location);
                _windowOrigin = Location;
                return;
            }

            if (_miniMode && new Rectangle(0, 0, ClientSize.Width, 58).Contains(e.Location) && HitButton(e.Location) == null)
            {
                _draggingWindow = true;
                _dragOrigin = PointToScreen(e.Location);
                _windowOrigin = Location;
                return;
            }

            var seekTarget = _miniMode ? _miniSeekRect : _seekRect;
            if (seekTarget.Contains(e.Location))
            {
                _draggingSeek = true;
                _seekPreviewing = true;
                _seekRatio = RatioFromX(seekTarget, e.X);
                Invalidate();
                return;
            }

            if (!_miniMode && _volumeRect.Contains(e.Location))
            {
                _draggingVolume = true;
                _volumeRatio = RatioFromX(_volumeRect, e.X);
                _engine.SetVolume((int)Math.Round(_volumeRatio * 1000));
                Invalidate();
                return;
            }

            var buttonHit = HitButton(e.Location);
            if (buttonHit != null)
            {
                _pressedButton = buttonHit;
                Invalidate();
                return;
            }

            int clicked = HitRow(e.Location);
            if (clicked >= 0)
            {
                var now = DateTime.Now;
                bool isDouble = clicked == _lastClickRow && (now - _lastClickAt).TotalMilliseconds < 420;
                _lastClickRow = clicked;
                _lastClickAt = now;

                _selectedIndex = clicked;
                if (isDouble)
                {
                    _engine.PlayAt(clicked);
                    _lastClickRow = -1;
                }
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_draggingWindow)
            {
                _draggingWindow = false;
                return;
            }

            if (_draggingSeek)
            {
                _draggingSeek = false;
                _seekPreviewing = false;
                double duration = _engine.Duration.TotalSeconds;
                if (duration > 0) _engine.Seek(TimeSpan.FromSeconds(duration * _seekRatio));
                Invalidate();
                return;
            }

            if (_draggingVolume)
            {
                _draggingVolume = false;
                return;
            }

            var button = HitButton(e.Location);
            if (button != null && button == _pressedButton)
            {
                _pressedButton = null;
                HandleButton(button.Name);
            }
            else
            {
                _pressedButton = null;
            }

            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverButton = null;
            _hoverRow = -1;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_miniMode) return;
            if (!_listRect.Contains(e.Location)) return;

            int max = Math.Max(0, _engine.Items.Count * RowHeight - _listRect.Height);
            _listScroll = Math.Max(0, Math.Min(max, _listScroll - e.Delta));
            Invalidate(_listRect);
        }

        private static double RatioFromX(Rectangle r, int x)
        {
            double pad = 6;
            double width = Math.Max(1, r.Width - pad * 2);
            return Math.Max(0, Math.Min(1, (x - r.X - pad) / width));
        }

        private UIButton HitButton(Point p)
        {
            foreach (var b in _buttons)
                if (b.Bounds.Contains(p))
                    return b;
            return null;
        }

        private int HitRow(Point p)
        {
            if (!_listRect.Contains(p)) return -1;
            int index = (p.Y - _listRect.Y + _listScroll) / RowHeight;
            return index >= 0 && index < _engine.Items.Count ? index : -1;
        }

        private void HandleButton(string name)
        {
            switch (name)
            {
                case "play":
                case "miniplay":
                    _engine.TogglePlay();
                    break;
                case "prev":
                    _engine.Previous();
                    break;
                case "next":
                    _engine.Next();
                    break;
                case "mode":
                    CycleMode();
                    break;
                case "mute":
                    _engine.ToggleMute();
                    break;
                case "addFile":
                    OpenFiles();
                    break;
                case "addFolder":
                    OpenFolder();
                    break;
                case "clear":
                    if (_engine.Items.Count > 0) _engine.Clear();
                    break;
                case "close":
                case "miniclose":
                    Close();
                    break;
                case "minimize":
                    WindowState = FormWindowState.Minimized;
                    break;
                case "minibtn":
                case "expand":
                    ToggleMini();
                    break;
            }
        }

        private void CycleMode()
        {
            switch (_engine.Mode)
            {
                case PlayMode.ListLoop: _engine.Mode = PlayMode.SingleLoop; break;
                case PlayMode.SingleLoop: _engine.Mode = PlayMode.Shuffle; break;
                case PlayMode.Shuffle: _engine.Mode = PlayMode.Sequential; break;
                default: _engine.Mode = PlayMode.ListLoop; break;
            }
            Invalidate();
        }

        private void ToggleMini()
        {
            _miniMode = !_miniMode;
            if (_miniMode)
            {
                MinimumSize = Size.Empty;   // 迷你窗口比正常模式的最小尺寸还小
                ClientSize = new Size(S(Theme.MiniWidth), S(Theme.MiniHeight));
            }
            else
            {
                ClientSize = new Size(S(Theme.NormalWidth), S(Theme.NormalHeight));
                MinimumSize = new Size(S(760), S(520));
            }
            LayoutAll();
            ApplyRoundedRegion();
            Invalidate();
        }

        private void OpenFiles()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "选择音乐文件",
                Multiselect = true,
                Filter = "音频与播放列表|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac;*.ogg;*.m3u;*.m3u8;*.pls|音频文件|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac;*.ogg|播放列表|*.m3u;*.m3u8;*.pls|所有文件|*.*"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _engine.AddFiles(dlg.FileNames, true);
            }
        }

        private void OpenFolder()
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "选择音乐文件夹（会包含子目录）",
                ShowNewFolderButton = false
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                int count = _engine.AddFiles(new[] { dlg.SelectedPath }, true);
                if (count == 0)
                    MessageBox.Show(this, "这个文件夹里没找到支持的音频文件。", "清商音乐播放器",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowRowMenu(int index, Point at)
        {
            var menu = new ContextMenuStrip
            {
                Renderer = new DarkMenuRenderer(),
                BackColor = Theme.Bg3,
                ForeColor = Theme.TextHi,
                Font = Theme.Font(12.5f),
                ShowImageMargin = false
            };

            menu.Items.Add("播放", null, (s, e) => _engine.PlayAt(index));
            menu.Items.Add("从列表移除", null, (s, e) => _engine.RemoveAt(index));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("在文件夹中显示", null, (s, e) => RevealInExplorer(index));

            menu.Show(this, at);
        }

        private void RevealInExplorer(int index)
        {
            if (index < 0 || index >= _engine.Items.Count) return;
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe",
                    "/select,\"" + _engine.Items[index].FilePath + "\"") { UseShellExecute = true });
            }
            catch
            {
                // 忽略
            }
        }

        private void ShowToast(string message)
        {
            // 用一个临时提示条代替弹窗，不打断播放
            MessageBox.Show(this, message, "清商音乐播放器", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.KeyCode)
            {
                case Keys.Space:
                    _engine.TogglePlay();
                    e.Handled = true;
                    break;
                case Keys.Left:
                    _engine.Seek(_engine.Position - TimeSpan.FromSeconds(5));
                    e.Handled = true;
                    break;
                case Keys.Right:
                    _engine.Seek(_engine.Position + TimeSpan.FromSeconds(5));
                    e.Handled = true;
                    break;
                case Keys.Up:
                    _engine.SetVolume(_engine.VolumeRaw + 50);
                    e.Handled = true;
                    break;
                case Keys.Down:
                    _engine.SetVolume(_engine.VolumeRaw - 50);
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    if (_miniMode) ToggleMini();
                    e.Handled = true;
                    break;
                case Keys.MediaPlayPause:
                    _engine.TogglePlay();
                    e.Handled = true;
                    break;
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;
            _engine.AddFiles(paths, true);
        }

        // =====================================================================
        // 辅助类型
        // =====================================================================

        private sealed class UIButton
        {
            public string Name;
            public Rectangle Bounds;
            public string ToolTip;
            public bool Primary;
            public bool IsToggle;
            public bool Toggled;
            public Color Accent = Theme.Cyan;
            public string Icon;
        }

        private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var rect = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
                using (var brush = new SolidBrush(e.Item.Selected
                           ? Color.FromArgb(0x38, Theme.Cyan.R, Theme.Cyan.G, Theme.Cyan.B)
                           : Color.Transparent))
                    e.Graphics.FillRectangle(brush, rect);
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var brush = new SolidBrush(Theme.Bg3))
                    e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var pen = new Pen(Theme.Bg4))
                    e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Selected ? Theme.TextHi : Theme.TextMid;
                base.OnRenderItemText(e);
            }
        }
    }
}
