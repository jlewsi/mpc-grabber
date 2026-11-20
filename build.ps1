param([switch]$NoIcon)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

# make sure a running instance is not locking the output file
Get-Process MPCGrabber -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$outDir = Join-Path $root 'bin'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$exe = Join-Path $outDir 'MPCGrabber.exe'

$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw 'no sources' }

$args = @(
  '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/debug-',
  "/out:$exe",
  "/win32manifest:$(Join-Path $root 'app.manifest')",
  '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll',
  '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll'
)
if (-not $NoIcon) {
  $ico = Join-Path $root 'app.ico'
  if (Test-Path $ico) { $args += "/win32icon:$ico" }
}
$args += $sources

& $csc @args
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
$size = [math]::Round((Get-Item $exe).Length / 1KB, 1)
"built: $exe ($size KB)"
