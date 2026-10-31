$ErrorActionPreference = 'Stop'
$root = "C:\Users\Administrator\Documents\deepseek-harness\default-workspace\qingshang"
$p = "$root\ios\Sources\ContentView.swift"
$c = [System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)

# ContentView -> NowPlayingView，engine 改由外部注入
$c = $c.Replace('struct ContentView: View {
    @StateObject private var engine = PlayerEngine()', 'struct NowPlayingView: View {
    @ObservedObject var engine: PlayerEngine
    @ObservedObject var library: LibraryStore')

# 去掉在线入口的状态
$c = $c.Replace('    @State private var showPlaylist = false
    @State private var showOnline = false', '    @State private var showPlaylist = false')

# 头部去掉放大镜（改成底部 tab）
$oldBtn = @'
            Button {
                showOnline = true
            } label: {
                Image(systemName: "magnifyingglass")
                    .font(.system(size: 17, weight: .medium))
                    .foregroundColor(Theme.cyan)
            }

'@
$c = $c.Replace($oldBtn, '')

# 去掉在线 sheet
$c = $c.Replace('
        .sheet(isPresented: $showOnline) {
            OnlineView(engine: engine)
        }', '')

# 空状态文案更新
$c = $c.Replace('Text(engine.current?.displayArtist ?? "点右上角 + 添加音乐")',
                'Text(engine.current?.displayArtist ?? "本地没歌？去「发现」搜一张榜单")')

[System.IO.File]::WriteAllText($p, $c, (New-Object System.Text.UTF8Encoding $false))

# 删掉旧的 OnlineView（已被 SearchView 取代）
Remove-Item "$root\ios\Sources\OnlineView.swift" -Force -ErrorAction SilentlyContinue

# App 入口指向 MainTabView
$app = "$root\ios\Sources\App.swift"
$ac = [System.IO.File]::ReadAllText($app, [System.Text.Encoding]::UTF8)
$ac = $ac.Replace('            ContentView()
                .preferredColorScheme(.dark)', '            MainTabView()')
[System.IO.File]::WriteAllText($app, $ac, (New-Object System.Text.UTF8Encoding $false))

Write-Host "--- 检查 ---"
Select-String -Path $p -Pattern 'struct NowPlayingView|ObservedObject var engine|showOnline' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
Write-Host "--- App.swift ---"
Get-Content $app -Raw -Encoding UTF8
Write-Host "--- 文件清单 ---"
Get-ChildItem "$root\ios\Sources" -Filter *.swift | Select-Object Name | Format-Table -AutoSize | Out-String
