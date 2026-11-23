param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = 'C:\Users\jlews\OneDrive\Documents\deepseek-harness\default-workspace\mpc-grabber'
$src = Join-Path $root 'assets\icon-src.png'
$bannerSrc = Join-Path $root 'assets\banner-src.png'

# ---- icon: crop to the visible pixels, then emit a multi-size ICO ----
$img = [System.Drawing.Image]::FromFile($src)
$bmp = New-Object System.Drawing.Bitmap $img
$minX = $bmp.Width; $minY = $bmp.Height; $maxX = 0; $maxY = 0
for ($y = 0; $y -lt $bmp.Height; $y += 4) {
  for ($x = 0; $x -lt $bmp.Width; $x += 4) {
    if ($bmp.GetPixel($x, $y).A -gt 8) {
      if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
      if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
    }
  }
}
$pad = 6
$minX = [Math]::Max(0, $minX - $pad); $minY = [Math]::Max(0, $minY - $pad)
$maxX = [Math]::Min($bmp.Width - 1, $maxX + $pad); $maxY = [Math]::Min($bmp.Height - 1, $maxY + $pad)
$cropW = $maxX - $minX + 1; $cropH = $maxY - $minY + 1
"content box: $cropW x $cropH"

$entries = @()
foreach ($size in 16, 24, 32, 48, 64, 128, 256) {
  $square = New-Object System.Drawing.Bitmap $size, $size
  $g = [System.Drawing.Graphics]::FromImage($square)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)
  # keep the aspect ratio of the cropped art inside the square
  $scale = [Math]::Min($size / $cropW, $size / $cropH)
  $dw = [int]($cropW * $scale); $dh = [int]($cropH * $scale)
  $dx = [int](($size - $dw) / 2); $dy = [int](($size - $dh) / 2)
  $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle $dx, $dy, $dw, $dh), (New-Object System.Drawing.Rectangle $minX, $minY, $cropW, $cropH), [System.Drawing.GraphicsUnit]::Pixel)
  $g.Dispose()
  $ms = New-Object System.IO.MemoryStream
  $square.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $entries += , @{ Size = $size; Data = $ms.ToArray() }
  $ms.Dispose(); $square.Dispose()
}
$bmp.Dispose(); $img.Dispose()

$icoPath = Join-Path $root 'app.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
  $s = $e.Size
  $bw.Write([Byte]$(if ($s -ge 256) { 0 } else { $s }))
  $bw.Write([Byte]$(if ($s -ge 256) { 0 } else { $s }))
  $bw.Write([Byte]0); $bw.Write([Byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32)
  $bw.Write([UInt32]$e.Data.Length)
  $bw.Write([UInt32]$offset)
  $offset += $e.Data.Length
}
foreach ($e in $entries) { $bw.Write($e.Data) }
$bw.Flush(); $bw.Close(); $fs.Dispose()
"icon: $icoPath ($([math]::Round((Get-Item $icoPath).Length/1KB,1)) KB, $($entries.Count) sizes)"

# ---- banner: scale to the sidebar width, keep aspect ----
$bimg = [System.Drawing.Image]::FromFile($bannerSrc)
$targetW = 180
$targetH = [int][Math]::Round($bimg.Height * ($targetW / $bimg.Width))
$bb = New-Object System.Drawing.Bitmap $targetW, $targetH
$bg = [System.Drawing.Graphics]::FromImage($bb)
$bg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$bg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$bg.DrawImage($bimg, 0, 0, $targetW, $targetH)
$bg.Dispose(); $bimg.Dispose()
$bannerOut = Join-Path $root 'assets\banner.png'
$bb.Save($bannerOut, [System.Drawing.Imaging.ImageFormat]::Png)
$bb.Dispose()
"banner: $bannerOut (${targetW}x${targetH})"
