import SwiftUI

struct OnlineView: View {
    @ObservedObject var engine: PlayerEngine
    @StateObject private var api = OnlineSource()

    @State private var keyword = ""
    @State private var loadingId: String?
    @State private var showSettings = false
    @Environment(\.dismiss) private var dismiss

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
                            .font(.system(size: 12))
                            .foregroundColor(Theme.textMid)
                            .padding(.top, 8)
                        Spacer()
                    } else if let err = api.lastError, api.results.isEmpty {
                        Spacer()
                        Text(err)
                            .font(.system(size: 13))
                            .foregroundColor(Theme.textLow)
                            .multilineTextAlignment(.center)
                            .padding(.horizontal, 40)
                        Spacer()
                    } else if api.results.isEmpty {
                        Spacer()
                        VStack(spacing: 10) {
                            Image(systemName: "magnifyingglass")
                                .font(.system(size: 40))
                                .foregroundColor(Theme.textLow)
                            Text("搜点什么")
                                .font(.system(size: 14))
                                .foregroundColor(Theme.textMid)
                            Text("选好音源，输入歌名或歌手")
                                .font(.system(size: 12))
                                .foregroundColor(Theme.textLow)
                        }
                        Spacer()
                    } else {
                        resultList
                    }
                }
            }
            .navigationTitle("在线音乐")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    Button {
                        showSettings = true
                    } label: {
                        Image(systemName: "slider.horizontal.3")
                            .foregroundColor(Theme.textMid)
                    }
                }
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("完成") { dismiss() }
                        .foregroundColor(Theme.cyan)
                }
            }
            .sheet(isPresented: $showSettings) { settingsView }
        }
        .preferredColorScheme(.dark)
    }

    // MARK: - 搜索栏

    private var searchBar: some View {
        HStack(spacing: 10) {
            HStack(spacing: 8) {
                Image(systemName: "magnifyingglass")
                    .font(.system(size: 14))
                    .foregroundColor(Theme.textLow)

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
                            .font(.system(size: 14))
                            .foregroundColor(Theme.textLow)
                    }
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 9)
            .background(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(Theme.bg2)
            )

            Button { runSearch() } label: {
                Text("搜索")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(Theme.cyan)
            }
        }
        .padding(.horizontal, 16)
        .padding(.top, 8)
        .padding(.bottom, 10)
    }

    // MARK: - 音源

    private var sourceChips: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                ForEach(OnlineSource.sources, id: \.id) { item in
                    Button {
                        api.selectedSource = item.id
                        if !keyword.isEmpty { runSearch() }
                    } label: {
                        Text(item.name)
                            .font(.system(size: 12.5, weight: api.selectedSource == item.id ? .semibold : .regular))
                            .foregroundColor(api.selectedSource == item.id ? Color(red: 0.02, green: 0.08, blue: 0.1) : Theme.textMid)
                            .padding(.horizontal, 14)
                            .padding(.vertical, 7)
                            .background(
                                Capsule().fill(api.selectedSource == item.id ? Theme.cyan : Theme.bg2)
                            )
                    }
                }
            }
            .padding(.horizontal, 16)
        }
        .padding(.bottom, 10)
    }

    // MARK: - 结果

    private var resultList: some View {
        ScrollView {
            LazyVStack(spacing: 2) {
                ForEach(api.results) { song in
                    Button {
                        play(song)
                    } label: {
                        HStack(spacing: 12) {
                            artwork(song)

                            VStack(alignment: .leading, spacing: 3) {
                                Text(song.name)
                                    .font(.system(size: 14))
                                    .foregroundColor(Theme.textHi)
                                    .lineLimit(1)
                                HStack(spacing: 6) {
                                    Text(song.displayArtist)
                                        .font(.system(size: 11))
                                        .foregroundColor(Theme.textMid)
                                        .lineLimit(1)
                                    if !song.album.isEmpty {
                                        Text("·")
                                            .font(.system(size: 11))
                                            .foregroundColor(Theme.textLow)
                                        Text(song.album)
                                            .font(.system(size: 11))
                                            .foregroundColor(Theme.textLow)
                                            .lineLimit(1)
                                    }
                                }
                            }

                            Spacer()

                            if loadingId == song.id {
                                ProgressView().scaleEffect(0.7).tint(Theme.cyan)
                            } else {
                                Image(systemName: "play.circle")
                                    .font(.system(size: 20))
                                    .foregroundColor(Theme.cyan.opacity(0.75))
                            }
                        }
                        .padding(.horizontal, 16)
                        .padding(.vertical, 9)
                        .background(
                            RoundedRectangle(cornerRadius: 10, style: .continuous)
                                .fill(Theme.bg2.opacity(0.35))
                        )
                        .padding(.horizontal, 12)
                    }
                }
            }
            .padding(.vertical, 6)
        }
    }

    private func artwork(_ song: OnlineSong) -> some View {
        Group {
            if let u = api.artworkURL(for: song) {
                AsyncImage(url: u) { phase in
                    if let img = phase.image {
                        img.resizable().aspectRatio(contentMode: .fill)
                    } else {
                        RoundedRectangle(cornerRadius: 8).fill(Theme.bg3)
                    }
                }
            } else {
                RoundedRectangle(cornerRadius: 8).fill(Theme.bg3)
            }
        }
        .frame(width: 46, height: 46)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    // MARK: - 设置

    private var settingsView: some View {
        NavigationView {
            ZStack {
                Theme.background.ignoresSafeArea()
                VStack(alignment: .leading, spacing: 18) {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("API 地址")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundColor(Theme.textHi)
                        TextField("https://...", text: $api.baseURL)
                            .font(.system(size: 12, design: .monospaced))
                            .foregroundColor(Theme.textHi)
                            .padding(10)
                            .background(RoundedRectangle(cornerRadius: 8).fill(Theme.bg2))
                            .autocapitalization(.none)
                            .disableAutocorrection(true)
                    }

                    Text("默认接的是 GD 音乐台的聚合接口。换源或者接口挂了的话，可以在这里改成自建服务，只要它兼容同样的参数（types/search、types/url、types/pic）。")
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
                    Button("好") { showSettings = false }
                        .foregroundColor(Theme.cyan)
                }
            }
        }
        .preferredColorScheme(.dark)
    }

    // MARK: - 动作

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
                api.lastError = "这首拿不到播放地址（可能是付费或版权限制）"
                return
            }

            let track = api.toTrack(song, url: url)
            engine.playOnline(track)
            dismiss()
        }
    }
}

extension View {
    func placeholder<Content: View>(
        when shouldShow: Bool,
        alignment: Alignment = .leading,
        @ViewBuilder placeholder: () -> Content
    ) -> some View {
        ZStack(alignment: alignment) {
            placeholder().opacity(shouldShow ? 1 : 0)
            self
        }
    }
}
