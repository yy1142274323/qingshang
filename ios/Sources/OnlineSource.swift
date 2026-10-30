import Foundation
import AVFoundation
import MediaPlayer
import Combine

/// 在线音源。默认接 GD 音乐台聚合 API（地址可在设置里改）。
struct OnlineSong: Identifiable, Equatable {
    let id: String
    let name: String
    let artist: String
    let album: String
    let source: String
    let picId: String

    var displayArtist: String { artist.isEmpty ? "未知艺术家" : artist }
}

@MainActor
final class OnlineSource: ObservableObject {
    @Published var baseURL: String {
        didSet { UserDefaults.standard.set(baseURL, forKey: "qs.api.base") }
    }
    @Published var selectedSource: String = "netease"
    @Published var results: [OnlineSong] = []
    @Published var isSearching = false
    @Published var lastError: String?

    static let sources: [(id: String, name: String)] = [
        ("netease", "网易云"),
        ("tencent", "QQ音乐"),
        ("kugou", "酷狗"),
        ("kuwo", "酷我"),
        ("migu", "咪咕"),
        ("joox", "JOOX"),
        ("deezer", "Deezer")
    ]

    init() {
        baseURL = UserDefaults.standard.string(forKey: "qs.api.base")
            ?? "https://music-api.gdstudio.xyz/api.php"
    }

    func search(_ keyword: String, page: Int = 1, count: Int = 30) async {
        let trimmed = keyword.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }

        isSearching = true
        lastError = nil
        defer { isSearching = false }

        guard var comp = URLComponents(string: baseURL) else {
            lastError = "API 地址不合法"
            return
        }
        comp.queryItems = [
            URLQueryItem(name: "types", value: "search"),
            URLQueryItem(name: "source", value: selectedSource),
            URLQueryItem(name: "name", value: trimmed),
            URLQueryItem(name: "count", value: String(count)),
            URLQueryItem(name: "pages", value: String(page))
        ]

        guard let url = comp.url else { return }

        do {
            var req = URLRequest(url: url)
            req.timeoutInterval = 20
            req.setValue("QingShang/1.1", forHTTPHeaderField: "User-Agent")

            let (data, _) = try await URLSession.shared.data(for: req)
            let rows = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] ?? []

            results = rows.compactMap { row in
                guard let id = row["id"] as? String ?? (row["id"].map { "\($0)" }),
                      let name = row["name"] as? String else { return nil }

                let artists = (row["artist"] as? [String])?.joined(separator: " / ") ?? ""
                return OnlineSong(
                    id: id,
                    name: name,
                    artist: artists,
                    album: row["album"] as? String ?? "",
                    source: row["source"] as? String ?? selectedSource,
                    picId: row["pic_id"] as? String ?? ""
                )
            }

            if results.isEmpty { lastError = "没有找到结果" }
        } catch {
            lastError = "搜索失败：" + error.localizedDescription
            results = []
        }
    }

    /// 取播放直链
    func resolveURL(for song: OnlineSong, bitrate: Int = 320) async -> URL? {
        guard var comp = URLComponents(string: baseURL) else { return nil }
        comp.queryItems = [
            URLQueryItem(name: "types", value: "url"),
            URLQueryItem(name: "source", value: song.source),
            URLQueryItem(name: "id", value: song.id),
            URLQueryItem(name: "br", value: String(bitrate))
        ]
        guard let url = comp.url else { return nil }

        do {
            var req = URLRequest(url: url)
            req.timeoutInterval = 20
            let (data, _) = try await URLSession.shared.data(for: req)
            let obj = try JSONSerialization.jsonObject(with: data) as? [String: Any]
            guard let raw = obj?["url"] as? String, !raw.isEmpty else { return nil }
            return URL(string: raw)
        } catch {
            return nil
        }
    }

    /// 封面图地址
    func artworkURL(for song: OnlineSong) -> URL? {
        guard !song.picId.isEmpty,
              var comp = URLComponents(string: baseURL) else { return nil }
        comp.queryItems = [
            URLQueryItem(name: "types", value: "pic"),
            URLQueryItem(name: "source", value: song.source),
            URLQueryItem(name: "id", value: song.picId),
            URLQueryItem(name: "size", value: "300")
        ]
        return comp.url
    }

    func toTrack(_ song: OnlineSong, url: URL) -> Track {
        let name = Self.sources.first { $0.id == song.source }?.name ?? song.source
        return Track(
            url: url,
            title: song.name,
            artist: song.artist,
            isStream: true,
            artwork: artworkURL(for: song),
            sourceTag: name
        )
    }
}
