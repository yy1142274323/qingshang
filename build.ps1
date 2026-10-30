param(
  [switch]$NoIcon
)

$ErrorActionPreference = 'Stop'
$root = "C:\Users\Administrator\Documents\deepseek-harness\default-workspace\qingshang"
$csc  = "C:\Users\Administrator\tools\roslyn\tasks\net472\csc.exe"
$ref  = "$env:USERPROFILE\tools\net48-refs\build\.NETFramework\v4.8"
$out  = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$refs = @(
  "$ref\mscorlib.dll", "$ref\System.dll", "$ref\System.Core.dll", "$ref\System.Xml.dll",
  "$ref\System.Drawing.dll", "$ref\System.Windows.Forms.dll"
) | ForEach-Object { "/reference:$_" }

$files = Get-ChildItem (Join-Path $root "src\QingShang") -Recurse -Filter *.cs |
         ForEach-Object { $_.FullName }

$target = Join-Path $out "QingShang.exe"
$icon   = Join-Path $root "src\QingShang\Assets\app.ico"

$extra = @(
  "/win32manifest:$root\src\QingShang\app.manifest",
  "/resource:$icon,QingShang.app.ico"
)
if (-not $NoIcon) { $extra += "/win32icon:$icon" }

& $csc /nologo /nostdlib+ /target:winexe /platform:anycpu /langversion:9 /optimize+ `
  /out:"$target" /codepage:65001 /utf8output /nowarn:CS8632,CS0649,CS0169,CS0219 @refs @extra @files

if ($LASTEXITCODE -ne 0) {
  Write-Host "BUILD FAILED ($LASTEXITCODE)"
  exit $LASTEXITCODE
}

Write-Host ("BUILD OK -> {0} ({1:N1} KB)" -f (Split-Path $target -Leaf), ((Get-Item $target).Length / 1KB))
