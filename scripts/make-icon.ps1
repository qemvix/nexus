$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$iconDirectory = Join-Path $repositoryRoot 'apps\desktop\Assets'
New-Item -ItemType Directory -Path $iconDirectory -Force | Out-Null
$bitmap = [System.Drawing.Bitmap]::new(64, 64)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(16, 19, 32))
$orbit = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(164, 151, 255), 2)
$fill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(186, 172, 255))
$inside = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(23, 26, 43), 2)
$graphics.DrawEllipse($orbit, 7, 7, 50, 50)
$diamond = [System.Drawing.Point[]]@([System.Drawing.Point]::new(32, 15), [System.Drawing.Point]::new(49, 32), [System.Drawing.Point]::new(32, 49), [System.Drawing.Point]::new(15, 32))
$graphics.FillPolygon($fill, $diamond)
$innerDiamond = [System.Drawing.Point[]]@([System.Drawing.Point]::new(32, 21), [System.Drawing.Point]::new(43, 32), [System.Drawing.Point]::new(32, 43), [System.Drawing.Point]::new(21, 32))
$graphics.DrawPolygon($inside, $innerDiamond)
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream = [System.IO.File]::Create((Join-Path $iconDirectory 'nexus.ico'))
try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose(); $inside.Dispose(); $fill.Dispose(); $orbit.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
