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
    var picURL: String = ""

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

    /// 实测只有这两个源能返回数据，其余一律 400，不再列出来误导人。
    static let sources: [(id: String, name: String)] = [
        ("netease", "网易云"),
        ("joox", "JOOX")
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
        if !song.picURL.isEmpty {
            // 歌单接口直接给了封面地址，比再走一次 pic 接口快
            if song.picURL.hasPrefix("http") { return URL(string: song.picURL) }
        }
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

// MARK: - 榜单与歌单

struct RankItem: Identifiable, Hashable {
    let id: String
    let name: String
    let note: String
    let tint: Int     // 0..5，用来挑封面底色
}

extension OnlineSource {
    /// 这些都是实测能返回完整曲目列表的网易云歌单 ID。
    static let ranks: [RankItem] = [
        RankItem(id: "3778678",    name: "热歌榜",     note: "200 首",  tint: 0),
        RankItem(id: "3779629",    name: "新歌榜",     note: "100 首",  tint: 1),
        RankItem(id: "19723756",   name: "飙升榜",     note: "100 首",  tint: 2),
        RankItem(id: "2884035",    name: "原创榜",     note: "100 首",  tint: 3),
        RankItem(id: "6723173524", name: "网络热歌榜", note: "100 首",  tint: 4),
        RankItem(id: "2809513713", name: "欧美热歌榜", note: "200 首",  tint: 5),
        RankItem(id: "5059642708", name: "国风榜",     note: "50 首",   tint: 0),
        RankItem(id: "71384707",   name: "古典榜",     note: "100 首",  tint: 1),
        RankItem(id: "1978921795", name: "电音榜",     note: "50 首",   tint: 2),
        RankItem(id: "5059644681", name: "日语榜",     note: "100 首",  tint: 3),
        RankItem(id: "745956260",  name: "韩语榜",     note: "100 首",  tint: 4)
    ]

    /// 拉一张歌单的完整曲目。
    func loadPlaylist(id: String, source: String = "netease") async -> [OnlineSong] {
        guard var comp = URLComponents(string: baseURL) else { return [] }
        comp.queryItems = [
            URLQueryItem(name: "types", value: "playlist"),
            URLQueryItem(name: "source", value: source),
            URLQueryItem(name: "id", value: id)
        ]
        guard let url = comp.url else { return [] }

        do {
            var req = URLRequest(url: url)
            req.timeoutInterval = 25
            req.setValue("QingShang/1.2", forHTTPHeaderField: "User-Agent")

            let (data, _) = try await URLSession.shared.data(for: req)
            guard let obj = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let pl = obj["playlist"] as? [String: Any],
                  let tracks = pl["tracks"] as? [[String: Any]] else { return [] }

            return tracks.compactMap { t in
                guard let name = t["name"] as? String else { return nil }
                let rawId: String
                if let i = t["id"] as? Int { rawId = String(i) }
                else if let s = t["id"] as? String { rawId = s }
                else if let n = t["id"] as? NSNumber { rawId = n.stringValue }
                else { return nil }

                let artists = (t["ar"] as? [[String: Any]])?
                    .compactMap { $0["name"] as? String }
                    .joined(separator: " / ") ?? ""

                let al = t["al"] as? [String: Any]
                let album = al?["name"] as? String ?? ""
                let pic = al?["picUrl"] as? String ?? ""

                return OnlineSong(id: rawId, name: name, artist: artists,
                                  album: album, source: source, picId: "", picURL: pic)
            }
        } catch {
            return []
        }
    }
}