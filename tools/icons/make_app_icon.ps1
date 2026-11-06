# Draws Sketch's application icon (the splash screen's cube: three shaded faces with orange edges, on a transparent
# background) at the usual Windows sizes and writes them into one .ico file (PNG-compressed entries).
# Usage: powershell -ExecutionPolicy Bypass -File tools\icons\make_app_icon.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot "..\..\src\Sketch.App\Resources\Sketch.ico"
$sizes = 16, 24, 32, 48, 64, 128, 256

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 100.0

    # Cube outline spans x 8..92 and y 6..96: scaled to fill the icon (room left for the edge line), centred.
    function P([double]$x, [double]$y) { New-Object System.Drawing.PointF ([single]((($x - 8) * 0.94 + 10.5) * $s)), ([single]((($y - 6) * 0.94 + 7.7) * $s)) }
    $top = [System.Drawing.PointF[]]@((P 50 6), (P 92 28), (P 50 50), (P 8 28))
    $left = [System.Drawing.PointF[]]@((P 8 28), (P 50 50), (P 50 96), (P 8 74))
    $right = [System.Drawing.PointF[]]@((P 92 28), (P 50 50), (P 50 96), (P 92 74))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0xE9, 0xEE, 0xF5))), $top)
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0xA9, 0xB6, 0xC8))), $left)
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x7C, 0x8B, 0xA1))), $right)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 0xF0, 0x8A, 0x1C)), ([single][Math]::Max(1.0, 4.4 * $s))
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPolygon($pen, [System.Drawing.PointF[]]@((P 50 6), (P 92 28), (P 92 74), (P 50 96), (P 8 74), (P 8 28)))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((P 8 28), (P 50 50), (P 92 28)))
    $g.DrawLine($pen, (P 50 50), (P 50 96))

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return , $ms.ToArray()
}

$images = foreach ($size in $sizes) { , (New-IconPng $size) }
$stream = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $stream
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $out)).Path + "\Sketch.ico", $stream.ToArray())
Write-Output "Wrote $out ($($stream.Length) bytes)"
