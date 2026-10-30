using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace QingShang.Core
{
    /// <summary>
    /// 轻量标签读取：ID3v2.2/2.3/2.4 文本帧 + ID3v1 兜底。
    /// 不依赖任何第三方库，只读文本信息，不碰音频数据。
    /// </summary>
    public static class TagReader
    {
        private static readonly string[] AudioExtensions =
        {
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".wma", ".ogg", ".aiff", ".aif"
        };

        public static IEnumerable<string> SupportedExtensions => AudioExtensions;

        public static bool IsSupported(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            var lower = ext.ToLowerInvariant();
            return AudioExtensions.Contains(lower);
        }

        /// <summary>把标签写进 Track，读不到就保持文件名回退。</summary>
        public static void Apply(Track track)
        {
            try
            {
                var tags = ReadId3v2(track.FilePath);

                if (HasTag(tags, "TIT2")) track.Title = tags["TIT2"];
                if (HasTag(tags, "TPE1")) track.Artist = tags["TPE1"];
                if (HasTag(tags, "TALB")) track.Album = tags["TALB"];

                if (string.IsNullOrWhiteSpace(track.Artist) || string.IsNullOrWhiteSpace(track.Album))
                {
                    var v1 = ReadId3v1(track.FilePath);
                    if (v1 != null)
                    {
                        if (string.IsNullOrWhiteSpace(track.Artist)) track.Artist = v1.Value.artist;
                        if (string.IsNullOrWhiteSpace(track.Album)) track.Album = v1.Value.album;
                        if (!HasTag(tags, "TIT2") && !string.IsNullOrWhiteSpace(v1.Value.title))
                            track.Title = v1.Value.title;
                    }
                }

                // 文件名兜底："艺术家 - 标题"
                if (string.IsNullOrWhiteSpace(track.Artist))
                {
                    var name = Path.GetFileNameWithoutExtension(track.FilePath);
                    var idx = name.IndexOf(" - ", StringComparison.Ordinal);
                    if (idx > 0 && idx < name.Length - 3)
                    {
                        track.Artist = name.Substring(0, idx);
                        if (!HasTag(tags, "TIT2"))
                            track.Title = name.Substring(idx + 3);
                    }
                }

                // 时长：列表里先显示出来，播放时会被 MCI 的精确值覆盖
                if (track.Duration <= TimeSpan.Zero)
                {
                    var probed = ProbeDuration(track.FilePath);
                    if (probed > TimeSpan.Zero) track.Duration = probed;
                }
            }
            catch
            {
                // 标签坏了不影响播放
            }
        }

        private static bool HasTag(Dictionary<string, string> tags, string key)
        {
            return tags.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v);
        }

        /// <summary>
        /// 不进解码器也能算出时长：WAV 读 RIFF 头，MP3 按首帧比特率估算。
        /// 拿不准就返回 0，由播放时的 MCI 结果覆盖。
        /// </summary>
        public static TimeSpan ProbeDuration(string path)
        {
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".wav") return ProbeWavDuration(path);
                if (ext == ".mp3") return ProbeMp3Duration(path);
            }
            catch
            {
                // 算不出来就算了
            }
            return TimeSpan.Zero;
        }

        private static TimeSpan ProbeWavDuration(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs))
            {
                if (fs.Length < 44) return TimeSpan.Zero;

                var riff = Encoding.ASCII.GetString(reader.ReadBytes(4));
                reader.ReadUInt32();
                var wave = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (riff != "RIFF" || wave != "WAVE") return TimeSpan.Zero;

                long byteRate = 0;
                long dataSize = 0;

                while (fs.Position + 8 <= fs.Length)
                {
                    var chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    uint chunkSize = reader.ReadUInt32();
                    long dataStart = fs.Position;
                    long next = dataStart + chunkSize + (chunkSize % 2);

                    if (chunkId == "fmt ")
                    {
                        reader.ReadUInt16();          // audio format
                        reader.ReadUInt16();          // channels
                        reader.ReadUInt32();          // sample rate
                        byteRate = reader.ReadUInt32();
                    }
                    else if (chunkId == "data")
                    {
                        dataSize = chunkSize;
                        if (byteRate > 0) break;
                    }

                    if (next <= dataStart || next > fs.Length) break;
                    fs.Position = next;
                }

                if (byteRate <= 0 || dataSize <= 0) return TimeSpan.Zero;
                return TimeSpan.FromSeconds(dataSize / (double)byteRate);
            }
        }

        private static readonly int[] Mp3Bitrates =
        {
            0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0
        };

        private static TimeSpan ProbeMp3Duration(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var header = new byte[10];
                long start = 0;

                if (fs.Length > 10 && fs.Read(header, 0, 10) == 10 &&
                    header[0] == (byte)'I' && header[1] == (byte)'D' && header[2] == (byte)'3')
                {
                    start = 10 + ((header[6] & 0x7F) << 21 | (header[7] & 0x7F) << 14 | (header[8] & 0x7F) << 7 | (header[9] & 0x7F));
                    if ((header[5] & 0x10) != 0) start += 10; // footer
                }

                fs.Position = Math.Min(start, Math.Max(0, fs.Length - 4));

                var buffer = new byte[8192];
                int read = fs.Read(buffer, 0, buffer.Length);

                for (int i = 0; i + 4 < read; i++)
                {
                    if (buffer[i] != 0xFF || (buffer[i + 1] & 0xE0) != 0xE0) continue;

                    int versionBits = (buffer[i + 1] >> 3) & 0x03;
                    int layerBits = (buffer[i + 1] >> 1) & 0x03;
                    int bitrateIndex = (buffer[i + 2] >> 4) & 0x0F;

                    if (versionBits == 1 || layerBits == 0) continue;
                    if (bitrateIndex <= 0 || bitrateIndex >= 15) continue;

                    int bitrate = Mp3Bitrates[bitrateIndex] * 1000;
                    if (bitrate <= 0) continue;

                    long audioBytes = fs.Length - start;
                    if (audioBytes <= 0) return TimeSpan.Zero;
                    return TimeSpan.FromSeconds(audioBytes * 8.0 / bitrate);
                }
            }

            return TimeSpan.Zero;
        }

        private static Dictionary<string, string> ReadId3v2(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < 10) return result;

                var header = new byte[10];
                if (fs.Read(header, 0, 10) != 10) return result;
                if (header[0] != (byte)'I' || header[1] != (byte)'D' || header[2] != (byte)'3') return result;

                int major = header[3];
                int flags = header[5];
                int size = (header[6] & 0x7F) << 21 | (header[7] & 0x7F) << 14 | (header[8] & 0x7F) << 7 | (header[9] & 0x7F);
                if (size <= 0 || size > 8 * 1024 * 1024) return result;

                var buffer = new byte[size];
                int read = fs.Read(buffer, 0, size);
                int pos = 0;

                if ((flags & 0x40) != 0 && major >= 3 && read >= 4)
                {
                    int extSize = major == 4
                        ? ((buffer[0] & 0x7F) << 21 | (buffer[1] & 0x7F) << 14 | (buffer[2] & 0x7F) << 7 | (buffer[3] & 0x7F))
                        : (buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3]);
                    pos += extSize + (major == 4 ? 0 : 4);
                }

                int headerSize = major == 2 ? 6 : 10;
                while (pos + headerSize <= read)
                {
                    string id;
                    int frameSize;

                    if (major == 2)
                    {
                        id = Encoding.ASCII.GetString(buffer, pos, 3);
                        frameSize = buffer[pos + 3] << 16 | buffer[pos + 4] << 8 | buffer[pos + 5];
                    }
                    else
                    {
                        id = Encoding.ASCII.GetString(buffer, pos, 4);
                        frameSize = major == 4
                            ? ((buffer[pos + 4] & 0x7F) << 21 | (buffer[pos + 5] & 0x7F) << 14 | (buffer[pos + 6] & 0x7F) << 7 | (buffer[pos + 7] & 0x7F))
                            : (buffer[pos + 4] << 24 | buffer[pos + 5] << 16 | buffer[pos + 6] << 8 | buffer[pos + 7]);
                    }

                    if (string.IsNullOrWhiteSpace(id) || id[0] == '\0') break;
                    if (frameSize <= 0 || pos + headerSize + frameSize > read) break;

                    int dataStart = pos + headerSize;
                    var mapped = MapV22(id);
                    if (mapped != null)
                    {
                        var text = DecodeFrameText(buffer, dataStart, frameSize);
                        if (!string.IsNullOrWhiteSpace(text))
                            result[mapped] = text;
                    }

                    pos = dataStart + frameSize;
                }
            }

            return result;
        }

        private static string MapV22(string id)
        {
            switch (id)
            {
                case "TT2": return "TIT2";
                case "TP1": return "TPE1";
                case "TAL": return "TALB";
                case "TIT2":
                case "TPE1":
                case "TALB":
                    return id;
                default:
                    return null;
            }
        }

        private static string DecodeFrameText(byte[] buffer, int start, int length)
        {
            if (length <= 1) return string.Empty;
            byte encoding = buffer[start];
            int offset = start + 1;
            int count = length - 1;

            Encoding enc;
            switch (encoding)
            {
                case 0: enc = Encoding.GetEncoding("iso-8859-1"); break;
                case 1: enc = Encoding.Unicode; break;
                case 2: enc = Encoding.BigEndianUnicode; break;
                default: enc = Encoding.UTF8; break;
            }

            try
            {
                var text = enc.GetString(buffer, offset, count);

                // 有些写标签的工具会在 UTF-8 帧里塞 BOM，显示出来就是一个隐形方块
                text = text.Replace("\uFEFF", string.Empty).Replace("\u200B", string.Empty);

                var parts = text.Split(new[] { '\0', '/' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim().Trim('\uFEFF', '\u200B'))
                                .Where(p => p.Length > 0)
                                .ToArray();
                return string.Join(" / ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static (string title, string artist, string album)? ReadId3v1(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < 128) return null;
                    fs.Seek(-128, SeekOrigin.End);
                    var buf = new byte[128];
                    if (fs.Read(buf, 0, 128) != 128) return null;
                    if (buf[0] != (byte)'T' || buf[1] != (byte)'A' || buf[2] != (byte)'G') return null;

                    var enc = Encoding.GetEncoding("gb18030");
                    string title = enc.GetString(buf, 3, 30).TrimEnd('\0', ' ');
                    string artist = enc.GetString(buf, 33, 30).TrimEnd('\0', ' ');
                    string album = enc.GetString(buf, 63, 30).TrimEnd('\0', ' ');
                    return (title, artist, album);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
