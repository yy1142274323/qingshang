using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace QingShang.UI
{
    /// <summary>
    /// 图标全部用 GraphicsPath 在 24x24 网格里画出来，随尺寸缩放不糊，
    /// 也不需要任何图片资源。
    /// </summary>
    internal static class Icons
    {
        private static readonly Dictionary<string, GraphicsPath> Cache = new Dictionary<string, GraphicsPath>();
        private static readonly object Gate = new object();

        public static GraphicsPath Get(string name)
        {
            lock (Gate)
            {
                if (Cache.TryGetValue(name, out var p)) return p;
                var path = Build(name);
                Cache[name] = path;
                return path;
            }
        }

        /// <summary>填充类图标（播放、暂停、上一首、下一首）。</summary>
        public static bool Filled(string name)
        {
            switch (name)
            {
                case "play":
                case "pause":
                case "prev":
                case "next":
                case "stop":
                case "mini":
                    return true;
                default:
                    return false;
            }
        }

        private static GraphicsPath Build(string name)
        {
            var p = new GraphicsPath();
            switch (name)
            {
                case "play":
                    p.AddPolygon(new[]
                    {
                        new PointF(8f, 4.6f), new PointF(20f, 12f), new PointF(8f, 19.4f)
                    });
                    break;

                case "pause":
                    p.AddRectangle(new RectangleF(8f, 5f, 2.6f, 14f));
                    p.AddRectangle(new RectangleF(13.4f, 5f, 2.6f, 14f));
                    break;

                case "stop":
                    p.AddRectangle(new RectangleF(6.5f, 6.5f, 11f, 11f));
                    break;

                case "prev":
                    p.AddPolygon(new[]
                    {
                        new PointF(18.5f, 5f), new PointF(18.5f, 19f), new PointF(8.5f, 12f)
                    });
                    p.AddRectangle(new RectangleF(5.5f, 5f, 2f, 14f));
                    break;

                case "next":
                    p.AddPolygon(new[]
                    {
                        new PointF(5.5f, 5f), new PointF(5.5f, 19f), new PointF(15.5f, 12f)
                    });
                    p.AddRectangle(new RectangleF(16.5f, 5f, 2f, 14f));
                    break;

                case "mini":
                    p.AddRectangle(new RectangleF(4f, 5.5f, 16f, 13f));
                    break;

                case "volume":
                    p.AddPolygon(new[]
                    {
                        new PointF(4f, 9.5f), new PointF(7.5f, 9.5f), new PointF(12.5f, 5f),
                        new PointF(12.5f, 19f), new PointF(7.5f, 14.5f), new PointF(4f, 14.5f)
                    });
                    p.StartFigure();
                    p.AddArc(new RectangleF(11.5f, 8.5f, 8f, 7f), -60f, 120f);
                    p.StartFigure();
                    p.AddArc(new RectangleF(11.5f, 5.5f, 12f, 13f), -55f, 110f);
                    break;

                case "mute":
                    p.AddPolygon(new[]
                    {
                        new PointF(4f, 9.5f), new PointF(7.5f, 9.5f), new PointF(12.5f, 5f),
                        new PointF(12.5f, 19f), new PointF(7.5f, 14.5f), new PointF(4f, 14.5f)
                    });
                    p.StartFigure();
                    p.AddLine(15.5f, 9.5f, 20f, 14.5f);
                    p.StartFigure();
                    p.AddLine(20f, 9.5f, 15.5f, 14.5f);
                    break;

                case "list":
                    p.AddLine(4f, 7f, 20f, 7f);
                    p.StartFigure();
                    p.AddLine(4f, 12f, 20f, 12f);
                    p.StartFigure();
                    p.AddLine(4f, 17f, 14f, 17f);
                    break;

                case "repeatAll":
                    p.AddArc(new RectangleF(4f, 5f, 16f, 14f), 200f, 150f);
                    p.StartFigure();
                    p.AddPolygon(new[]
                    {
                        new PointF(17.5f, 2.6f), new PointF(20.6f, 5.2f), new PointF(17.5f, 7.8f)
                    });
                    p.StartFigure();
                    p.AddArc(new RectangleF(4f, 5f, 16f, 14f), 20f, 150f);
                    p.StartFigure();
                    p.AddPolygon(new[]
                    {
                        new PointF(6.5f, 16.2f), new PointF(3.4f, 18.8f), new PointF(6.5f, 21.4f)
                    });
                    break;

                case "repeatOne":
                    p.AddArc(new RectangleF(4f, 5f, 16f, 14f), 200f, 150f);
                    p.StartFigure();
                    p.AddPolygon(new[]
                    {
                        new PointF(17.5f, 2.6f), new PointF(20.6f, 5.2f), new PointF(17.5f, 7.8f)
                    });
                    p.StartFigure();
                    p.AddArc(new RectangleF(4f, 5f, 16f, 14f), 20f, 150f);
                    p.StartFigure();
                    p.AddPolygon(new[]
                    {
                        new PointF(6.5f, 16.2f), new PointF(3.4f, 18.8f), new PointF(6.5f, 21.4f)
                    });
                    p.StartFigure();
                    p.AddLine(10.6f, 9.6f, 12f, 9.0f);
                    p.AddLine(12f, 9.0f, 12f, 15f);
                    p.AddLine(10.6f, 15f, 13.4f, 15f);
                    break;

                case "shuffle":
                    p.AddLine(3.5f, 17.5f, 9f, 12f);
                    p.StartFigure();
                    p.AddLine(9f, 12f, 13f, 16f);
                    p.StartFigure();
                    p.AddLine(13f, 16f, 20f, 9f);
                    p.StartFigure();
                    p.AddPolygon(new[]
                    {
                        new PointF(16.5f, 9f), new PointF(20f, 9f), new PointF(20f, 12.5f)
                    });
                    p.StartFigure();
                    p.AddLine(3.5f, 8f, 8f, 12.5f);
                    break;

                case "folder":
                    p.AddPolygon(new[]
                    {
                        new PointF(3.5f, 7.2f), new PointF(9.4f, 7.2f), new PointF(11.6f, 9.2f),
                        new PointF(20.5f, 9.2f), new PointF(20.5f, 18f), new PointF(3.5f, 18f)
                    });
                    break;

                case "trash":
                    p.AddLine(4.5f, 7f, 19.5f, 7f);
                    p.StartFigure();
                    p.AddPolygon(new[]
                    {
                        new PointF(6.6f, 7f), new PointF(7.5f, 19.4f), new PointF(16.5f, 19.4f),
                        new PointF(17.4f, 7f)
                    });
                    p.StartFigure();
                    p.AddLine(9.4f, 7f, 9.4f, 4.6f);
                    p.AddLine(9.4f, 4.6f, 14.6f, 4.6f);
                    p.AddLine(14.6f, 4.6f, 14.6f, 7f);
                    break;

                case "note":
                    p.AddEllipse(new RectangleF(6.0f, 15.4f, 6.4f, 5.0f));
                    p.StartFigure();
                    p.AddLine(11.4f, 17.6f, 11.4f, 4.2f);
                    p.StartFigure();
                    p.AddLine(11.4f, 4.2f, 18.4f, 6.6f);
                    break;

                case "music":
                    p.AddEllipse(new RectangleF(5.0f, 15.0f, 5.6f, 4.4f));
                    p.StartFigure();
                    p.AddLine(9.8f, 17.0f, 9.8f, 4.6f);
                    p.StartFigure();
                    p.AddLine(9.8f, 4.6f, 16.6f, 6.8f);
                    p.StartFigure();
                    p.AddEllipse(new RectangleF(12.6f, 16.6f, 5.2f, 4.2f));
                    break;

                case "minimize":
                    p.AddLine(5f, 12f, 19f, 12f);
                    break;

                case "close":
                    p.AddLine(6.5f, 6.5f, 17.5f, 17.5f);
                    p.StartFigure();
                    p.AddLine(17.5f, 6.5f, 6.5f, 17.5f);
                    break;

                case "expand":
                    p.AddRectangle(new RectangleF(4f, 5f, 16f, 2f));
                    p.StartFigure();
                    p.AddRectangle(new RectangleF(4f, 9.5f, 16f, 9.5f));
                    break;

                default:
                    p.AddEllipse(new RectangleF(6f, 6f, 12f, 12f));
                    break;
            }

            return p;
        }
    }
}
