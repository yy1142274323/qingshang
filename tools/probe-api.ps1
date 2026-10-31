$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
$base = "https://music-api.gdstudio.xyz/api.php"
$H = @{ 'User-Agent' = 'QingShang/1.1' }
$kw = [uri]::EscapeDataString('周杰伦')

function Try-Url($u) {
    try {
        $r = Invoke-WebRequest -Uri $u -Headers $H -TimeoutSec 15
        return @{ ok = $true; status = $r.StatusCode; body = $r.Content }
    } catch {
        return @{ ok = $false; status = 0; body = $_.Exception.Message }
    }
}

Write-Host "===== 各 source 搜索（周杰伦）====="
foreach ($s in @('netease', 'tencent', 'kugou', 'kuwo', 'migu', 'joox', 'deezer', 'spotify', 'ytmusic', 'qobuz', 'tidal', 'ximalaya')) {
    $u = "${base}?types=search&source=${s}&name=${kw}&count=3&pages=1"
    $r = Try-Url $u
    if ($r.ok) {
        $body = $r.body
        $isArray = $body.TrimStart().StartsWith('[')
        $count = ([regex]::Matches($body, '"name"')).Count
        "{0,-10} HTTP {1}  len={2,-6} 数组={3,-6} 条目≈{4}" -f $s, $r.status, $body.Length, $isArray, $count
    } else {
        "{0,-10} 失败: {1}" -f $s, $r.body
    }
}

Write-Host ""
Write-Host "===== types 探测（netease）====="
foreach ($t in @('playlist', 'top', 'rank', 'list', 'song', 'album', 'artist', 'lyric', 'pic', 'url', 'search', 'tag', 'category', 'recommend', 'sheet')) {
    $u = "${base}?types=${t}&source=netease&id=3778678&name=${kw}&count=3"
    $r = Try-Url $u
    if ($r.ok) {
        $head = $r.body.Substring(0, [Math]::Min(80, $r.body.Length)) -replace "`r|`n", " "
        "{0,-11} HTTP {1} len={2,-6} {3}" -f $t, $r.status, $r.body.Length, $head
    } else {
        "{0,-11} 失败" -f $t
    }
}
