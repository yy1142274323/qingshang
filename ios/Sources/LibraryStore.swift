import Foundation

/// 收藏和历史里存的一首歌。只存元数据 —— 在线直链有时效，播的时候再重新解析。
struct SavedSong: Codable, Identifiable, Equatable {
    let songId: String
    let source: String
    let name: String
    let artist: String
    let album: String
    let picId: String
    var savedAt: Date

    var id: String { "\(source)_\(songId)" }

    var displayArtist: String { artist.isEmpty ? "未知艺术家" : artist }

    init(songId: String, source: String, name: String, artist: String,
         album: String, picId: String, savedAt: Date = Date()) {
        self.songId = songId
        self.source = source
        self.name = name
        self.artist = artist
        self.album = album
        self.picId = picId
        self.savedAt = savedAt
    }

    init(_ online: OnlineSong) {
        self.init(songId: online.id, source: online.source, name: online.name,
                  artist: online.artist, album: online.album, picId: online.picId)
    }

    var asOnline: OnlineSong {
        OnlineSong(id: songId, name: name, artist: artist, album: album,
                   source: source, picId: picId)
    }
}

/// 收藏 + 播放历史，存成 Documents/library.json。
@MainActor
final class LibraryStore: ObservableObject {
    @Published private(set) var favorites: [SavedSong] = []
    @Published private(set) var history: [SavedSong] = []

    private let maxHistory = 120
    private let fileURL: URL

    init() {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        fileURL = docs.appendingPathComponent("library.json")
        load()
    }

    func isFavorite(_ song: OnlineSong) -> Bool {
        favorites.contains { $0.songId == song.id && $0.source == song.source }
    }

    func toggleFavorite(_ song: OnlineSong) {
        if let idx = favorites.firstIndex(where: { $0.songId == song.id && $0.source == song.source }) {
            favorites.remove(at: idx)
        } else {
            favorites.insert(SavedSong(song), at: 0)
        }
        save()
    }

    func removeFavorite(_ saved: SavedSong) {
        favorites.removeAll { $0.id == saved.id }
        save()
    }

    /// 播放时塞进历史，同一首重复播放只更新时间并置顶。
    func record(_ song: OnlineSong) {
        history.removeAll { $0.songId == song.id && $0.source == song.source }
        var s = SavedSong(song)
        s.savedAt = Date()
        history.insert(s, at: 0)
        if history.count > maxHistory {
            history = Array(history.prefix(maxHistory))
        }
        save()
    }

    func clearHistory() {
        history.removeAll()
        save()
    }

    // MARK: - 存取

    private struct Payload: Codable {
        var favorites: [SavedSong]
        var history: [SavedSong]
    }

    private func load() {
        guard let data = try? Data(contentsOf: fileURL),
              let p = try? JSONDecoder().decode(Payload.self, from: data) else { return }
        favorites = p.favorites
        history = p.history
    }

    private func save() {
        let p = Payload(favorites: favorites, history: history)
        guard let data = try? JSONEncoder().encode(p) else { return }
        try? data.write(to: fileURL, options: .atomic)
    }
}
