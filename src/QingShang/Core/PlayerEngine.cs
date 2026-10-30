using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QingShang.Core
{
    public enum PlayMode
    {
        ListLoop,     // 列表循环
        SingleLoop,   // 单曲循环
        Shuffle,      // 随机
        Sequential    // 顺序播放，播完停
    }

    /// <summary>
    /// 播放引擎：播放列表 + MCI 播放器 + 100ms 心跳。
    /// 所有事件都在 UI 线程上触发。
    /// </summary>
    public sealed class PlayerEngine : IDisposable
    {
        private readonly MciPlayer _mci = new MciPlayer();
        private readonly Timer _ticker;
        private readonly Random _random = new Random();
        private readonly List<int> _shuffleBag = new List<int>();
        private readonly List<Track> _items = new List<Track>();

        private int _currentIndex = -1;
        private bool _isPlaying;
        private bool _wasPlaying;
        private int _volume = 800;
        private bool _muted;
        private bool _expectingEnd;

        public PlayerEngine()
        {
            _ticker = new Timer { Interval = 100 };
            _ticker.Tick += OnTick;
        }

        public IReadOnlyList<Track> Items => _items;

        public PlayMode Mode { get; set; } = PlayMode.ListLoop;

        public Track Current => _currentIndex >= 0 && _currentIndex < _items.Count ? _items[_currentIndex] : null;

        public int CurrentIndex => _currentIndex;

        public bool IsPlaying => _isPlaying;

        public bool IsMuted => _muted;

        public double Volume
        {
            get => _volume / 1000.0;
            set => SetVolume((int)Math.Round(Math.Max(0, Math.Min(1, value)) * 1000));
        }

        public int VolumeRaw => _volume;

        public TimeSpan Duration
        {
            get
            {
                var track = Current;
                if (track != null && track.Duration > TimeSpan.Zero) return track.Duration;
                return _mci.Duration;
            }
        }

        public TimeSpan Position => _mci.IsOpen ? _mci.Position : TimeSpan.Zero;

        public event EventHandler StateChanged;
        public event EventHandler TrackChanged;
        public event EventHandler PositionChanged;
        public event EventHandler PlaylistChanged;
        public event EventHandler<string> ErrorOccurred;

        // ---------------- 播放列表 ----------------

        public int AddFiles(IEnumerable<string> paths, bool autoPlayIfEmpty)
        {
            var found = new List<string>();
            var seen = new HashSet<string>(_items.Select(t => t.FilePath), StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                // 播放列表文件：把里面所有的条目展开加入
                if (PlaylistFile.IsPlaylist(path))
                {
                    foreach (var entry in PlaylistFile.Read(path))
                    {
                        if (File.Exists(entry) && TagReader.IsSupported(entry) && seen.Add(entry))
                            found.Add(entry);
                    }
                    continue;
                }

                if (Directory.Exists(path))
                {
                    foreach (var file in EnumerateAudio(path))
                        if (seen.Add(file)) found.Add(file);
                }
                else if (File.Exists(path) && TagReader.IsSupported(path))
                {
                    if (seen.Add(path)) found.Add(path);
                }
            }

            found.Sort(StringComparer.CurrentCultureIgnoreCase);

            foreach (var file in found)
            {
                var track = new Track(file);
                TagReader.Apply(track);
                _items.Add(track);
            }

            if (found.Count > 0)
            {
                _shuffleBag.Clear();
                PlaylistChanged?.Invoke(this, EventArgs.Empty);
                if (autoPlayIfEmpty && Current == null)
                    PlayAt(_items.Count - found.Count);
            }

            return found.Count;
        }

        private static IEnumerable<string> EnumerateAudio(string dir)
        {
            var results = new List<string>();
            var stack = new Stack<string>();
            stack.Push(dir);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(current))
                        if (TagReader.IsSupported(file))
                            results.Add(file);
                }
                catch
                {
                    // 目录不可读就跳过
                }

                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(current))
                        stack.Push(sub);
                }
                catch
                {
                    // 忽略
                }
            }

            return results;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            bool isCurrent = index == _currentIndex;
            _items.RemoveAt(index);
            _shuffleBag.Clear();

            if (isCurrent)
            {
                StopInternal();
                _currentIndex = -1;
                TrackChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (index < _currentIndex)
            {
                _currentIndex--;
            }

            PlaylistChanged?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            StopInternal();
            _items.Clear();
            _currentIndex = -1;
            _shuffleBag.Clear();
            PlaylistChanged?.Invoke(this, EventArgs.Empty);
            TrackChanged?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------------- 播放控制 ----------------

        public void PlayAt(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            if (index == _currentIndex && _mci.IsOpen)
            {
                if (!_isPlaying) Resume();
                return;
            }

            var track = _items[index];
            if (!_mci.Open(track.FilePath, out var error))
            {
                ErrorOccurred?.Invoke(this, error);
                _isPlaying = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (_currentIndex >= 0 && _currentIndex < _items.Count && _currentIndex != index)
                _items[_currentIndex].IsPlaying = false;

            _currentIndex = index;
            track.IsPlaying = true;
            if (_mci.Duration > TimeSpan.Zero) track.Duration = _mci.Duration;

            _mci.SetVolume(_muted ? 0 : _volume);
            _mci.Play();
            _isPlaying = true;
            _expectingEnd = false;
            _ticker.Start();

            TrackChanged?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void TogglePlay()
        {
            if (Current == null)
            {
                if (_items.Count > 0) PlayAt(_currentIndex >= 0 ? _currentIndex : 0);
                return;
            }

            if (_isPlaying) Pause();
            else Resume();
        }

        public void Pause()
        {
            if (!_mci.IsOpen) return;
            _mci.Pause();
            _isPlaying = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Resume()
        {
            if (!_mci.IsOpen) return;
            _mci.Resume();
            _isPlaying = true;
            _expectingEnd = false;
            _ticker.Start();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            StopInternal();
            StateChanged?.Invoke(this, EventArgs.Empty);
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StopInternal()
        {
            _mci.Stop();
            _isPlaying = false;
            _expectingEnd = false;
        }

        public void Next(bool fromEnd = false)
        {
            if (_items.Count == 0) return;

            if (!fromEnd && Mode == PlayMode.SingleLoop && _currentIndex >= 0)
            {
                _mci.Seek(TimeSpan.Zero);
                Resume();
                return;
            }

            int next = PickNext(fromEnd);
            if (next >= 0) PlayAt(next);
        }

        public void Previous()
        {
            if (_items.Count == 0) return;

            // 播了 3 秒以上，先回到本曲开头
            if (Position.TotalSeconds > 3 && _mci.IsOpen)
            {
                Seek(TimeSpan.Zero);
                return;
            }

            int prev = _currentIndex <= 0 ? _items.Count - 1 : _currentIndex - 1;
            PlayAt(prev);
        }

        private int PickNext(bool fromEnd)
        {
            if (_items.Count == 0) return -1;

            switch (Mode)
            {
                case PlayMode.Shuffle:
                {
                    if (_shuffleBag.Count == 0)
                    {
                        _shuffleBag.AddRange(Enumerable.Range(0, _items.Count).OrderBy(_ => _random.Next()));
                        if (_items.Count > 1 && _shuffleBag[0] == _currentIndex)
                        {
                            var last = _shuffleBag[_shuffleBag.Count - 1];
                            _shuffleBag[_shuffleBag.Count - 1] = _shuffleBag[0];
                            _shuffleBag[0] = last;
                        }
                    }
                    int pick = _shuffleBag[0];
                    _shuffleBag.RemoveAt(0);
                    return pick;
                }

                case PlayMode.Sequential:
                {
                    int next = _currentIndex + 1;
                    return next >= _items.Count ? -1 : next;
                }

                default:
                {
                    int next = _currentIndex + 1;
                    return next >= _items.Count ? 0 : next;
                }
            }
        }

        public void Seek(TimeSpan position)
        {
            if (!_mci.IsOpen) return;
            _mci.Seek(position);
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetVolume(int raw)
        {
            _volume = Math.Max(0, Math.Min(1000, raw));
            _mci.SetVolume(_muted ? 0 : _volume);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleMute()
        {
            _muted = !_muted;
            _mci.SetVolume(_muted ? 0 : _volume);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------------- 心跳 ----------------

        private void OnTick(object sender, EventArgs e)
        {
            if (!_mci.IsOpen) return;

            PositionChanged?.Invoke(this, EventArgs.Empty);

            var state = _mci.State();
            var duration = Duration;
            var position = _mci.Position;

            bool reachedEnd = duration > TimeSpan.Zero
                              && position >= duration - TimeSpan.FromMilliseconds(350);

            if (state == "stopped" && (_wasPlaying || _expectingEnd || reachedEnd))
            {
                _ticker.Stop();
                _isPlaying = false;
                _wasPlaying = false;
                _expectingEnd = false;

                if (Mode == PlayMode.Sequential && _currentIndex >= _items.Count - 1)
                {
                    StateChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }

                Next(true);
                return;
            }

            if (state == "paused" && _isPlaying)
            {
                _isPlaying = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }

            _wasPlaying = state == "playing";
        }

        public void Dispose()
        {
            _ticker.Stop();
            _ticker.Dispose();
            _mci.Dispose();
        }
    }
}
