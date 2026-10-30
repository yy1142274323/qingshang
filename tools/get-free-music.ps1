<#
.SYNOPSIS
    从 Internet Archive 批量抓公开授权音乐，生成本地歌单。

.DESCRIPTION
    只从 CC 授权 / 公共领域的集合里取（netlabels、78rpm、musopen 等），
    下载 mp3 到本地目录，最后生成一个 .m3u8 直接拖进播放器。

.EXAMPLE
    .\get-free-music.ps1                 # 默认 40 首，存到 music\
    .\get-free-music.ps1 -Count 100
    .\get-free-music.ps1 -Collection 78rpm -Count 30 -OutDir oldsongs
#>
param(
    [int]$Count = 40,
    [string]$OutDir = "music",
    [ValidateSet("netlabels", "78rpm", "audio_music", "opensource_audio", "musopen")]
    [string]$Collection = "netlabels",
    [int]$MaxMbPerFile = 15,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $here
if (-not [System.IO.Path]::IsPathRooted($OutDir)) {
    $OutDir = Join-Path $projectRoot $OutDir
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$ua = @{ 'User-Agent' = 'QingShang/1.0 (free music fetcher)' }

function Get-Json($url) {
    Invoke-RestMethod -Uri $url -Headers $ua -TimeoutSec 40
}

Write-Host "== 搜索 Internet Archive 集合: $Collection ==" -ForegroundColor Cyan

# 只要真正的音乐条目：用 subject 收窄，排掉口述 / 播客 / 电台录音
$musicSubjects = '(subject:jazz OR subject:rock OR subject:electronic OR subject:classical OR subject:pop OR subject:ambient OR subject:folk OR subject:instrumental OR subject:piano OR subject:guitar OR subject:blues OR subject:hiphop OR subject:soundtrack)'
$query = [uri]::EscapeDataString("collection:$Collection AND mediatype:audio AND $musicSubjects")
$searchUrl = "https://archive.org/advancedsearch.php?q=$query" +
             "&fl%5B%5D=identifier&fl%5B%5D=title&fl%5B%5D=creator" +
             "&rows=$([Math]::Max($Count * 3, 60))&page=1&output=json&sort%5B%5D=downloads+desc"

$search = Get-Json $searchUrl
$docs = @($search.response.docs)
Write-Host ("找到 {0} 个条目，开始筛选音频文件..." -f $search.response.numFound)

$downloaded = @()
$index = 0
$totalBytes = 0

foreach ($doc in $docs) {
    if ($downloaded.Count -ge $Count) { break }
    $index++

    try {
        $meta = Get-Json "https://archive.org/metadata/$($doc.identifier)"
    } catch {
        continue
    }

    $all = @($meta.files | Where-Object {
        $_.name -match '\.(mp3|m4a|ogg)$' -and
        $_.name -notmatch '(^|/)_' -and
        ($_.size -eq $null -or [int64]$_.size -lt ($MaxMbPerFile * 1MB))
    })

    # 同一个专辑别整张搬走：mp3 优先，最多取 3 首，顺便去掉 ogg 重复
    $mp3 = @($all | Where-Object { $_.name -match '\.(mp3|m4a)$' })
    $files = if ($mp3.Count -gt 0) { @($mp3 | Select-Object -First 3) } else { @($all | Select-Object -First 2) }

    if ($files.Count -eq 0) { continue }

    foreach ($f in $files) {
        if ($downloaded.Count -ge $Count) { break }

        $safeName = ($doc.identifier + "-" + ($f.name -replace '.*/', '')) -replace '[\\/:*?"<>|]', '_'
        $dest = Join-Path $OutDir $safeName

        if (Test-Path $dest) {
            $size = (Get-Item $dest).Length
            Write-Host ("  [跳过] {0}" -f $safeName) -ForegroundColor DarkGray
            $downloaded += $dest
            continue
        }

        $url = "https://archive.org/download/$($doc.identifier)/" + [uri]::EscapeDataString($f.name)
        $sizeMb = if ($f.size) { [math]::Round([int64]$f.size / 1MB, 1) } else { 0 }

        if ($DryRun) {
            Write-Host ("  [预演] {0}  ({1} MB)" -f $safeName, $sizeMb) -ForegroundColor Yellow
            $downloaded += $dest
            continue
        }

        Write-Host ("  [{0}/{1}] {2}  ({3} MB)" -f ($downloaded.Count + 1), $Count, $safeName, $sizeMb) -NoNewline
        try {
            curl.exe -sL --retry 2 --max-time 120 -o $dest $url
            if ((Test-Path $dest) -and (Get-Item $dest).Length -gt 1024) {
                $len = (Get-Item $dest).Length
                $totalBytes += $len
                Write-Host ("  ok {0:N1} MB" -f ($len / 1MB)) -ForegroundColor Green
                $downloaded += $dest
            } else {
                Remove-Item $dest -Force -ErrorAction SilentlyContinue
                Write-Host "  失败" -ForegroundColor Red
            }
        } catch {
            Write-Host ("  失败: {0}" -f $_.Exception.Message) -ForegroundColor Red
        }
    }
}

if ($downloaded.Count -eq 0) {
    Write-Host "没有下载到任何文件。" -ForegroundColor Yellow
    exit 1
}

# 生成歌单
$m3u = Join-Path $projectRoot ("playlist-" + $Collection + ".m3u8")
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("#EXTM3U")
foreach ($f in $downloaded) {
    [void]$sb.AppendLine("#EXTINF:-1," + [System.IO.Path]::GetFileNameWithoutExtension($f))
    [void]$sb.AppendLine([System.IO.Path]::GetFullPath($f))
}
[System.IO.File]::WriteAllText($m3u, $sb.ToString(), (New-Object System.Text.UTF8Encoding $true))

Write-Host ""
Write-Host ("完成：{0} 首，共 {1:N1} MB" -f $downloaded.Count, ($totalBytes / 1MB)) -ForegroundColor Cyan
Write-Host ("音乐目录：{0}" -f $OutDir)
Write-Host ("歌单文件：{0}" -f $m3u) -ForegroundColor Cyan
Write-Host "把这个 .m3u8 拖进播放器窗口就能整批导入。" -ForegroundColor DarkGray
