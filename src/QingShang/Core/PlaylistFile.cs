using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QingShang.Core
{
    /// <summary>
    /// 播放列表文件解析：M3U / M3U8 / PLS。
    /// 任何音乐源只要能导出这三种格式，拖进窗口就能直接读。
    /// </summary>
    public static class PlaylistFile
    {
        public static bool IsPlaylist(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".m3u" || ext == ".m3u8" || ext == ".pls";
        }

        /// <summary>读出列表里的所有条目（本地绝对路径，或 http(s) 地址）。</summary>
        public static List<string> Read(string path)
        {
            var result = new List<string>();
            string baseDir;
            string[] lines;

            try
            {
                baseDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
                lines = File.ReadAllLines(path, DetectEncoding(path));
            }
            catch
            {
                return result;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var raw = ext == ".pls" ? ParsePls(lines) : ParseM3u(lines);

            foreach (var entry in raw)
            {
                var resolved = Resolve(entry, baseDir);
                if (resolved != null) result.Add(resolved);
            }

            return result;
        }

        private static IEnumerable<string> ParseM3u(string[] lines)
        {
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#")) continue;          // #EXTM3U / #EXTINF 等
                yield return line;
            }
        }

        private static IEnumerable<string> ParsePls(string[] lines)
        {
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("[")) continue;
                if (!line.StartsWith("File", StringComparison.OrdinalIgnoreCase)) continue;

                int eq = line.IndexOf('=');
                if (eq <= 4) continue;
                var value = line.Substring(eq + 1).Trim();
                if (value.Length > 0) yield return value;
            }
        }

        private static string Resolve(string entry, string baseDir)
        {
            entry = entry.Trim().Trim('"');

            if (entry.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                entry.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                entry.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
                entry.StartsWith("mms://", StringComparison.OrdinalIgnoreCase))
                return entry;

            if (entry.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try { return new Uri(entry).LocalPath; }
                catch { return null; }
            }

            try
            {
                // 老播放器导出的列表常见反斜杠
                var normalized = entry.Replace('\\', Path.DirectorySeparatorChar);

                if (Path.IsPathRooted(normalized))
                    return File.Exists(normalized) ? normalized : null;

                var combined = Path.GetFullPath(Path.Combine(baseDir, normalized));
                if (File.Exists(combined)) return combined;

                // 有些列表写的是相对 item 名的裸文件名
                var alt = Path.GetFullPath(Path.Combine(baseDir, Path.GetFileName(normalized)));
                return File.Exists(alt) ? alt : null;
            }
            catch
            {
                return null;
            }
        }

        private static Encoding DetectEncoding(string path)
        {
            try
            {
                var head = new byte[3];
                using (var fs = File.OpenRead(path))
                {
                    int n = fs.Read(head, 0, 3);
                    if (n >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
                        return new UTF8Encoding(false);
                }
            }
            catch
            {
                // 忽略
            }

            // 国内导出的 m3u 多数是 GBK
            return Encoding.GetEncoding("gb18030");
        }

        /// <summary>把一组条目写成标准 M3U8。</summary>
        public static void Write(string targetPath, IEnumerable<string> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");

            foreach (var entry in entries)
            {
                var name = Path.GetFileNameWithoutExtension(entry);
                sb.AppendLine("#EXTINF:-1," + name);
                sb.AppendLine(entry);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath)) ?? ".");
            File.WriteAllText(targetPath, sb.ToString(), new UTF8Encoding(true));
        }
    }
}
