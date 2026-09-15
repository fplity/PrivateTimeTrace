# Run with Windows PowerShell 5.1 (System.Drawing ships with Windows).
[CmdletBinding()]
param([string]$Source)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetRoot = [IO.Path]::GetFullPath("$PSScriptRoot\..\Assets")
if (!$Source) { $Source = Join-Path $assetRoot 'AppIcon.png' }
$original = [Drawing.Image]::FromFile([IO.Path]::GetFullPath($Source))
function Resize-Icon([int]$Width, [int]$Height, [double]$Fill = 1) {
    $bitmap = [Drawing.Bitmap]::new($Width, $Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $size = [int]([Math]::Min($Width, $Height) * $Fill)
        $graphics.DrawImage($original, [Drawing.Rectangle]::new(($Width-$size)/2, ($Height-$size)/2, $size, $size))
    } finally { $graphics.Dispose() }
    return $bitmap
}
try {
    $sizes = @(16,20,24,32,40,48,64,128,256)
    $frames = foreach ($size in $sizes) {
        $bitmap = Resize-Icon $size $size
        $stream = [IO.MemoryStream]::new()
        $writer = [IO.BinaryWriter]::new($stream)
        try {
            $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
            $writer.Write([int]40); $writer.Write([int]$size); $writer.Write([int]($size*2))
            $writer.Write([int16]1); $writer.Write([int16]32); $writer.Write([int]0)
            $writer.Write([int]($size*$size*4 + $maskStride*$size))
            1..4 | ForEach-Object { $writer.Write([int]0) }
            for ($y=$size-1; $y -ge 0; $y--) {
                for ($x=0; $x -lt $size; $x++) {
                    $pixel = $bitmap.GetPixel($x,$y)
                    $writer.Write([byte]$pixel.B); $writer.Write([byte]$pixel.G)
                    $writer.Write([byte]$pixel.R); $writer.Write([byte]$pixel.A)
                }
            }
            for ($y=$size-1; $y -ge 0; $y--) {
                $mask = [byte[]]::new($maskStride)
                for ($x=0; $x -lt $size; $x++) {
                    if ($bitmap.GetPixel($x,$y).A -eq 0) {
                        $index = [int][Math]::Floor($x/8)
                        $mask[$index] = $mask[$index] -bor (128 -shr ($x % 8))
                    }
                }
                $writer.Write($mask)
            }
            $writer.Flush()
            [pscustomobject]@{ Size=$size; Bytes=$stream.ToArray() }
        } finally { $writer.Dispose(); $stream.Dispose(); $bitmap.Dispose() }
    }
    $iconStream = [IO.File]::Create("$assetRoot\AppIcon.ico")
    $iconWriter = [IO.BinaryWriter]::new($iconStream)
    try {
        $iconWriter.Write([int16]0); $iconWriter.Write([int16]1); $iconWriter.Write([int16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = [byte]($frame.Size % 256)
            $iconWriter.Write($dimension); $iconWriter.Write($dimension)
            $iconWriter.Write([byte]0); $iconWriter.Write([byte]0)
            $iconWriter.Write([int16]1); $iconWriter.Write([int16]32)
            $iconWriter.Write([int]$frame.Bytes.Length); $iconWriter.Write([int]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $iconWriter.Write([byte[]]$frame.Bytes) }
    } finally { $iconWriter.Dispose(); $iconStream.Dispose() }
    $logos = @(
        @('Square44x44Logo.scale-200.png',88,88),
        @('Square44x44Logo.targetsize-24_altform-unplated.png',24,24),
        @('Square44x44Logo.targetsize-48_altform-lightunplated.png',48,48),
        @('Square150x150Logo.scale-200.png',300,300),
        @('Wide310x150Logo.scale-200.png',620,300),
        @('StoreLogo.png',50,50),
        @('LockScreenLogo.scale-200.png',48,48),
        @('SplashScreen.scale-200.png',1240,600)
    )
    foreach ($logo in $logos) {
        $bitmap = Resize-Icon $logo[1] $logo[2]
        try { $bitmap.Save("$assetRoot\$($logo[0])", [Drawing.Imaging.ImageFormat]::Png) }
        finally { $bitmap.Dispose() }
    }
    "Built ICO with $($frames.Count) alpha-preserving sizes and $($logos.Count) Windows logo assets."
} finally { $original.Dispose() }
