$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r * 2
  $p.AddArc($x, $y, $d, $d, 180, 90)
  $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
  $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
  $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
  $p.CloseFigure()
  return $p
}

# background: rounded square with vertical gradient
$rect = New-RoundRect 8 8 240 240 54
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
  (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 0, 256),
  [System.Drawing.Color]::FromArgb(255, 108, 128, 255),
  [System.Drawing.Color]::FromArgb(255, 88, 60, 220))
$g.FillPath($brush, $rect)

# subtle top highlight
$hl = New-RoundRect 8 8 240 120 54
$hlBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
  (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 0, 130),
  [System.Drawing.Color]::FromArgb(60, 255, 255, 255),
  [System.Drawing.Color]::FromArgb(0, 255, 255, 255))
$g.FillPath($hlBrush, $hl)

# download arrow
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$shaft = New-RoundRect 112 62 32 76 14
$g.FillPath($white, $shaft)
$arrow = New-Object System.Drawing.Drawing2D.GraphicsPath
$arrow.AddPolygon(@(
  (New-Object System.Drawing.Point 82, 128),
  (New-Object System.Drawing.Point 174, 128),
  (New-Object System.Drawing.Point 128, 178)
))
$g.FillPath($white, $arrow)

# tray
$tray = New-RoundRect 66 194 124 18 9
$g.FillPath($white, $tray)

$g.Dispose()

# --- write 256x256 PNG-compressed ICO ---
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$ms.Dispose(); $bmp.Dispose()

$icoPath = Join-Path $root 'app.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]1)
$bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([Byte]0)
$bw.Write([UInt16]1); $bw.Write([UInt16]32)
$bw.Write([UInt32]$png.Length); $bw.Write([UInt32]22)
$bw.Write($png)
$bw.Flush(); $bw.Close(); $fs.Dispose()
"icon: $icoPath ($([math]::Round((Get-Item $icoPath).Length/1KB,1)) KB)"
