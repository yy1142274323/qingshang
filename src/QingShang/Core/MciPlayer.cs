using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QingShang.Core
{
    /// <summary>
    /// 用 Windows MCI（winmm.dll）播放音频。系统自带，不依赖 WPF / Media Foundation。
    /// 支持 mp3 / wav / wma 等 MCI 能识别的格式。
    /// </summary>
    public sealed class MciPlayer : IDisposable
    {
        private const string Alias = "qqpTrack";

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        private bool _open;
        private string _lastError = string.Empty;

        public bool IsOpen => _open;

        public string LastError => _lastError;

        public TimeSpan Duration { get; private set; } = TimeSpan.Zero;

        /// <summary>0..1000，MCI 的音量刻度。</summary>
        public int Volume { get; private set; } = 800;

        public bool Open(string path, out string error)
        {
            error = null;
            Close();

            if (!File.Exists(path))
            {
                error = "文件不存在";
                return false;
            }

            var full = Path.GetFullPath(path);
            int rc = Send($"open \"{full}\" alias {Alias}");
            if (rc != 0)
            {
                _lastError = "MCI open 失败，错误码 " + rc;
                error = "这个格式当前系统播放不了：" + Path.GetFileName(path);
                Diag.Write("MCI open failed rc=" + rc + " file=" + full);
                return false;
            }

            _open = true;

            var lengthText = Query($"status {Alias} length");
            if (long.TryParse(lengthText, out var ms) && ms > 0)
                Duration = TimeSpan.FromMilliseconds(ms);
            else
                Duration = TimeSpan.Zero;

            ApplyVolume();
            return true;
        }

        public void Play()
        {
            if (!_open) return;
            Send($"play {Alias}");
        }

        public void Pause()
        {
            if (!_open) return;
            Send($"pause {Alias}");
        }

        public void Resume()
        {
            if (!_open) return;
            Send($"resume {Alias}");
        }

        public void Stop()
        {
            if (!_open) return;
            Send($"stop {Alias}");
            Send($"seek {Alias} to 0");
        }

        public void Close()
        {
            if (!_open) return;
            Send($"close {Alias}");
            _open = false;
            Duration = TimeSpan.Zero;
        }

        public TimeSpan Position
        {
            get
            {
                if (!_open) return TimeSpan.Zero;
                var text = Query($"status {Alias} position");
                return long.TryParse(text, out var ms) ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;
            }
        }

        public void Seek(TimeSpan position)
        {
            if (!_open) return;
            long ms = Math.Max(0, (long)position.TotalMilliseconds);
            if (Duration.TotalMilliseconds > 0 && ms > Duration.TotalMilliseconds)
                ms = (long)Duration.TotalMilliseconds;
            Send($"seek {Alias} to {ms}");
        }

        /// <summary>playing / paused / stopped / not ready</summary>
        public string State()
        {
            if (!_open) return "stopped";
            return Query($"status {Alias} mode").Trim().ToLowerInvariant();
        }

        public void SetVolume(int volume)
        {
            Volume = Math.Max(0, Math.Min(1000, volume));
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (!_open) return;
            Send($"setaudio {Alias} volume to {Volume}");
        }

        private int Send(string command)
        {
            try
            {
                return mciSendString(command, null, 0, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Diag.Error("MCI", ex);
                return -1;
            }
        }

        private string Query(string command)
        {
            try
            {
                var buffer = new StringBuilder(256);
                int rc = mciSendString(command, buffer, buffer.Capacity, IntPtr.Zero);
                if (rc != 0) return string.Empty;
                return buffer.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
