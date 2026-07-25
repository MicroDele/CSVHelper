$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$assetDir = Join-Path $root 'CsvReaderApp\Assets'
$icoPath = Join-Path $assetDir 'CSVHelper.ico'

if (-not (Test-Path -LiteralPath $assetDir)) {
    New-Item -ItemType Directory -Path $assetDir | Out-Null
}

function New-IconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $Size / 256.0
    $graphics.ScaleTransform($scale, $scale)

    $darkGreen = [System.Drawing.Color]::FromArgb(22, 134, 92)
    $sheetTop = [System.Drawing.Color]::FromArgb(248, 255, 251)
    $sheetBottom = [System.Drawing.Color]::FromArgb(223, 247, 236)
    $gridDark = [System.Drawing.Color]::FromArgb(21, 131, 91)
    $gridMid = [System.Drawing.Color]::FromArgb(103, 173, 144)
    $gridLight = [System.Drawing.Color]::FromArgb(183, 220, 203)
    $corner = [System.Drawing.Color]::FromArgb(159, 216, 186)
    $pencilA = [System.Drawing.Color]::FromArgb(255, 208, 90)
    $pencilB = [System.Drawing.Color]::FromArgb(240, 138, 40)
    $brown = [System.Drawing.Color]::FromArgb(112, 72, 27)
    $lead = [System.Drawing.Color]::FromArgb(91, 59, 34)

    $shadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(60, 0, 0, 0))
    $shadowPath = New-RoundedRectanglePath -X 38 -Y 22 -Width 188 -Height 220 -Radius 28
    $graphics.FillPath($shadowBrush, $shadowPath)
    $shadowPath.Dispose()
    $shadowBrush.Dispose()

    $backBrush = New-Object System.Drawing.SolidBrush $darkGreen
    $backPath = New-RoundedRectanglePath -X 34 -Y 18 -Width 188 -Height 220 -Radius 28
    $graphics.FillPath($backBrush, $backPath)
    $backPath.Dispose()
    $backBrush.Dispose()

    $sheetBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.RectangleF]::new(34, 18, 188, 220)), $sheetTop, $sheetBottom, 60
    $sheetPath = New-RoundedRectanglePath -X 34 -Y 18 -Width 188 -Height 220 -Radius 28
    $graphics.FillPath($sheetBrush, $sheetPath)
    $sheetPath.Dispose()
    $sheetBrush.Dispose()

    $cornerPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cornerPath.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(170, 18),
        [System.Drawing.PointF]::new(222, 70),
        [System.Drawing.PointF]::new(183, 70),
        [System.Drawing.PointF]::new(170, 57)
    ))
    $cornerBrush = New-Object System.Drawing.SolidBrush $corner
    $graphics.FillPath($cornerBrush, $cornerPath)
    $cornerBrush.Dispose()
    $cornerPath.Dispose()

    $penDark = New-Object System.Drawing.Pen $gridDark, 12
    $penDark.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penDark.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($penDark, 68, 88, 188, 88)
    $penDark.Dispose()

    $penMid = New-Object System.Drawing.Pen $gridMid, 10
    $penMid.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penMid.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($penMid, 68, 122, 188, 122)
    $graphics.DrawLine($penMid, 68, 156, 152, 156)
    $penMid.Dispose()

    $penLight = New-Object System.Drawing.Pen $gridLight, 8
    $penLight.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penLight.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($penLight, 92, 75, 92, 169)
    $graphics.DrawLine($penLight, 132, 75, 132, 169)
    $graphics.DrawLine($penLight, 172, 75, 172, 145)
    $penLight.Dispose()

    $pencilPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pencilPath.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(152, 190),
        [System.Drawing.PointF]::new(201, 141),
        [System.Drawing.PointF]::new(232, 172),
        [System.Drawing.PointF]::new(183, 221),
        [System.Drawing.PointF]::new(144, 229)
    ))
    $pencilBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.RectangleF]::new(144, 141, 88, 88)), $pencilA, $pencilB, 35
    $graphics.FillPath($pencilBrush, $pencilPath)
    $pencilPen = New-Object System.Drawing.Pen $brown, 8
    $pencilPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($pencilPen, $pencilPath)
    $pencilBrush.Dispose()
    $pencilPen.Dispose()
    $pencilPath.Dispose()

    $highlightPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 240, 176)), 8
    $highlightPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $highlightPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($highlightPen, 200, 142, 231, 173)
    $highlightPen.Dispose()

    $tipPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tipPath.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(144, 229),
        [System.Drawing.PointF]::new(156, 194),
        [System.Drawing.PointF]::new(179, 217)
    ))
    $tipBrush = New-Object System.Drawing.SolidBrush $lead
    $graphics.FillPath($tipBrush, $tipPath)
    $tipBrush.Dispose()
    $tipPath.Dispose()

    $graphics.Dispose()
    return $bitmap
}

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Convert-BitmapToIcoImageBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream
    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $pixelBytes = $width * $height * 4
    $maskStride = [int]([Math]::Ceiling($width / 32.0) * 4)
    $maskBytes = $maskStride * $height

    $writer.Write([UInt32]40)
    $writer.Write([Int32]$width)
    $writer.Write([Int32]($height * 2))
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]0)
    $writer.Write([UInt32]$pixelBytes)
    $writer.Write([Int32]0)
    $writer.Write([Int32]0)
    $writer.Write([UInt32]0)
    $writer.Write([UInt32]0)

    for ($y = $height - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $width; $x++) {
            $color = $Bitmap.GetPixel($x, $y)
            $writer.Write([byte]$color.B)
            $writer.Write([byte]$color.G)
            $writer.Write([byte]$color.R)
            $writer.Write([byte]$color.A)
        }
    }

    for ($i = 0; $i -lt $maskBytes; $i++) {
        $writer.Write([byte]0)
    }

    $writer.Flush()
    $bytes = $stream.ToArray()
    $writer.Dispose()
    $stream.Dispose()
    return ,$bytes
}

$sizes = @(16, 24, 32, 48, 64, 128)
$images = foreach ($size in $sizes) {
    $bitmap = New-IconBitmap -Size $size
    try {
        [pscustomobject]@{
            Size = $size
            Bytes = [byte[]](Convert-BitmapToIcoImageBytes -Bitmap $bitmap)
        }
    } finally {
        $bitmap.Dispose()
    }
}

$stream = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter $stream
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$images.Count)

    $offset = 6 + (16 * $images.Count)
    foreach ($image in $images) {
        $encodedSize = $image.Size
        if ($encodedSize -eq 256) {
            $encodedSize = 0
        }
        $writer.Write([byte]$encodedSize)
        $writer.Write([byte]$encodedSize)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$image.Bytes.Length)
        $writer.Write([UInt32]$offset)
        $offset += $image.Bytes.Length
    }

    foreach ($image in $images) {
        $writer.Write($image.Bytes)
    }
} finally {
    $writer.Dispose()
}

Write-Host "Icon written: $icoPath"
