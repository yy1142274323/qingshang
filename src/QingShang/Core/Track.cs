using System;
using System.ComponentModel;
using System.IO;

namespace QingShang.Core
{
    /// <summary>
    /// 播放列表里的一条曲目。显示字段优先取标签，缺失时回退到文件名。
    /// </summary>
    public sealed class Track : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _artist = string.Empty;
        private string _album = string.Empty;
        private TimeSpan _duration;
        private bool _isPlaying;

        public Track(string filePath)
        {
            FilePath = filePath;
            _title = CleanFileName(Path.GetFileNameWithoutExtension(filePath));
        }

        /// <summary>去掉文件名前面的序号，像 "01 静听·夏夜" → "静听·夏夜"。</summary>
        private static string CleanFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var trimmed = System.Text.RegularExpressions.Regex.Replace(
                name, @"^\s*\d{1,3}\s*[\.\-_、]?\s+", string.Empty).Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? name : trimmed;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string FilePath { get; }

        public string FileName => Path.GetFileName(FilePath);

        public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

        public string Title
        {
            get => _title;
            set
            {
                var v = string.IsNullOrWhiteSpace(value) ? Path.GetFileNameWithoutExtension(FilePath) : value.Trim();
                if (_title == v) return;
                _title = v;
                Raise(nameof(Title));
                Raise(nameof(DisplayTitle));
            }
        }

        public string Artist
        {
            get => _artist;
            set
            {
                var v = (value ?? string.Empty).Trim();
                if (_artist == v) return;
                _artist = v;
                Raise(nameof(Artist));
                Raise(nameof(DisplayArtist));
            }
        }

        public string Album
        {
            get => _album;
            set
            {
                var v = (value ?? string.Empty).Trim();
                if (_album == v) return;
                _album = v;
                Raise(nameof(Album));
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (_duration == value) return;
                _duration = value;
                Raise(nameof(Duration));
                Raise(nameof(DurationText));
            }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying == value) return;
                _isPlaying = value;
                Raise(nameof(IsPlaying));
            }
        }

        public string DisplayTitle => _title;

        public string DisplayArtist => string.IsNullOrWhiteSpace(_artist) ? "未知艺术家" : _artist;

        public string DurationText => _duration <= TimeSpan.Zero ? "--:--" : _duration.ToString(@"mm\:ss");

        public string SearchKey => (_title + " " + _artist + " " + _album + " " + FileName).ToLowerInvariant();

        private void Raise(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString() => DisplayTitle + " - " + DisplayArtist;
    }
}
