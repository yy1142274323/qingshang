import Foundation
import AVFoundation

/// 一首歌。iOS 这边不做 ID3 深度解析，AVAsset 能读到的元数据就用，读不到退回文件名。
struct Track: Identifiable, Equatable {
    let id = UUID()
    let url: URL
    var title: String
    var artist: String
    var duration: Double = 0

    var displayArtist: String {
        artist.isEmpty ? "未知艺术家" : artist
    }

    static func == (a: Track, b: Track) -> Bool {
        a.url == b.url
    }
}

enum AudioFiles {
    static let extensions: Set<String> = [
        "mp3", "m4a", "aac", "wav", "aiff", "aif", "flac", "caf", "mp4", "m4b"
    ]

    static func isAudio(_ url: URL) -> Bool {
        extensions.contains(url.pathExtension.lowercased())
    }

    /// 递归扫一个目录，把能放的音频都捞出来
    static func scan(_ root: URL) -> [URL] {
        var found: [URL] = []
        let keys: [URLResourceKey] = [.isRegularFileKey, .nameKey]

        guard let en = FileManager.default.enumerator(
            at: root,
            includingPropertiesForKeys: keys,
            options: [.skipsHiddenFiles, .skipsPackageDescendants]
        ) else { return [] }

        for case let url as URL in en {
            if isAudio(url) { found.append(url) }
        }

        return found.sorted { $0.lastPathComponent.localizedStandardCompare($1.lastPathComponent) == .orderedAscending }
    }

    /// 读标题/歌手/时长，异步完成回调
    static func makeTrack(from url: URL, completion: @escaping (Track) -> Void) {
        let fallbackTitle = cleanName(url.deletingPathExtension().lastPathComponent)
        let asset = AVURLAsset(url: url)

        asset.loadValuesAsynchronously(forKeys: ["commonMetadata", "duration"]) {
            var title = fallbackTitle
            var artist = ""
            var duration: Double = 0

            if asset.statusOfValue(forKey: "commonMetadata", error: nil) == .loaded {
                let items = asset.commonMetadata
                for item in items {
                    guard let key = item.commonKey else { continue }
                    if key == .commonKeyTitle, let v = try? item.value as? String, !v.isEmpty {
                        title = v
                    } else if key == .commonKeyArtist, let v = try? item.value as? String, !v.isEmpty {
                        artist = v
                    }
                }
            }

            if asset.statusOfValue(forKey: "duration", error: nil) == .loaded {
                let d = CMTimeGetSeconds(asset.duration)
                if d.isFinite && d > 0 { duration = d }
            }

            let track = Track(url: url, title: title, artist: artist, duration: duration)
            DispatchQueue.main.async { completion(track) }
        }
    }

    /// 去掉文件名前面的序号："01 静听·夏夜" → "静听·夏夜"
    static func cleanName(_ name: String) -> String {
        let pattern = "^\\s*\\d{1,3}\\s*[.\\-_、]?\\s+"
        if let range = name.range(of: pattern, options: .regularExpression) {
            let trimmed = String(name[range.upperBound...])
            return trimmed.isEmpty ? name : trimmed
        }
        return name
    }
}
