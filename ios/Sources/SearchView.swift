import SwiftUI

// MARK: - 搜索

struct SearchView: View {
    @ObservedObject var engine: PlayerEngine
    @ObservedObject var library: LibraryStore

    @StateObject private var api = OnlineSource()
    @State private var keyword = ""
    @State private var loadingId: String?
    @State private var showSettings = false

    var body: some View {
        NavigationView {
            ZStack {
                Theme.background.ignoresSafeArea()

                VStack(spacing: 0) {
                    searchBar
                    sourceChips

                    if api.isSearching {
                        Spacer()
                        ProgressView().tint(Theme.cyan)
                        Text("搜索中…")
                            .font(.system(size: 12)).foregroundColor(Theme.textMid)
                            .padding(.top, 8)
                        Spacer()
                    } else if api.results.isEmpty {
                        Spacer()
                        VStack(spacing: 10) {
                            Image(systemName: "magnifyingglass")
                                .font(.system(size: 40)).foregroundColor(Theme.textLow)
                            Text(api.lastError ?? "搜点什么")
                                .font(.system(size: 13))
                                .foregroundColor(Theme.textMid)
                            Text("搜不到就换音源再试")
                                .font(.system(size: 11)).foregroundColor(Theme.textLow)
                        }
                        Spacer()
                    } else {
                        resultList
                    }
                }
            }
            .navigationTitle("搜索")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button { showSettings = true } label: {
                        Image(systemName: "slider.horizontal.3").foregroundColor(Theme.textMid)
                    }
                }
            }
            .sheet(isPresented: $showSettings) { settingsView }
        }
    }

    private var searchBar: some View {
        HStack(spacing: 10) {
            HStack(spacing: 8) {
                Image(systemName: "magnifyingglass")
                    .font(.system(size: 14)).foregroundColor(Theme.textLow)

                TextField("", text: $keyword)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .submitLabel(.search)
                    .font(.system(size: 14))
                    .foregroundColor(Theme.textHi)
                    .placeholder(when: keyword.isEmpty) {
                        Text("搜歌名 / 歌手").foregroundColor(Theme.textLow)
                    }
                    .onSubmit { runSearch() }

                if !keyword.isEmpty {
                    Button { keyword = "" } label: {
                        Image(systemName: "xmark.circle.fill")
                            .font(.system(size: 14)).foregroundColor(Theme.textLow)
                    }
                }
            }
            .padding(.horizontal, 12).padding(.vertical, 9)
            .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(Theme.bg2))

            Button { runSearch() } label: {
                Text("搜索")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(Theme.cyan)
            }
        }
        .padding(.horizontal, 16).padding(.top, 8).padding(.bottom, 10)
    }

    private var sourceChips: some View {
        HStack(spacing: 8) {
            ForEach(OnlineSource.sources, id: \.id) { item in
                Button {
                    api.selectedSource = item.id
                    if !keyword.isEmpty { runSearch() }
                } label: {
                    Text(item.name)
                        .font(.system(size: 12.5, weight: api.selectedSource == item.id ? .semibold : .regular))
                        .foregroundColor(api.selectedSource == item.id
                                         ? Color(red: 0.02, green: 0.08, blue: 0.1)
                                         : Theme.textMid)
                        .padding(.horizontal, 14).padding(.vertical, 7)
                        .background(Capsule().fill(api.selectedSource == item.id ? Theme.cyan : Theme.bg2))
                }
            }
            Spacer()
        }
        .padding(.horizontal, 16)
        .padding(.bottom, 10)
    }

    private var resultList: some View {
        ScrollView {
            LazyVStack(spacing: 2) {
                ForEach(api.results) { song in
                    HStack(spacing: 10) {
                        Button { play(song) } label: {
                            HStack(spacing: 12) {
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

                        Button { library.toggleFavorite(song) } label: {
                            Image(systemName: library.isFavorite(song) ? "heart.fill" : "heart")
                                .font(.system(size: 15))
                                .foregroundColor(library.isFavorite(song) ? Theme.rose : Theme.textLow)
                        }
                    }
                    .padding(.horizontal, 16).padding(.vertical, 8)
                    .background(RoundedRectangle(cornerRadius: 10, style: .continuous).fill(Theme.bg2.opacity(0.32)))
                    .padding(.horizontal, 12)
                }
            }
            .padding(.vertical, 6)
        }
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
        .frame(width: 44, height: 44)
        .clipShape(RoundedRectangle(cornerRadius: 7, style: .continuous))
    }

    private var settingsView: some View {
        NavigationView {
            ZStack {
                Theme.background.ignoresSafeArea()
                VStack(alignment: .leading, spacing: 16) {
                    Text("API 地址")
                        .font(.system(size: 13, weight: .semibold)).foregroundColor(Theme.textHi)
                    TextField("https://...", text: $api.baseURL)
                        .font(.system(size: 12, design: .monospaced))
                        .foregroundColor(Theme.textHi)
                        .padding(10)
                        .background(RoundedRectangle(cornerRadius: 8).fill(Theme.bg2))
                        .autocapitalization(.none)
                        .disableAutocorrection(true)

                    Text("默认接 GD 音乐台的聚合接口。实测只有网易云和 JOOX 返回数据，tencent / kugou / kuwo / migu 等一律 400。接口挂了可以换成自建服务，参数保持 types=search / url / pic / playlist。")
                        .font(.system(size: 11.5))
                        .foregroundColor(Theme.textLow)
                        .fixedSize(horizontal: false, vertical: true)
                    Spacer()
                }
                .padding(20)
            }
            .navigationTitle("音源设置")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("好") { showSettings = false }.foregroundColor(Theme.cyan)
                }
            }
        }
        .preferredColorScheme(.dark)
    }

    private func runSearch() {
        Task { await api.search(keyword) }
    }

    private func play(_ song: OnlineSong) {
        guard loadingId == nil else { return }
        loadingId = song.id
        Task {
            let url = await api.resolveURL(for: song)
            loadingId = nil
            guard let url else {
                api.lastError = "这首拿不到播放地址"
                return
            }
            library.record(song)
            engine.playOnline(api.toTrack(song, url: url))
        }
    }
}
