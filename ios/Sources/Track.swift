import Foundation

/// 一首歌。既可以是本地文件，也可以是在线直链。
struct Track: Identifiable, Equatable {
    let id: UUID
    let url: URL
    var title: String
    var artist: String
    var duration: Double
    var isStream: Bool
    var artwork: URL?
    var sourceTag: String   // 本地为空；在线是音源名，比如「网易云」

    init(url: URL, title: String, artist: String, duration: Double = 0,
         isStream: Bool = false, artwork: URL? = nil, sourceTag: String = "") {
        self.id = UUID()
        self.url = url
        self.title = title
        self.artist = artist
        self.duration = duration
        self.isStream = isStream
        self.artwork = artwork
        self.sourceTag = sourceTag
    }

    var displayArtist: String {
        artist.isEmpty ? "未知艺术家" : artist
    }

    static func == (a: Track, b: Track) -> Bool {
        a.url == b.url && a.title == b.title
    }
}

enum AudioFiles {
    static let extensions: Set<String> = [
        "mp3", "m4a", "aac", "wav", "aiff", "aif", "flac", "caf", "mp4", "m4b"
    ]

    static func isAudio(_ url: URL) -> Bool {
        extensions.contains(url.pathExtension.lowercased())
    }

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

    static func makeTrack(from url: URL, completion: @escaping (Track) -> Void) {
        let fallbackTitle = cleanName(url.deletingPathExtension().lastPathComponent)
        let asset = AVURLAssetRef.make(url)

        asset.loadValuesAsynchronously(forKeys: ["commonMetadata", "duration"]) {
            var title = fallbackTitle
            var artist = ""
            var duration: Double = 0

            if asset.statusOfValue(forKey: "commonMetadata", error: nil) == .loaded {
                for item in asset.commonMetadata {
                    guard let key = item.commonKey else { continue }
                    if key == .commonKeyTitle, let v = item.value as? String, !v.isEmpty {
                        title = v
                    } else if key == .commonKeyArtist, let v = item.value as? String, !v.isEmpty {
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

    static func cleanName(_ name: String) -> String {
        let pattern = "^\\s*\\d{1,3}\\s*[.\\-_、]?\\s+"
        if let range = name.range(of: pattern, options: .regularExpression) {
            let trimmed = String(name[range.upperBound...])
            return trimmed.isEmpty ? name : trimmed
        }
        return name
    }
}

import AVFoundation

enum AVURLAssetRef {
    static func make(_ url: URL) -> AVURLAsset { AVURLAsset(url: url) }
}
