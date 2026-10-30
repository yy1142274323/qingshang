import SwiftUI
import UniformTypeIdentifiers

struct ContentView: View {
    @StateObject private var engine = PlayerEngine()

    @State private var showPlaylist = false
    @State private var showOnline = false
    @State private var showImporter = false
    @State private var discAngle: Double = 0
    @State private var phase: Double = 0
    @State private var bars: [CGFloat] = Array(repeating: 0.12, count: 40)
    @State private var seeds: [Double] = (0..<40).map { _ in Double.random(in: 0...6.28) }

    private let ticker = Timer.publish(every: 0.055, on: .main, in: .common).autoconnect()

    private var colors: (Color, Color) {
        discColors(for: engine.current.map { "\($0.title)|\($0.artist)|\($0.url.lastPathComponent)" } ?? "qingshang")
    }

    var body: some View {
        ZStack {
            Theme.background.ignoresSafeArea()

            VStack(spacing: 0) {
                header

                Spacer(minLength: 6)

                disc

                Spacer(minLength: 10)

                titleBlock
                    .padding(.top, 18)

                spectrum
                    .padding(.top, 18)

                Spacer(minLength: 10)

                progressBlock

                controls
                    .padding(.top, 8)
            }
            .padding(.horizontal, 24)
            .padding(.top, 8)
            .padding(.bottom, 10)
        }
        .preferredColorScheme(.dark)
        .sheet(isPresented: $showPlaylist) {
            PlaylistView(engine: engine, showImporter: $showImporter)
        }
        .sheet(isPresented: $showOnline) {
            OnlineView(engine: engine)
        }
        .fileImporter(
            isPresented: $showImporter,
            allowedContentTypes: [.audio, .mp3, .wav, .mpeg4Audio, .aiff],
            allowsMultipleSelection: true
        ) { result in
            if case .success(let urls) = result {
                importFiles(urls)
            }
        }
        .onAppear { engine.reload() }
        .onReceive(ticker) { _ in animate() }
    }

    // MARK: - 头部

    private var header: some View {
        HStack(spacing: 10) {
            ZStack {
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(LinearGradient(colors: [Theme.cyan, Theme.cyanDeep],
                                         startPoint: .topLeading, endPoint: .bottomTrailing))
                    .frame(width: 26, height: 26)
                Image(systemName: "music.note")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundColor(Color(red: 0.03, green: 0.09, blue: 0.11))
            }

            HStack(alignment: .firstTextBaseline, spacing: 5) {
                Text("清商")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(Theme.textHi)
                Text("音乐播放器")
                    .font(.system(size: 12.5))
                    .foregroundColor(Theme.textMid.opacity(0.92))
            }

            Spacer()

            Button {
                showOnline = true
            } label: {
                Image(systemName: "magnifyingglass")
                    .font(.system(size: 17, weight: .medium))
                    .foregroundColor(Theme.cyan)
            }

            Button {
                engine.reload()
            } label: {
                Image(systemName: "arrow.clockwise")
                    .font(.system(size: 15, weight: .medium))
                    .foregroundColor(Theme.textMid)
            }

            Button {
                showPlaylist = true
            } label: {
                Image(systemName: "list.bullet")
                    .font(.system(size: 17, weight: .regular))
                    .foregroundColor(Theme.textMid)
            }
        }
        .padding(.top, 4)
    }

    // MARK: - 唱片

    private var disc: some View {
        let size = min(UIScreen.main.bounds.width * 0.62, 260.0)

        return ZStack {
            Circle()
                .fill(
                    RadialGradient(
                        colors: [Color(red: 0.106, green: 0.129, blue: 0.165),
                                 Color(red: 0.031, green: 0.043, blue: 0.055)],
                        center: .center, startRadius: 4, endRadius: size / 2
                    )
                )
                .overlay(
                    Circle()
                        .stroke(Theme.cyan.opacity(0.10), lineWidth: 1)
                )
                .shadow(color: colors.0.opacity(0.18), radius: 26)

            // 纹路
            ForEach(0..<9, id: \.self) { i in
                Circle()
                    .stroke(Color.white.opacity(0.022), lineWidth: 0.7)
                    .padding(CGFloat(12 + i * 9))
            }

            // 中心标签
            Circle()
                .fill(LinearGradient(colors: [colors.0, colors.1],
                                     startPoint: .topLeading, endPoint: .bottomTrailing))
                .frame(width: size * 0.46, height: size * 0.46)

            Image(systemName: "music.note")
                .font(.system(size: size * 0.17, weight: .regular))
                .foregroundColor(.white.opacity(0.85))
                .rotationEffect(.degrees(discAngle))

            Circle()
                .fill(Color(red: 0.043, green: 0.063, blue: 0.082))
                .frame(width: size * 0.035, height: size * 0.035)
        }
        .frame(width: size, height: size)
    }

    // MARK: - 曲目信息

    private var titleBlock: some View {
        VStack(spacing: 6) {
            Text(engine.current?.title ?? "还没有选歌")
                .font(.system(size: 22, weight: .bold))
                .foregroundColor(Theme.textHi)
                .lineLimit(1)
                .minimumScaleFactor(0.7)

            Text(engine.current?.displayArtist ?? "点右上角 + 添加音乐")
                .font(.system(size: 13))
                .foregroundColor(engine.current == nil ? Theme.textLow : Theme.cyan.opacity(0.92))
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity)
    }

    // MARK: - 频谱

    private var spectrum: some View {
        HStack(alignment: .bottom, spacing: 3) {
            ForEach(0..<bars.count, id: \.self) { i in
                RoundedRectangle(cornerRadius: 1.6, style: .continuous)
                    .fill(
                        LinearGradient(colors: [Theme.cyan, Theme.amber],
                                       startPoint: .bottom, endPoint: .top)
                    )
                    .frame(height: max(2, bars[i] * 54))
            }
        }
        .frame(height: 56, alignment: .bottom)
        .frame(maxWidth: .infinity)
        .clipped()
        .opacity(engine.isPlaying ? 1 : 0.35)
    }

    // MARK: - 进度

    private var progressBlock: some View {
        VStack(spacing: 2) {
            Slider(
                value: Binding(
                    get: { engine.duration > 0 ? engine.position / engine.duration : 0 },
                    set: { engine.seek(to: $0 * engine.duration) }
                ),
                in: 0...1
            )
            .tint(Theme.cyan)

            HStack {
                Text(formatTime(engine.position))
                Spacer()
                Text(formatTime(engine.duration))
            }
            .font(.system(size: 11, design: .monospaced))
            .foregroundColor(Theme.textMid)
        }
    }

    // MARK: - 控制

    private var controls: some View {
        HStack(spacing: 26) {
            Button { engine.repeatOne.toggle() } label: {
                Image(systemName: engine.repeatOne ? "repeat.1" : "repeat")
                    .font(.system(size: 17, weight: .medium))
                    .foregroundColor(engine.repeatOne ? Theme.cyan : Theme.textMid)
            }

            Button { engine.previous() } label: {
                Image(systemName: "backward.fill")
                    .font(.system(size: 22))
                    .foregroundColor(Theme.textHi)
            }

            Button { engine.toggle() } label: {
                ZStack {
                    Circle()
                        .fill(Theme.cyan)
                        .frame(width: 66, height: 66)
                        .shadow(color: Theme.cyan.opacity(0.35), radius: 16)
                    Image(systemName: engine.isPlaying ? "pause.fill" : "play.fill")
                        .font(.system(size: 26, weight: .bold))
                        .foregroundColor(Color(red: 0.02, green: 0.07, blue: 0.09))
                        .offset(x: engine.isPlaying ? 0 : 2)
                }
            }

            Button { engine.next() } label: {
                Image(systemName: "forward.fill")
                    .font(.system(size: 22))
                    .foregroundColor(Theme.textHi)
            }

            Button { showPlaylist = true } label: {
                Image(systemName: "music.note.list")
                    .font(.system(size: 16, weight: .medium))
                    .foregroundColor(Theme.textMid)
            }
        }
        .padding(.bottom, 6)
    }

    // MARK: - 逻辑

    private func animate() {
        phase += 0.055

        for i in 0..<bars.count {
            if engine.isPlaying {
                let x = Double(i) / Double(bars.count)
                let wave = sin(phase * 3.1 + seeds[i]) * 0.34
                    + sin(phase * 5.7 - Double(i) * 0.31) * 0.24
                    + sin(phase * 1.6 + Double(i) * 1.13) * 0.22
                    + sin(phase * 9.3 + seeds[i] * 2) * 0.10
                let tilt = pow(1.0 - x * 0.8, 0.62)
                let pulse = 0.5 + 0.5 * sin(phase * 2.2)
                let target = max(0.05, min(1.0, (wave * 0.5 + 0.5) * tilt * (0.62 + 0.38 * pulse)))
                bars[i] += (CGFloat(target) - bars[i]) * 0.42
            } else {
                bars[i] += (0.06 - bars[i]) * 0.10
            }
        }

        if engine.isPlaying {
            discAngle = (discAngle + 0.55).truncatingRemainder(dividingBy: 360)
        }
    }

    private func importFiles(_ urls: [URL]) {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        for url in urls {
            let scoped = url.startAccessingSecurityScopedResource()
            defer { if scoped { url.stopAccessingSecurityScopedResource() } }

            let dest = docs.appendingPathComponent(url.lastPathComponent)
            if FileManager.default.fileExists(atPath: dest.path) { continue }
            try? FileManager.default.copyItem(at: url, to: dest)
        }
        engine.reload()
    }
}

// MARK: - 播放列表

struct PlaylistView: View {
    @ObservedObject var engine: PlayerEngine
    @Binding var showImporter: Bool
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationView {
            ZStack {
                Theme.background.ignoresSafeArea()

                if engine.tracks.isEmpty {
                    VStack(spacing: 12) {
                        Image(systemName: "music.note.list")
                            .font(.system(size: 42))
                            .foregroundColor(Theme.textLow)
                        Text("还没有音乐")
                            .font(.system(size: 15))
                            .foregroundColor(Theme.textMid)
                        Text("点右上角 + 从「文件」里选歌，\n或者用 Filza 把歌丢进 Documents 目录")
                            .font(.system(size: 12))
                            .multilineTextAlignment(.center)
                            .foregroundColor(Theme.textLow)
                            .padding(.horizontal, 40)
                    }
                } else {
                    ScrollView {
                        LazyVStack(spacing: 2) {
                            ForEach(Array(engine.tracks.enumerated()), id: \.element.id) { index, track in
                                Button {
                                    engine.play(at: index)
                                    dismiss()
                                } label: {
                                    HStack(spacing: 12) {
                                        Text(String(format: "%02d", index + 1))
                                            .font(.system(size: 11, design: .monospaced))
                                            .foregroundColor(index == engine.currentIndex ? Theme.cyan : Theme.textLow)
                                            .frame(width: 24, alignment: .trailing)

                                        VStack(alignment: .leading, spacing: 3) {
                                            Text(track.title)
                                                .font(.system(size: 14, weight: index == engine.currentIndex ? .semibold : .regular))
                                                .foregroundColor(index == engine.currentIndex ? Theme.cyan : Theme.textHi)
                                                .lineLimit(1)
                                            Text(track.displayArtist)
                                                .font(.system(size: 11))
                                                .foregroundColor(Theme.textMid)
                                                .lineLimit(1)
                                        }

                                        Spacer()

                                        if !track.sourceTag.isEmpty {
                                            Text(track.sourceTag)
                                                .font(.system(size: 9))
                                                .foregroundColor(Theme.cyan)
                                                .padding(.horizontal, 5)
                                                .padding(.vertical, 1.5)
                                                .background(Capsule().fill(Theme.cyan.opacity(0.15)))
                                        }

                                        if track.duration > 0 {
                                            Text(formatTime(track.duration))
                                                .font(.system(size: 11, design: .monospaced))
                                                .foregroundColor(Theme.textLow)
                                        }
                                    }
                                    .padding(.horizontal, 16)
                                    .padding(.vertical, 11)
                                    .background(
                                        RoundedRectangle(cornerRadius: 10, style: .continuous)
                                            .fill(index == engine.currentIndex
                                                  ? Theme.cyan.opacity(0.12)
                                                  : Theme.bg2.opacity(0.42))
                                    )
                                    .padding(.horizontal, 12)
                                }
                            }
                        }
                        .padding(.vertical, 8)
                    }
                }
            }
            .navigationTitle("播放列表（\(engine.tracks.count) 首）")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    Button {
                        dismiss()
                        DispatchQueue.main.asyncAfter(deadline: .now() + 0.35) { showImporter = true }
                    } label: {
                        Image(systemName: "plus")
                            .foregroundColor(Theme.cyan)
                    }
                }
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("完成") { dismiss() }
                        .foregroundColor(Theme.cyan)
                }
            }
        }
        .preferredColorScheme(.dark)
    }
}
