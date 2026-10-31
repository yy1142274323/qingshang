$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
$base = "https://music-api.gdstudio.xyz/api.php"

# 常见网易云歌单/榜单 ID
$ids = [ordered]@{
    '3778678'   = '热歌榜'
    '3779629'   = '新歌榜'
    '19723756'  = '飙升榜'
    '2884035'   = '原创榜'
    '9913195903'= '说唱榜'
    '1978921795'= '电音榜'
    '71384707'  = '古风榜'
    '5059642708'= '欧美热歌榜'
    '5059644681'= '日语榜'
    '745956260' = '韩语榜'
    '2809513713'= '民谣'
    '2829816518'= '轻音乐'
    '19654323'  = '华语'
    '6723173524'= '经典老歌'
    '3770017'   = '?'
    '3788097'   = '?'
}

foreach ($id in $ids.Keys) {
    $u = "${base}?types=playlist&source=netease&id=${id}"
    try {
        $wc = New-Object System.Net.WebClient
        $wc.Encoding = [System.Text.Encoding]::UTF8
        $json = $wc.DownloadString($u)
        if ($json.Length -lt 50) { "{0,-12} 空响应" -f $id; continue }
        $o = $json | ConvertFrom-Json
        if ($o.code -ne 200 -or $null -eq $o.playlist) {
            "{0,-12} code={1} 无效" -f $id, $o.code
            continue
        }
        $tracks = @($o.playlist.tracks).Count
        "{0,-12} {1,-14} 曲目={2,-5} 期望={3}" -f $id, $o.playlist.name, $tracks, $ids[$id]
    } catch {
        "{0,-12} ERR {1}" -f $id, $_.Exception.Message
    }
}
