using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace QingShang.Core
{
    public sealed class LyricLine
    {
        public LyricLine(TimeSpan time, string text)
        {
            Time = time;
            Text = text;
        }

        public TimeSpan Time { get; }
        public string Text { get; }
    }

    /// <summary>
    /// LRC 歌词：解析时间标签、按时间轴查找当前行。
    /// 支持 [mm:ss.xx] / [mm:ss:xx] / 一行多标签 / 元信息标签。
    /// </summary>
    public sealed class Lyrics
    {
        private static readonly Regex TimeTag = new Regex(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);

        private readonly List<LyricLine> _lines = new List<LyricLine>();

        public static Lyrics Empty { get; } = new Lyrics();

        public IReadOnlyList<LyricLine> Lines => _lines;

        public bool IsEmpty => _lines.Count == 0;

        /// <summary>当前时间对应的行索引；还没有歌词命中时返回 -1。</summary>
        public int IndexAt(TimeSpan position)
        {
            if (_lines.Count == 0) return -1;
            int lo = 0, hi = _lines.Count - 1, result = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (_lines[mid].Time <= position)
                {
                    result = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return result;
        }

        public static Lyrics Parse(string content)
        {
            var lyrics = new Lyrics();
            if (string.IsNullOrWhiteSpace(content)) return lyrics;

            var raw = new List<LyricLine>();
            foreach (var line in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var matches = TimeTag.Matches(line);
                if (matches.Count == 0) continue;

                var text = TimeTag.Replace(line, string.Empty).Trim();
                foreach (Match m in matches)
                {
                    int minutes = int.Parse(m.Groups[1].Value);
                    int seconds = int.Parse(m.Groups[2].Value);
                    int fraction = 0;
                    if (m.Groups[3].Success)
                    {
                        var frac = m.Groups[3].Value;
                        fraction = frac.Length == 1 ? int.Parse(frac) * 100
                                 : frac.Length == 2 ? int.Parse(frac) * 10
                                 : int.Parse(frac);
                    }

                    var time = new TimeSpan(0, 0, minutes, seconds, fraction);
                    raw.Add(new LyricLine(time, text));
                }
            }

            raw.Sort((a, b) => a.Time.CompareTo(b.Time));

            // 去掉相邻重复（部分 LRC 会为每句重复标注）
            LyricLine last = null;
            foreach (var item in raw)
            {
                if (last != null && last.Time == item.Time && last.Text == item.Text) continue;
                lyrics._lines.Add(item);
                last = item;
            }

            return lyrics;
        }

        /// <summary>找同名 .lrc；找不到返回空歌词。</summary>
        public static Lyrics LoadFor(string audioPath)
        {
            try
            {
                var dir = Path.GetDirectoryName(audioPath);
                var name = Path.GetFileNameWithoutExtension(audioPath);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) return Empty;

                foreach (var candidate in new[]
                         {
                             Path.Combine(dir, name + ".lrc"),
                             Path.Combine(dir, name + ".LRC")
                         })
                {
                    if (!File.Exists(candidate)) continue;
                    var bytes = File.ReadAllBytes(candidate);
                    var text = DecodeText(bytes);
                    return Parse(text);
                }
            }
            catch
            {
                // 歌词读不出来就当没有
            }

            return Empty;
        }

        private static string DecodeText(byte[] bytes)
        {
            // UTF-8 BOM / UTF-16 BOM / 其余按 GB18030 处理（老歌词多为 GBK）
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch
            {
                return Encoding.GetEncoding("gb18030").GetString(bytes);
            }
        }
    }
}
