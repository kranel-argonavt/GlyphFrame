# Rebuild the static Windows shell asset from the existing in-app vector geometry.
# Run with: pwsh -NoProfile -STA -File scripts/Build-ShellIcon.ps1
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Run this generator with PowerShell 7 on Windows and -STA.'
}
Add-Type -AssemblyName PresentationCore, WindowsBase
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$xml = Get-Content -LiteralPath (Join-Path $repo 'src/GlyphFrame.App/Resources/Components.xaml') -Raw
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('w', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
$drawings = @($xml.SelectNodes('//w:DrawingImage[@x:Key="GlyphFrameWindowIcon"]//w:GeometryDrawing', $ns))
if ($drawings.Count -ne 3) { throw 'Unexpected in-app logo structure; review before regenerating.' }
# Keep the frame + GF paths exactly; omit only the accent background and use white.
$frame = [Windows.Media.Geometry]::Parse($drawings[1].GetAttribute('Geometry'))
$letters = [Windows.Media.Geometry]::Parse($drawings[2].GetAttribute('Geometry'))
$thickness = [double]::Parse($drawings[1].SelectSingleNode('.//w:Pen', $ns).GetAttribute('Thickness'), [Globalization.CultureInfo]::InvariantCulture)
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = foreach ($size in $sizes) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([Windows.Media.ScaleTransform]::new(($size * 4 / 32.0), ($size * 4 / 32.0)))
    $context.DrawGeometry($null, [Windows.Media.Pen]::new([Windows.Media.Brushes]::White, $thickness), $frame)
    $context.DrawGeometry([Windows.Media.Brushes]::White, $null, $letters)
    $context.Pop(); $context.Close()
    $large = [Windows.Media.Imaging.RenderTargetBitmap]::new(($size * 4), ($size * 4), 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $large.Render($visual)
    $downsample = [Windows.Media.DrawingVisual]::new()
    [Windows.Media.RenderOptions]::SetBitmapScalingMode($downsample, [Windows.Media.BitmapScalingMode]::HighQuality)
    $context = $downsample.RenderOpen()
    $context.DrawImage($large, [Windows.Rect]::new(0, 0, $size, $size)); $context.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($downsample)
    # Keep antialiased coverage, but avoid premultiplied rounding turning white into 254.
    $pixels = [byte[]]::new($size * $size * 4)
    $bitmap.CopyPixels($pixels, $size * 4, 0)
    for ($pixel = 0; $pixel -lt $pixels.Length; $pixel += 4) {
        if ($pixels[$pixel + 3] -ne 0) { $pixels[$pixel] = 255; $pixels[$pixel + 1] = 255; $pixels[$pixel + 2] = 255 }
    }
    $white = [Windows.Media.Imaging.BitmapSource]::Create($size, $size, 96, 96, [Windows.Media.PixelFormats]::Bgra32, $null, $pixels, $size * 4)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($white))
    $stream = [IO.MemoryStream]::new()
    try { $encoder.Save($stream); [pscustomobject]@{ Size=$size; Bytes=$stream.ToArray() } }
    finally { $stream.Dispose() }
}
$output = Join-Path $repo 'src/GlyphFrame.App/Assets/GlyphFrame.ico'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
$stream = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($item in $frames) {
        $dimension = if ($item.Size -eq 256) { 0 } else { $item.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$item.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $item.Bytes.Length
    }
    foreach ($item in $frames) { $writer.Write([byte[]]$item.Bytes) }
    $writer.Flush(); [IO.File]::WriteAllBytes($output, $stream.ToArray())
}
finally { $writer.Dispose(); $stream.Dispose() }
Write-Host "Generated white, transparent shell icon: $output ($($sizes -join ', ') px)"
