import SwiftUI

// MARK: - 发现：榜单入口

struct DiscoverView: View {
    @ObservedObject var engine: PlayerEngine
    @ObservedObject var library: LibraryStore

    @StateObject private var api = OnlineSource()
    @State private var selected: RankItem?

    private let columns = [
        GridItem(.flexible(), spacing: 12),
        GridItem(.flexible(), spacing: 12)
    ]

    var body: some View {
        NavigationView {
            ZStack {
                Theme.background.ignoresSafeArea()

                ScrollView {
                    VStack(alignment: .leading, spacing: 14) {
                        Text("榜单")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundColor(Theme.textMid)
                            .padding(.horizontal, 18)

                        LazyVGrid(columns: columns, spacing: 12) {
                            ForEach(OnlineSource.ranks) { rank in
                                Button { selected = rank } label: { rankCard(rank) }
                            }
                        }
                        .padding(.horizontal, 16)

                        Text("接口来自第三方聚合服务，只有网易云和 JOOX 两个源是活的，其余平台一律返回 400。")
                            .font(.system(size: 11))
                            .foregroundColor(Theme.textLow)
                            .padding(.horizontal, 18)
                            .padding(.top, 6)
                            .padding(.bottom, 20)
                    }
                    .padding(.top, 6)
                }
            }
            .navigationTitle("发现")
            .navigationBarTitleDisplayMode(.inline)
            .sheet(item: $selected) { rank in
                PlaylistDetailView(rank: rank, engine: engine, library: library)
            }
        }
    }

    private func rankCard(_ rank: RankItem) -> some View {
        let tint = tintColor(rank.tint)

        return ZStack(alignment: .bottomLeading) {
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(
                    LinearGradient(
                        colors: [tint.opacity(0.85), tint.opacity(0.28)],
                        startPoint: .topLeading, endPoint: .bottomTrailing
                    )
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 14, style: .continuous)
                        .stroke(Color.white.opacity(0.06), lineWidth: 1)
                )

            VStack(alignment: .leading, spacing: 3) {
                Text(rank.name)
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(.white)
                Text(rank.note)
                    .font(.system(size: 10.5))
                    .foregroundColor(.white.opacity(0.62))
            }
            .padding(12)

            Image(systemName: "chart.line.uptrend.xyaxis")
                .font(.system(size: 34))
                .foregroundColor(.white.opacity(0.12))
                .offset(x: -12, y: -10)
                .frame(maxWidth: .infinity, alignment: .trailing)
        }
        .frame(height: 84)
    }

    private func tintColor(_ i: Int) -> Color {
        let palette: [Color] = [
            Theme.cyanDeep, Color(red: 0.36, green: 0.30, blue: 0.70),
            Color(red: 0.62, green: 0.26, blue: 0.42), Color(red: 0.18, green: 0.42, blue: 0.52),
            Color(red: 0.30, green: 0.46, blue: 0.30), Color(red: 0.45, green: 0.34, blue: 0.24)
        ]
        return palette[abs(i) % palette.count]
    }
}

// MARK: - 歌单详情

struct PlaylistDetailView: View {
    let rank: RankItem
    @ObservedObject var engine: PlayerEngine
    @ObservedObject var library: LibraryStore

    @StateObject private var api = OnlineSource()
    @State private var songs: [OnlineSong] = []
    @State private var loading = true
    @State private var loadingId: String?
    @State private var toast: String?

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationView {
            ZStack {
                Theme.background.ignoresSafeArea()

                if loading {
                    ProgressView().tint(Theme.cyan)
                } else if songs.isEmpty {
                    VStack(spacing: 10) {
                        Image(systemName: "music.note.list")
                            .font(.system(size: 40)).foregroundColor(Theme.textLow)
                        Text("这张榜单拉不到曲目").foregroundColor(Theme.textMid)
                    }
                } else {
                    ScrollView {
                        LazyVStack(spacing: 2) {
                            ForEach(Array(songs.enumerated()), id: \.element.id) { idx, song in
                                row(idx: idx, song: song)
                            }
                        }
                        .padding(.vertical, 6)
                    }
                }
            }
            .navigationTitle(rank.name)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    Button {
                        playAll()
                    } label: {
                        Label("播放全部", systemImage: "play.fill")
                            .font(.system(size: 12))
                            .foregroundColor(Theme.cyan)
                    }
                    .disabled(songs.isEmpty)
                }
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("完成") { dismiss() }
                        .foregroundColor(Theme.cyan)
                }
            }
            .overlay(alignment: .bottom) {
                if let t = toast {
                    Text(t)
                        .font(.system(size: 12))
                        .foregroundColor(Theme.textHi)
                        .padding(.horizontal, 14).padding(.vertical, 8)
                        .background(Capsule().fill(Theme.bg3))
                        .padding(.bottom, 30)
                        .transition(.opacity)
                }
            }
        }
        .preferredColorScheme(.dark)
        .task { await load() }
    }

    private func row(idx: Int, song: OnlineSong) -> some View {
        HStack(spacing: 12) {
            Button {
                play(song)
            } label: {
                HStack(spacing: 12) {
                    Text(String(format: "%02d", idx + 1))
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(Theme.textLow)
                        .frame(width: 24, alignment: .trailing)

                    cover(song)

                    VStack(alignment: .leading, spacing: 3) {
                        Text(song.name)
                            .font(.system(size: 14))
                            .foregroundColor(Theme.textHi)
                            .lineLimit(1)
                        Text(song.displayArtist)
                            .font(.system(size: 11))
                            .foregroundColor(Theme.textMid)
                            .lineLimit(1)
                    }

                    Spacer()

                    if loadingId == song.id {
                        ProgressView().scaleEffect(0.7).tint(Theme.cyan)
                    }
                }
                .contentShape(Rectangle())
            }

            Button {
                library.toggleFavorite(song)
            } label: {
                Image(systemName: library.isFavorite(song) ? "heart.fill" : "heart")
                    .font(.system(size: 15))
                    .foregroundColor(library.isFavorite(song) ? Theme.rose : Theme.textLow)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 8)
        .background(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(Theme.bg2.opacity(0.32))
        )
        .padding(.horizontal, 12)
    }

    private func cover(_ song: OnlineSong) -> some View {
        Group {
            if let u = api.artworkURL(for: song) {
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
        .frame(width: 40, height: 40)
        .clipShape(RoundedRectangle(cornerRadius: 7, style: .continuous))
    }

    // MARK: - 动作

    private func load() async {
        loading = true
        songs = await api.loadPlaylist(id: rank.id)
        loading = false
    }

    private func play(_ song: OnlineSong) {
        guard loadingId == nil else { return }
        loadingId = song.id

        Task {
            let url = await api.resolveURL(for: song)
            loadingId = nil
            guard let url else {
                showToast("这首拿不到播放地址")
                return
            }
            library.record(song)
            engine.playOnline(api.toTrack(song, url: url))
        }
    }

    private func playAll() {
        guard let first = songs.first else { return }
        play(first)
    }

    private func showToast(_ text: String) {
        toast = text
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.6) {
            if toast == text { toast = nil }
        }
    }
}
