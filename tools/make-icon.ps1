# Draws the app icon (a crescent Moon and stars on a night-blue tile) and saves assets\icon.ico
# with sizes from 16 to 256 pixels. Run it again only if you change the design.
Add-Type -AssemblyName System.Drawing
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here '..\assets\icon.ico'

function Draw-Icon([int]$size) {
    $k = $size / 32.0
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'

    # Rounded night-blue tile
    $r = New-Object System.Drawing.RectangleF (1 * $k), (1 * $k), (30 * $k), (30 * $k)
    $d = 14 * $k
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $r, ([System.Drawing.Color]::FromArgb(36, 58, 140)), ([System.Drawing.Color]::FromArgb(9, 12, 36)), 90.0
    $g.FillPath($lg, $p)

    # Crescent Moon: a full Moon on its own layer with a bite taken out, so the edges stay smooth
    $layer = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $lgx = [System.Drawing.Graphics]::FromImage($layer)
    $lgx.SmoothingMode = 'AntiAlias'
    $moonRect = New-Object System.Drawing.RectangleF (6.5 * $k), (6.5 * $k), (17 * $k), (17 * $k)
    $mb = New-Object System.Drawing.Drawing2D.LinearGradientBrush $moonRect, ([System.Drawing.Color]::FromArgb(255, 238, 170)), ([System.Drawing.Color]::FromArgb(245, 196, 90)), 45.0
    $lgx.FillEllipse($mb, $moonRect)
    $lgx.CompositingMode = 'SourceCopy'
    $clear = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $lgx.FillEllipse($clear, (12 * $k), (3.5 * $k), (16 * $k), (16 * $k))
    $lgx.Dispose()
    $g.DrawImage($layer, 0, 0, $size, $size)

    # Stars
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.FillEllipse($white, (21.5 * $k), (21 * $k), (3 * $k), (3 * $k))
    $g.FillEllipse($white, (24.5 * $k), (9 * $k), (2.2 * $k), (2.2 * $k))
    if ($size -ge 24) { $g.FillEllipse($white, (9 * $k), (24.5 * $k), (2 * $k), (2 * $k)) }
    $g.Dispose()
    return $bmp
}

# The 256 size is stored as a PNG; smaller sizes as plain bitmaps, which every Windows icon reader understands
function Icon-Bitmap([System.Drawing.Bitmap]$b) {
    $s = $b.Width
    $rect = New-Object System.Drawing.Rectangle 0, 0, $s, $s
    $data = $b.LockBits($rect, 'ReadOnly', ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $pixels = New-Object byte[] ($s * $s * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $b.UnlockBits($data)
    $maskRow = [int]([math]::Ceiling($s / 32.0) * 4)
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $w.Write([uint32]40); $w.Write([int32]$s); $w.Write([int32]($s * 2))   # height counts the mask too
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]0)
    $w.Write([uint32]($s * $s * 4 + $maskRow * $s)); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($pixels, $y * $s * 4, $s * 4) }   # rows go bottom-up
    $w.Write((New-Object byte[] ($maskRow * $s)))                                 # empty mask: alpha does the work
    $w.Flush()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $b = Draw-Icon $s
    if ($s -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
    } else { $bytes = Icon-Bitmap $b }
    $b.Dispose()
    , $bytes
}

# An .ico file is a small header, one entry per size, then the pictures
$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $w.Write($png) }
$w.Close()
Write-Host "Saved $((Resolve-Path $out).Path)"
