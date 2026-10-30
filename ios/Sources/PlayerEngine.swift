import Foundation
import AVFoundation
import MediaPlayer
import Combine

/// 播放引擎：AVPlayer 统一处理本地文件和网络流，支持后台播放和锁屏控制。
@MainActor
final class PlayerEngine: ObservableObject {
    @Published private(set) var tracks: [Track] = []
    @Published private(set) var currentIndex: Int = -1
    @Published private(set) var isPlaying: Bool = false
    @Published private(set) var position: Double = 0
    @Published private(set) var duration: Double = 0
    @Published private(set) var isBuffering: Bool = false
    @Published var repeatOne: Bool = false

    private var player: AVPlayer?
    private var timeObserver: Any?
    private var endToken: NSObjectProtocol?
    private var statusObservation: NSKeyValueObservation?

    var current: Track? {
        guard currentIndex >= 0 && currentIndex < tracks.count else { return nil }
        return tracks[currentIndex]
    }

    init() {
        configureSession()
        configureRemoteCommands()
    }

    private func configureSession() {
        let session = AVAudioSession.sharedInstance()
        try? session.setCategory(.playback, mode: .default, options: [])
        try? session.setActive(true)
    }

    private func configureRemoteCommands() {
        let center = MPRemoteCommandCenter.shared()

        center.playCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.resume() }
            return .success
        }
        center.pauseCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.pause() }
            return .success
        }
        center.togglePlayPauseCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.toggle() }
            return .success
        }
        center.nextTrackCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.next() }
            return .success
        }
        center.previousTrackCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.previous() }
            return .success
        }
        center.changePlaybackPositionCommand.addTarget { [weak self] event in
            guard let e = event as? MPChangePlaybackPositionCommandEvent else { return .commandFailed }
            Task { @MainActor in self?.seek(to: e.positionTime) }
            return .success
        }
    }

    // MARK: - 本地列表

    func reload() {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        let urls = AudioFiles.scan(docs)

        let keepStreams = tracks.filter { $0.isStream }
        let previous = current?.url

        var local: [Track] = urls.map {
            Track(url: $0, title: AudioFiles.cleanName($0.deletingPathExtension().lastPathComponent), artist: "")
        }
        local.append(contentsOf: keepStreams)
        tracks = local

        if let prev = previous, let idx = tracks.firstIndex(where: { $0.url == prev }) {
            currentIndex = idx
        }

        for (index, url) in urls.enumerated() {
            AudioFiles.makeTrack(from: url) { [weak self] meta in
                guard let self, index < self.tracks.count, self.tracks[index].url == meta.url else { return }
                self.tracks[index] = meta
            }
        }
    }

    // MARK: - 播放

    func play(at index: Int) {
        guard index >= 0 && index < tracks.count else { return }

        currentIndex = index
        let track = tracks[index]

        teardown()

        let item = AVPlayerItem(url: track.url)
        let p = AVPlayer(playerItem: item)
        p.volume = 1.0
        player = p
        duration = track.duration
        position = 0
        isBuffering = track.isStream

        statusObservation = item.observe(\.status, options: [.new]) { [weak self] it, _ in
            guard it.status == .readyToPlay else { return }
            Task { @MainActor in
                guard let self else { return }
                self.isBuffering = false
                let d = CMTimeGetSeconds(it.duration)
                if d.isFinite && d > 0 {
                    self.duration = d
                    if index < self.tracks.count { self.tracks[index].duration = d }
                }
            }
        }

        endToken = NotificationCenter.default.addObserver(
            forName: .AVPlayerItemDidPlayToEndTime,
            object: item,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in self?.handleFinish() }
        }

        timeObserver = p.addPeriodicTimeObserver(
            forInterval: CMTime(seconds: 0.25, preferredTimescale: 600),
            queue: .main
        ) { [weak self] t in
            Task { @MainActor in
                guard let self else { return }
                let s = t.seconds
                if s.isFinite { self.position = s }
            }
        }

        p.play()
        isPlaying = true
        updateNowPlaying()
    }

    /// 播一首在线歌：已经在列表里就复用，否则追加到队尾。
    func playOnline(_ track: Track) {
        if let idx = tracks.firstIndex(where: { $0.url == track.url }) {
            play(at: idx)
            return
        }
        tracks.append(track)
        play(at: tracks.count - 1)
    }

    func toggle() {
        if current == nil {
            if !tracks.isEmpty { play(at: 0) }
            return
        }
        isPlaying ? pause() : resume()
    }

    func pause() {
        player?.pause()
        isPlaying = false
        updateNowPlaying()
    }

    func resume() {
        if player == nil {
            if currentIndex >= 0 { play(at: currentIndex) }
            return
        }
        player?.play()
        isPlaying = true
        updateNowPlaying()
    }

    func next() {
        guard !tracks.isEmpty else { return }
        let n = currentIndex + 1
        play(at: n >= tracks.count ? 0 : n)
    }

    func previous() {
        guard !tracks.isEmpty else { return }
        if position > 3 {
            seek(to: 0)
            return
        }
        play(at: currentIndex <= 0 ? tracks.count - 1 : currentIndex - 1)
    }

    func seek(to time: Double) {
        guard let p = player else { return }
        let clamped = max(0, min(duration > 0 ? duration : time, time))
        p.seek(to: CMTime(seconds: clamped, preferredTimescale: 600),
               toleranceBefore: .zero, toleranceAfter: .zero)
        position = clamped
        updateNowPlaying()
    }

    private func handleFinish() {
        if repeatOne {
            seek(to: 0)
            resume()
        } else {
            next()
        }
    }

    private func teardown() {
        if let t = timeObserver, let p = player {
            p.removeTimeObserver(t)
            timeObserver = nil
        }
        if let e = endToken {
            NotificationCenter.default.removeObserver(e)
            endToken = nil
        }
        statusObservation?.invalidate()
        statusObservation = nil
        player?.pause()
        player = nil
    }

    // MARK: - 锁屏

    private func updateNowPlaying() {
        guard let track = current else {
            MPNowPlayingInfoCenter.default().nowPlayingInfo = nil
            return
        }

        var info: [String: Any] = [
            MPMediaItemPropertyTitle: track.title,
            MPMediaItemPropertyArtist: track.displayArtist,
            MPNowPlayingInfoPropertyElapsedPlaybackTime: position,
            MPNowPlayingInfoPropertyPlaybackRate: isPlaying ? 1.0 : 0.0
        ]
        if duration > 0 {
            info[MPMediaItemPropertyPlaybackDuration] = duration
        }
        MPNowPlayingInfoCenter.default().nowPlayingInfo = info
    }
}
