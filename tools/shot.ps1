param(
  [string]$Exe = "",
  [string]$OutDir = "",
  [int]$WaitSec = 3,
  [string]$Mode = "normal",
  [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int n);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
'@

[W32]::SetProcessDPIAware() | Out-Null

$root = "C:\Users\Administrator\Documents\deepseek-harness\default-workspace\qingshang"
if (-not $Exe) { $Exe = Join-Path $root "dist\QingShang.exe" }
if (-not $OutDir) { $OutDir = Join-Path $root "docs\shots" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$tracks = @(Join-Path $root "samples")

Get-Process -Name QingShang -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:TEMP "qingshang.log") -Force -ErrorAction SilentlyContinue
$proc = Start-Process -FilePath $Exe -ArgumentList $tracks -PassThru
Start-Sleep -Seconds $WaitSec

if ($proc.HasExited) {
  Write-Host "EXITED code=$($proc.ExitCode)"
  $log = Join-Path $env:TEMP "qingshang.log"
  if (Test-Path $log) { Get-Content $log -Raw -Encoding UTF8 }
  exit 1
}

$h = [IntPtr]::Zero
for ($i = 0; $i -lt 40; $i++) {
  Start-Sleep -Milliseconds 250
  $proc.Refresh()
  if ($proc.HasExited) { break }
  $cand = $proc.MainWindowHandle
  if ($cand -eq [IntPtr]::Zero) { $cand = [W32]::FindWindow($null, "清商播放器") }
  if ($cand -ne [IntPtr]::Zero) {
    $t = New-Object W32+RECT
    if ([W32]::GetWindowRect($cand, [ref]$t)) {
      if (($t.Right - $t.Left) -gt 200 -and ($t.Bottom - $t.Top) -gt 120) { $h = $cand; break }
    }
  }
}

Write-Host "hwnd=$h"

if ($Mode -eq "mini") {
  $r0 = New-Object W32+RECT
  [W32]::GetWindowRect($h, [ref]$r0) | Out-Null
  [W32]::SetCursorPos(($r0.Right - [int](110 * 1.25) + [int](15 * 1.25)), ($r0.Top + [int](25 * 1.25))) | Out-Null
  Start-Sleep -Milliseconds 300
  [W32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
  [W32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
  Start-Sleep -Milliseconds 900
}

if ($Mode -eq "play") {
  # 程序启动即自动播放，这里只等频谱和唱片动起来
  $r0 = New-Object W32+RECT
  [W32]::GetWindowRect($h, [ref]$r0) | Out-Null
  # 播放键：控制条左侧，距左 24+34+12+26 = 96，距底 66
  Start-Sleep -Milliseconds 2200
}

$HWND_TOPMOST = [IntPtr](-1)
$HWND_NOTOPMOST = [IntPtr](-2)
[W32]::SetWindowPos($h, $HWND_TOPMOST, 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040) | Out-Null
[W32]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 400

$rect = New-Object W32+RECT
[W32]::GetWindowRect($h, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
Write-Host "window ${width}x${height} at ($($rect.Left),$($rect.Top))"

$pad = 26
$x = [Math]::Max(0, $rect.Left - $pad)
$y = [Math]::Max(0, $rect.Top - $pad)
$w2 = $width + $pad * 2
$h2 = $height + $pad * 2
$sw = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width
$sh = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height
if ($x + $w2 -gt $sw) { $w2 = $sw - $x }
if ($y + $h2 -gt $sh) { $h2 = $sh - $y }

$bmp = New-Object System.Drawing.Bitmap($w2, $h2)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w2, $h2)))
$stamp = Get-Date -Format "HHmmss"
$path = Join-Path $OutDir "shot-$Mode-$stamp.png"
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

[W32]::SetWindowPos($h, $HWND_NOTOPMOST, 0, 0, 0, 0, 0x0001 -bor 0x0002) | Out-Null
Write-Host "saved: $path"
Write-Host "pid: $($proc.Id)"

if (-not $KeepOpen) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
