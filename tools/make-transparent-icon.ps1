$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Make-TransparentBg([string]$src, [string]$dst) {
  $srcBmp = New-Object System.Drawing.Bitmap $src
  $bmp = New-Object System.Drawing.Bitmap $srcBmp.Width, $srcBmp.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.DrawImage($srcBmp, 0, 0, $srcBmp.Width, $srcBmp.Height)
  $g.Dispose()
  $srcBmp.Dispose()

  $w = $bmp.Width
  $h = $bmp.Height

  function IsBg([System.Drawing.Color]$c) {
    if ($c.A -lt 16) { return $true }
    $max = [Math]::Max([int]$c.R, [Math]::Max([int]$c.G, [int]$c.B))
    $min = [Math]::Min([int]$c.R, [Math]::Min([int]$c.G, [int]$c.B))
    $lum = 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B
    return ($lum -ge 200 -and ($max - $min) -le 45)
  }

  $visited = New-Object 'bool[,]' $w, $h
  $q = New-Object System.Collections.Generic.Queue[object]
  $seeds = @(
    @(0, 0),
    @(($w - 1), 0),
    @(0, ($h - 1)),
    @(($w - 1), ($h - 1)),
    @([int]($w / 2), 0),
    @(0, [int]($h / 2)),
    @([int]($w / 2), ($h - 1)),
    @(($w - 1), [int]($h / 2))
  )
  foreach ($p in $seeds) {
    $sx = [int]$p[0]; $sy = [int]$p[1]
    if (-not $visited[$sx, $sy] -and (IsBg $bmp.GetPixel($sx, $sy))) {
      $visited[$sx, $sy] = $true
      $q.Enqueue(@($sx, $sy))
    }
  }

  $cleared = 0
  $dirs = @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))
  while ($q.Count -gt 0) {
    $cell = $q.Dequeue()
    $x = [int]$cell[0]
    $y = [int]$cell[1]
    $bmp.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
    $cleared++
    foreach ($d in $dirs) {
      $nx = $x + [int]$d[0]
      $ny = $y + [int]$d[1]
      if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
      if ($visited[$nx, $ny]) { continue }
      if (IsBg $bmp.GetPixel($nx, $ny)) {
        $visited[$nx, $ny] = $true
        $q.Enqueue(@($nx, $ny))
      }
    }
  }

  $bmp.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "cleared=$cleared -> $dst"
}

function Resize-Png([string]$src, [string]$dst, [int]$size) {
  $img = [System.Drawing.Image]::FromFile($src)
  $bmp = New-Object System.Drawing.Bitmap $size, $size
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::Transparent)
  $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.DrawImage($img, 0, 0, $size, $size)
  $g.Dispose()
  $img.Dispose()
  if (Test-Path $dst) { Remove-Item $dst -Force }
  $bmp.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

function Report([string]$path) {
  $b = New-Object System.Drawing.Bitmap $path
  $t = 0; $o = 0; $sum = 0.0
  for ($y = 0; $y -lt $b.Height; $y++) {
    for ($x = 0; $x -lt $b.Width; $x++) {
      $c = $b.GetPixel($x, $y)
      if ($c.A -lt 16) { $t++ }
      else { $o++; $sum += 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B }
    }
  }
  $c0 = $b.GetPixel(0, 0)
  "{0}: trans={1:p0} opaqueLum={2:n0} cornerA={3}" -f (Split-Path $path -Leaf), ($t / ($b.Width * $b.Height)), $(if ($o) { $sum / $o } else { 0 }), $c0.A
  $b.Dispose()
}

$assets = "C:\Users\User\.cursor\projects\c-Users-User-Documents-Cursor-navisworks-plugin-export-to-CAD\assets"
$repo = "C:\Users\User\Documents\Cursor\navisworks-plugin-export-to-CAD"
$alpha = Join-Path $assets "glb-export-icon-alpha.png"
Make-TransparentBg (Join-Path $assets "glb-export-icon-transparent.png") $alpha
Copy-Item $alpha (Join-Path $repo "assets\glb-export-icon-alpha.png") -Force
Resize-Png $alpha "$repo\NavisworksExport.Glb\Images\glb16.png" 16
Resize-Png $alpha "$repo\NavisworksExport.Glb\Images\glb32.png" 32
Report $alpha
Report "$repo\NavisworksExport.Glb\Images\glb32.png"
