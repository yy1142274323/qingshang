import Foundation
import AVFoundation
import MediaPlayer
import Combine

/// 播放引擎：AVAudioPlayer + 后台播放 + 锁屏控制 + 100ms 心跳。
@MainActor
final class PlayerEngine: ObservableObject {
    @Published private(set) var tracks: [Track] = []
    @Published private(set) var currentIndex: Int = -1
    @Published private(set) var isPlaying: Bool = false
    @Published private(set) var position: Double = 0
    @Published private(set) var duration: Double = 0
    @Published var repeatOne: Bool = false

    private var player: AVAudioPlayer?
    private var ticker: Timer?
    private var loadToken = UUID()

    var current: Track? {
        guard currentIndex >= 0 && currentIndex < tracks.count else { return nil }
        return tracks[currentIndex]
    }

    // MARK: - 初始化

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

    // MARK: - 播放列表

    func reload() {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        let urls = AudioFiles.scan(docs)

        let previous = current?.url
        tracks = urls.map { Track(url: $0, title: AudioFiles.cleanName($0.deletingPathExtension().lastPathComponent), artist: "") }

        if let prev = previous, let idx = tracks.firstIndex(where: { $0.url == prev }) {
            currentIndex = idx
        } else if currentIndex >= tracks.count {
            currentIndex = tracks.isEmpty ? -1 : tracks.count - 1
        }

        // 补齐元数据
        for (index, url) in urls.enumerated() {
            AudioFiles.makeTrack(from: url) { [weak self] meta in
                guard let self, index < self.tracks.count, self.tracks[index].url == meta.url else { return }
                var merged = meta
                self.tracks[index] = merged
                if index == self.currentIndex, self.duration == 0 {
                    self.duration = merged.duration
                }
            }
        }
    }

    // MARK: - 控制

    func play(at index: Int) {
        guard index >= 0 && index < tracks.count else { return }
        currentIndex = index
        let track = tracks[index]

        let token = UUID()
        loadToken = token

        do {
            let p = try AVAudioPlayer(contentsOf: track.url)
            p.delegate = PlayerDelegate.shared
            p.prepareToPlay()
            p.volume = 1.0
            p.play()

            player = p
            isPlaying = true
            duration = p.duration > 0 ? p.duration : track.duration
            position = 0

            PlayerDelegate.shared.onFinish = { [weak self] in
                Task { @MainActor in self?.handleFinish() }
            }

            startTicker()
            updateNowPlaying()
        } catch {
            isPlaying = false
            player = nil
        }
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
        stopTicker()
        updateNowPlaying()
    }

    func resume() {
        if player == nil {
            if currentIndex >= 0 { play(at: currentIndex) }
            return
        }
        player?.play()
        isPlaying = true
        startTicker()
        updateNowPlaying()
    }

    func next() {
        guard !tracks.isEmpty else { return }
        let nextIndex = currentIndex + 1
        play(at: nextIndex >= tracks.count ? 0 : nextIndex)
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
        let clamped = max(0, min(p.duration, time))
        p.currentTime = clamped
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

    // MARK: - 心跳

    private func startTicker() {
        stopTicker()
        ticker = Timer.scheduledTimer(withTimeInterval: 0.25, repeats: true) { [weak self] _ in
            Task { @MainActor in
                guard let self, let p = self.player else { return }
                self.position = p.currentTime
                if p.duration > 0 { self.duration = p.duration }
            }
        }
    }

    private func stopTicker() {
        ticker?.invalidate()
        ticker = nil
    }

    // MARK: - 锁屏信息

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

/// AVAudioPlayer 的 delegate 需要 NSObject，单独拎一个单例出来。
final class PlayerDelegate: NSObject, AVAudioPlayerDelegate {
    static let shared = PlayerDelegate()
    var onFinish: (() -> Void)?

    func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        onFinish?()
    }
}
