import SwiftUI
import UIKit

struct MainTabView: View {
    @StateObject private var engine = PlayerEngine()
    @StateObject private var library = LibraryStore()

    init() {
        let appearance = UITabBarAppearance()
        appearance.configureWithOpaqueBackground()
        appearance.backgroundColor = UIColor(red: 0.075, green: 0.098, blue: 0.129, alpha: 1)

        let item = appearance.stackedLayoutAppearance
        item.normal.iconColor = UIColor(red: 0.357, green: 0.420, blue: 0.494, alpha: 1)
        item.normal.titleTextAttributes = [.foregroundColor: UIColor(red: 0.357, green: 0.420, blue: 0.494, alpha: 1)]
        item.selected.iconColor = UIColor(red: 0.227, green: 0.847, blue: 0.914, alpha: 1)
        item.selected.titleTextAttributes = [.foregroundColor: UIColor(red: 0.227, green: 0.847, blue: 0.914, alpha: 1)]

        UITabBar.appearance().standardAppearance = appearance
        UITabBar.appearance().scrollEdgeAppearance = appearance
    }

    var body: some View {
        TabView {
            NowPlayingView(engine: engine, library: library)
                .tabItem { Label("正在播放", systemImage: "play.circle.fill") }

            DiscoverView(engine: engine, library: library)
                .tabItem { Label("发现", systemImage: "square.grid.2x2") }

            SearchView(engine: engine, library: library)
                .tabItem { Label("搜索", systemImage: "magnifyingglass") }

            LibraryView(engine: engine, library: library)
                .tabItem { Label("我的", systemImage: "heart") }
        }
        .tint(Theme.cyan)
        .preferredColorScheme(.dark)
    }
}

// 输入框占位符（iOS 15 的 TextField 没有这个）
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
