using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QingShang.UI
{
    /// <summary>
    /// 深空蓝视觉语言：一层底色、一套字阶、几个强调色。
    /// 所有绘制走 GDI+，抗锯齿 + ClearType，不依赖任何 UI 框架皮肤。
    /// </summary>
    internal static class Theme
    {
        // 底色层
        public static readonly Color Bg1 = Color.FromArgb(0x0E, 0x13, 0x1A);
        public static readonly Color Bg2 = Color.FromArgb(0x13, 0x1A, 0x24);
        public static readonly Color Bg3 = Color.FromArgb(0x1B, 0x24, 0x31);
        public static readonly Color Bg4 = Color.FromArgb(0x25, 0x30, 0x3F);

        // 文字
        public static readonly Color TextHi = Color.FromArgb(0xE9, 0xF1, 0xFA);
        public static readonly Color TextMid = Color.FromArgb(0x94, 0xA5, 0xB9);
        public static readonly Color TextLow = Color.FromArgb(0x5B, 0x6B, 0x7E);

        // 强调
        public static readonly Color Cyan = Color.FromArgb(0x3A, 0xD8, 0xE9);
        public static readonly Color CyanDeep = Color.FromArgb(0x18, 0x93, 0xA8);
        public static readonly Color Amber = Color.FromArgb(0xFF, 0xB0, 0x5C);
        public static readonly Color Rose = Color.FromArgb(0xFF, 0x6B, 0x81);

        public static readonly string UiFont = "Microsoft YaHei UI";
        public static readonly string NumFont = "Consolas";

        // ---------- 布局常量 ----------
        public const int NormalWidth = 1000;
        public const int NormalHeight = 656;
        public const int MiniWidth = 470;
        public const int MiniHeight = 108;
        public const int TitleHeight = 50;
        public const int ControlHeight = 96;
        public const int SidebarWidth = 322;
        public const int LyricHeight = 190;

        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();

        /// <summary>整窗缩放系数（DeviceDpi / 96），构造窗口时设定一次。</summary>
        public static float Scale = 1f;

        /// <summary>
        /// 字号按「像素」定义，再乘上全局缩放 —— 布局和文字用同一套坐标，
        /// 在 100% 和 125% 缩放下比例都一致。
        /// </summary>
        public static Font Font(float px, FontStyle style = FontStyle.Regular, bool numeric = false)
        {
            float real = Math.Max(6f, px * Scale);
            string key = real.ToString("0.##") + "|" + (int)style + "|" + numeric;
            if (FontCache.TryGetValue(key, out var cached)) return cached;

            var font = new Font(numeric ? NumFont : UiFont, real, style, GraphicsUnit.Pixel);
            FontCache[key] = font;
            return font;
        }

        public static Color Alpha(Color c, int a)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);
        }

        public static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Lighten(Color c, double amount) => Mix(c, Color.White, amount);
        public static Color Darken(Color c, double amount) => Mix(c, Color.Black, amount);

        public static void SetupQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        // ---------- 绘制辅助 ----------

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Max(0, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            if (d <= 0.5f)
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Brush brush, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
                g.FillPath(brush, path);
        }

        public static void DrawRound(Graphics g, Pen pen, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
                g.DrawPath(pen, path);
        }

        public static LinearGradientBrush VerticalGradient(RectangleF r, Color top, Color bottom)
        {
            var brush = new LinearGradientBrush(
                new RectangleF(r.X, r.Y - 0.5f, Math.Max(1, r.Width), Math.Max(1, r.Height + 1)),
                top, bottom, LinearGradientMode.Vertical);
            return brush;
        }

        public static LinearGradientBrush HorizontalGradient(RectangleF r, Color left, Color right)
        {
            var brush = new LinearGradientBrush(
                new RectangleF(r.X - 0.5f, r.Y, Math.Max(1, r.Width + 1), Math.Max(1, r.Height)),
                left, right, LinearGradientMode.Horizontal);
            return brush;
        }

        public static readonly StringFormat AlignLeft = MakeFormat(StringAlignment.Near);
        public static readonly StringFormat AlignCenter = MakeFormat(StringAlignment.Center);
        public static readonly StringFormat AlignRight = MakeFormat(StringAlignment.Far);

        private static StringFormat MakeFormat(StringAlignment align)
        {
            return new StringFormat
            {
                Alignment = align,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
        }

        /// <summary>单行文字，垂直居中，默认省略号截断。</summary>
        public static void DrawText(Graphics g, string text, Font font, Color color, RectangleF bounds,
            StringAlignment align = StringAlignment.Near)
        {
            if (string.IsNullOrEmpty(text)) return;
            var format = align == StringAlignment.Center ? AlignCenter
                       : align == StringAlignment.Far ? AlignRight
                       : AlignLeft;
            using (var brush = new SolidBrush(color))
                g.DrawString(text, font, brush, bounds, format);
        }

        public static string FormatTime(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
