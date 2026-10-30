$ErrorActionPreference = 'Stop'
$root = "C:\Users\Administrator\Documents\deepseek-harness\default-workspace\qingshang"
Set-Location $root

# ---- 读 GitHub 凭据（不打印） ----
$gPath = "C:\Users\Administrator\.dsh\private\G.txt"
if (-not (Test-Path $gPath)) { $gPath = "C:\Users\Administrator\Desktop\G.txt" }
$gText = Get-Content $gPath -Encoding UTF8 -Raw
$m = [regex]::Match($gText, 'ghp_[A-Za-z0-9]{20,}')
if (-not $m.Success) { throw "G.txt 里没找到 GitHub token" }
$env:GH_TOKEN = $m.Value
Write-Host ("token loaded: " + $m.Value.Substring(0, 8) + "***")

# ---- .gitignore ----
$ignore = @'
dist/
music/
tools/probe/
*.m3u8
*.zip
.vs/
*.user
'@
[System.IO.File]::WriteAllText((Join-Path $root ".gitignore"), $ignore, (New-Object System.Text.UTF8Encoding $false))

# ---- git 初始化 ----
if (-not (Test-Path (Join-Path $root ".git"))) {
    git init | Out-Null
}
git config user.name  "yy1142274323" | Out-Null
git config user.email "50814269+yy1142274323@users.noreply.github.com" | Out-Null
git add -A
$status = git status --porcelain
if ($status) {
    git commit -m "清商音乐播放器 v1.0.0" | Out-Null
    Write-Host "committed"
} else {
    Write-Host "nothing to commit"
}
git branch -M main
Write-Host "--- 仓库状态 ---"
git log --oneline -1
git ls-files | Measure-Object | Select-Object -ExpandProperty Count | ForEach-Object { "tracked files: $_" }
