import SwiftUI

// MARK: - 收藏 / 历史

struct LibraryView: View {
    @ObservedObject var engine: PlayerEngine
    @ObservedObject var library: LibraryStore

    @StateObject private var api = OnlineSource()
    @State private var segment = 0
    @State private var loadingId: String?

    private var list: [SavedSong] {
        segment == 0 ? library.favorites : library.history
    }

    var body: some View {
        NavigationView {
            ZStack {
                Theme.background.ignoresSafeArea()

                VStack(spacing: 0) {
                    Picker("", selection: $segment) {
                        Text("收藏 \(library.favorites.count)").tag(0)
                        Text("历史 \(library.history.count)").tag(1)
                    }
                    .pickerStyle(.segmented)
                    .padding(.horizontal, 16)
                    .padding(.top, 6)
                    .padding(.bottom, 10)

                    if list.isEmpty {
                        Spacer()
                        VStack(spacing: 10) {
                            Image(systemName: segment == 0 ? "heart" : "clock.arrow.circlepath")
                                .font(.system(size: 40)).foregroundColor(Theme.textLow)
                            Text(segment == 0 ? "还没有收藏" : "还没有播放记录")
                                .font(.system(size: 13)).foregroundColor(Theme.textMid)
                            Text(segment == 0 ? "在歌单或搜索结果里点心形" : "播过的歌会自动记在这里")
                                .font(.system(size: 11)).foregroundColor(Theme.textLow)
                        }
                        Spacer()
                    } else {
                        ScrollView {
                            LazyVStack(spacing: 2) {
                                ForEach(list) { item in
                                    row(item)
                                }
                            }
                            .padding(.vertical, 6)
                        }
                    }
                }
            }
            .navigationTitle("我的")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    if segment == 1 && !library.history.isEmpty {
                        Button("清空") { library.clearHistory() }
                            .foregroundColor(Theme.textLow)
                            .font(.system(size: 12))
                    }
                }
            }
        }
    }

    private func row(_ item: SavedSong) -> some View {
        HStack(spacing: 10) {
            Button { play(item) } label: {
                HStack(spacing: 12) {
                    cover(item)

                    VStack(alignment: .leading, spacing: 3) {
                        Text(item.name)
                            .font(.system(size: 14))
                            .foregroundColor(Theme.textHi)
                            .lineLimit(1)
                        Text(item.displayArtist)
                            .font(.system(size: 11))
                            .foregroundColor(Theme.textMid)
                            .lineLimit(1)
                    }

                    Spacer()

                    if loadingId == item.id {
                        ProgressView().scaleEffect(0.7).tint(Theme.cyan)
                    } else if segment == 1 {
                        Text(shortDate(item.savedAt))
                            .font(.system(size: 10))
                            .foregroundColor(Theme.textLow)
                    }
                }
                .contentShape(Rectangle())
            }

            if segment == 0 {
                Button { library.removeFavorite(item) } label: {
                    Image(systemName: "heart.fill")
                        .font(.system(size: 15))
                        .foregroundColor(Theme.rose)
                }
            }
        }
        .padding(.horizontal, 16).padding(.vertical, 8)
        .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(Theme.bg2.opacity(0.32)))
        .padding(.horizontal, 12)
    }

    private func cover(_ item: SavedSong) -> some View {
        Group {
            if let u = api.artworkURL(for: item.asOnline) {
                AsyncImage(url: u) { phase in
                    if let img = phase.image {
                        img.resizable().aspectRatio(contentMode: .fill)
                    } else {
                        RoundedRectangle(cornerRadius: 7).fill(Theme.bg3)
                    }
                }
            } else {
                RoundedRectangle(cornerRadius: 7).fill(Theme.bg3)
            }
        }
        .frame(width: 44, height: 44)
        .clipShape(RoundedRectangle(cornerRadius: 7, style: .continuous))
    }

    private func play(_ item: SavedSong) {
        guard loadingId == nil else { return }
        loadingId = item.id
        let song = item.asOnline

        Task {
            let url = await api.resolveURL(for: song)
            loadingId = nil
            guard let url else { return }
            library.record(song)
            engine.playOnline(api.toTrack(song, url: url))
        }
    }

    private func shortDate(_ d: Date) -> String {
        let f = DateFormatter()
        f.dateFormat = Calendar.current.isDateInToday(d) ? "HH:mm" : "MM-dd"
        return f.string(from: d)
    }
}
