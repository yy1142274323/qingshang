param([string]$Argument = "", [string]$Out = "", [switch]$Mini)

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class WShot {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@
[WShot]::SetProcessDPIAware() | Out-Null

$root = "C:\Users\Administrator\Documents\deepseek-harness\default-workspace\qingshang"
if (-not $Out) { $Out = Join-Path $root "docs\main.png" }

Get-Process -Name QingShang -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
[WShot]::SetCursorPos(20, 950) | Out-Null
Start-Sleep -Milliseconds 250

$fileArgs = @()
if ($Argument) { $fileArgs = @($Argument) }
if ($Mini) { $fileArgs += "--mini" }

$p = Start-Process -FilePath (Join-Path $root "dist\QingShang.exe") -ArgumentList $fileArgs -PassThru

$h = [IntPtr]::Zero
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 250
    $p.Refresh()
    if ($p.HasExited) { Write-Host "EXITED $($p.ExitCode)"; exit 1 }
    $cand = $p.MainWindowHandle
    if ($cand -ne [IntPtr]::Zero) {
        $t = New-Object WShot+RECT
        if ([WShot]::GetWindowRect($cand, [ref]$t)) {
            if (($t.Right - $t.Left) -gt 300) { $h = $cand; break }
        }
    }
}
if ($h -eq [IntPtr]::Zero) { Write-Host "no window"; Stop-Process -Id $p.Id -Force; exit 1 }

[WShot]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0013) | Out-Null
[WShot]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 1200
[WShot]::SetCursorPos(20, 950) | Out-Null
Start-Sleep -Milliseconds 400

$r = New-Object WShot+RECT
[WShot]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$hh = $r.Bottom - $r.Top
$pad = 26
$bw = $w + $pad * 2
$bh = $hh + $pad * 2

$bmp = New-Object System.Drawing.Bitmap($bw, $bh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(($r.Left - $pad), ($r.Top - $pad), 0, 0, (New-Object System.Drawing.Size($bw, $bh)))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

[WShot]::SetWindowPos($h, [IntPtr](-2), 0, 0, 0, 0, 0x0003) | Out-Null
Write-Host ("saved: {0}  ({1}x{2})" -f $Out, $w, $hh)
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
