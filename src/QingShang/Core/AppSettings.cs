using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace QingShang.Core
{
    /// <summary>
    /// 极简配置：%APPDATA%\QingShang\settings.ini，人可读、可手改。
    /// </summary>
    public sealed class AppSettings
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QingShang");

        private static readonly string FilePath = Path.Combine(Dir, "settings.ini");

        public int Volume { get; set; } = 800;
        public bool Muted { get; set; }
        public PlayMode Mode { get; set; } = PlayMode.ListLoop;
        public int Width { get; set; } = 1000;
        public int Height { get; set; } = 656;
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return settings;

                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    int split = line.IndexOf('=');
                    if (split <= 0) continue;

                    var key = line.Substring(0, split).Trim();
                    var value = line.Substring(split + 1).Trim();

                    switch (key)
                    {
                        case "volume":
                            if (int.TryParse(value, out var v)) settings.Volume = Math.Max(0, Math.Min(1000, v));
                            break;
                        case "muted":
                            settings.Muted = value == "1";
                            break;
                        case "mode":
                            if (Enum.TryParse<PlayMode>(value, true, out var m)) settings.Mode = m;
                            break;
                        case "x":
                            if (int.TryParse(value, out var x)) settings.X = x;
                            break;
                        case "y":
                            if (int.TryParse(value, out var y)) settings.Y = y;
                            break;
                    }
                }
            }
            catch
            {
                // 配置坏了就用默认值
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.AppendLine("# 清商播放器 配置文件");
                sb.AppendLine("volume=" + Volume.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("muted=" + (Muted ? "1" : "0"));
                sb.AppendLine("mode=" + Mode);
                sb.AppendLine("x=" + X);
                sb.AppendLine("y=" + Y);
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // 存不了就算了
            }
        }
    }
}
