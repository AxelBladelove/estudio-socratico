# build-icons.ps1
# Script to generate high-quality icons and branding images from SVG vectors using headless Microsoft Edge.
# Requires no third-party libraries or external dependencies.

$ErrorActionPreference = "Stop"

# Paths setup
$brandingDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Split-Path -Parent (Split-Path -Parent $brandingDir)

$svgAppPath = Join-Path $brandingDir "logo-app.svg"
$svgTilePath = Join-Path $brandingDir "logo-app-tile.svg"
$icoOutputPath = Join-Path $brandingDir "logo-configurator.ico"
$pngExtensionOutputPath = Join-Path $brandingDir "logo-vscode-extension.png"

# Temporary workspace setup
$tempDir = Join-Path $env:TEMP "EstudioSocraticoBrandingBuild"
$tempUserDir = Join-Path $tempDir "EdgeUserDir"
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
New-Item -ItemType Directory -Path $tempUserDir -Force | Out-Null

$nodePath = (Get-Command node -ErrorAction Stop).Source
$bunCommand = Get-Command bun -ErrorAction SilentlyContinue
$sharpToolDir = Join-Path $env:TEMP "estudio-branding-tools"
$sharpModulesPath = Join-Path $sharpToolDir "node_modules"
if (-not (Test-Path (Join-Path $sharpModulesPath "sharp"))) {
    New-Item -ItemType Directory -Force -Path $sharpToolDir | Out-Null
    if (-not (Test-Path (Join-Path $sharpToolDir "package.json"))) {
        @"
{
  "private": true,
  "trustedDependencies": [
    "sharp"
  ]
}
"@ | Set-Content -LiteralPath (Join-Path $sharpToolDir "package.json") -Encoding utf8
    }
    if ($bunCommand) {
        & $bunCommand.Source add sharp@0.33.5 --cwd $sharpToolDir | Out-Null
    } else {
        Write-Warning "Bun no esta disponible; fallback puntual a npm para herramienta temporal de branding."
        npm install --prefix $sharpToolDir sharp@0.33.5 | Out-Null
    }
}

$sharpRenderScriptPath = Join-Path $tempDir "render-svg.cjs"
@"
const sharp = require("sharp");
const [, , input, output, sizeArg] = process.argv;
const size = Number(sizeArg);

sharp(input, { density: 384 })
  .resize(size, size, {
    fit: "contain",
    background: { r: 0, g: 0, b: 0, alpha: 0 }
  })
  .png()
  .toFile(output)
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });
"@ | Set-Content -LiteralPath $sharpRenderScriptPath -Encoding utf8

Write-Host "Starting branding assets compilation..." -ForegroundColor Cyan

# Helper to render SVG at target resolution
function Render-SvgToPng {
    param (
        [string]$SvgPath,
        [int]$Size,
        [bool]$StripShadow,
        [string]$PngOutputPath
    )

    $svgContent = [System.IO.File]::ReadAllText($SvgPath)
    if ($StripShadow) {
        # Strip shadow filters for small resolutions to keep the silhouette crisp.
        $svgContent = $svgContent -replace '\sfilter="url\(#(?:softShadow|shadow)\)"', ''
    }

    $svgTempPath = Join-Path $tempDir ("render_{0}_{1}.svg" -f $Size, ([guid]::NewGuid().ToString("N")))
    $pngTempPath = Join-Path $tempDir "out_$($Size).png"
    [System.IO.File]::WriteAllText($svgTempPath, $svgContent, [System.Text.Encoding]::UTF8)

    $previousNodePath = $env:NODE_PATH
    $env:NODE_PATH = $sharpModulesPath
    try {
        & $nodePath $sharpRenderScriptPath $svgTempPath $pngTempPath $Size
    }
    finally {
        $env:NODE_PATH = $previousNodePath
    }

    if (-not (Test-Path $pngTempPath)) {
        throw "Failed to render PNG for size $($Size)px via sharp."
    }

    Move-Item -Path $pngTempPath -Destination $PngOutputPath -Force
}

# Helper to bundle multiple PNGs into a standard ICO file
function Write-IcoFile {
    param (
        [string[]]$PngPaths,
        [string]$OutputPath
    )

    $count = $PngPaths.Count
    # Header: 2 bytes reserved (0), 2 bytes type (1 = ICO), 2 bytes image count
    $header = [byte[]]@(0, 0, 1, 0, ($count -band 0xFF), (($count -shr 8) -band 0xFF))
    
    $pngData = @()
    $entries = @()
    $offset = 6 + ($count * 16) # Header (6) + Entries (16 bytes each)

    foreach ($pngPath in $PngPaths) {
        $bytes = [System.IO.File]::ReadAllBytes($pngPath)
        
        $filename = [System.IO.Path]::GetFileNameWithoutExtension($pngPath)
        $size = [int]($filename -replace '\D')
        
        $width = if ($size -ge 256) { 0 } else { $size }
        $height = if ($size -ge 256) { 0 } else { $size }
        
        $dataSize = $bytes.Length
        
        # Directory entry (16 bytes)
        $entry = [byte[]]@(
            $width,
            $height,
            0, # color palette count (0 = >=8bpp)
            0, # reserved
            1, 0, # planes (1)
            32, 0, # bpp (32)
            ($dataSize -band 0xFF), (($dataSize -shr 8) -band 0xFF), (($dataSize -shr 16) -band 0xFF), (($dataSize -shr 24) -band 0xFF),
            ($offset -band 0xFF), (($offset -shr 8) -band 0xFF), (($offset -shr 16) -band 0xFF), (($offset -shr 24) -band 0xFF)
        )
        
        $entries += $entry
        $pngData += ,$bytes
        $offset += $dataSize
    }
    
    # Write components to target binary ICO file
    $fs = [System.IO.File]::Create($OutputPath)
    $fs.Write($header, 0, $header.Length)
    foreach ($entry in $entries) {
        $fs.Write($entry, 0, $entry.Length)
    }
    foreach ($data in $pngData) {
        $fs.Write($data, 0, $data.Length)
    }
    $fs.Close()
}

function Make-PngBackgroundTransparent {
    param (
        [string]$PngPath,
        [int]$Tolerance = 32
    )

    Add-Type -AssemblyName System.Drawing

    $source = [System.Drawing.Bitmap]::FromFile($PngPath)
    $background = $source.GetPixel(0, 0)
    $output = New-Object System.Drawing.Bitmap($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $visited = New-Object 'bool[,]' $source.Width, $source.Height
    $queue = [System.Collections.Generic.Queue[System.Drawing.Point]]::new()

    function Test-BackgroundPixel([System.Drawing.Color]$pixel, [System.Drawing.Color]$bg, [int]$tol) {
        return ([Math]::Abs($pixel.R - $bg.R) -le $tol) -and
               ([Math]::Abs($pixel.G - $bg.G) -le $tol) -and
               ([Math]::Abs($pixel.B - $bg.B) -le $tol)
    }

    for ($x = 0; $x -lt $source.Width; $x++) {
        foreach ($y in @(0, ($source.Height - 1))) {
            $pixel = $source.GetPixel($x, $y)
            if (-not $visited[$x, $y] -and (Test-BackgroundPixel $pixel $background $Tolerance)) {
                $visited[$x, $y] = $true
                $queue.Enqueue([System.Drawing.Point]::new($x, $y))
            }
        }
    }

    for ($y = 0; $y -lt $source.Height; $y++) {
        foreach ($x in @(0, ($source.Width - 1))) {
            $pixel = $source.GetPixel($x, $y)
            if (-not $visited[$x, $y] -and (Test-BackgroundPixel $pixel $background $Tolerance)) {
                $visited[$x, $y] = $true
                $queue.Enqueue([System.Drawing.Point]::new($x, $y))
            }
        }
    }

    while ($queue.Count -gt 0) {
        $point = $queue.Dequeue()
        $x = $point.X
        $y = $point.Y
        $pixel = $source.GetPixel($x, $y)
        $output.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, $pixel.R, $pixel.G, $pixel.B))

        foreach ($neighbor in @(
            [System.Drawing.Point]::new($x - 1, $y),
            [System.Drawing.Point]::new($x + 1, $y),
            [System.Drawing.Point]::new($x, $y - 1),
            [System.Drawing.Point]::new($x, $y + 1)
        )) {
            if ($neighbor.X -lt 0 -or $neighbor.X -ge $source.Width -or $neighbor.Y -lt 0 -or $neighbor.Y -ge $source.Height) {
                continue
            }

            if ($visited[$neighbor.X, $neighbor.Y]) {
                continue
            }

            $neighborPixel = $source.GetPixel($neighbor.X, $neighbor.Y)
            if (Test-BackgroundPixel $neighborPixel $background $Tolerance) {
                $visited[$neighbor.X, $neighbor.Y] = $true
                $queue.Enqueue($neighbor)
            }
        }
    }

    for ($x = 0; $x -lt $source.Width; $x++) {
        for ($y = 0; $y -lt $source.Height; $y++) {
            if ($visited[$x, $y]) {
                continue
            }

            $output.SetPixel($x, $y, $source.GetPixel($x, $y))
        }
    }

    $source.Dispose()
    $output.Save($PngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $output.Dispose()
}

# --- Step 1: Render ICO PNG elements and compile logo-configurator.ico ---
$icoSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$pngTempFiles = @()

Write-Host "1. Rendering multi-resolution icon sizes from app SVG (transparent)..." -ForegroundColor DarkGray
foreach ($size in $icoSizes) {
    # Always strip shadow for crisp shell rendering on any background.
    $stripShadow = $true
    $pngDest = Join-Path $tempDir "ico_$($size).png"
    Render-SvgToPng -SvgPath $svgAppPath -Size $size -StripShadow $stripShadow -PngOutputPath $pngDest
    $pngTempFiles += $pngDest
    Write-Host "   - Rendered $($size)x$($size)px (StripShadow: $stripShadow)" -ForegroundColor DarkGray
}

Write-Host "2. Packaging PNG frames into logo-configurator.ico..." -ForegroundColor DarkGray
Write-IcoFile -PngPaths $pngTempFiles -OutputPath $icoOutputPath

# --- Step 2: Render transparent PNG branding asset ---
Write-Host "3. Rendering transparent PNG logo..." -ForegroundColor DarkGray
Render-SvgToPng -SvgPath $svgAppPath -Size 256 -StripShadow $true -PngOutputPath $pngExtensionOutputPath

# --- Step 3: Distribute generated branding files to project components ---
Write-Host "4. Distributing compiled icons across the repository..." -ForegroundColor DarkGray

# A. Configurator standard WinUI app
$appIcoDest = Join-Path $repoRoot "_estudio/installer/src/EstudioSocratico.Configurator.App/logo-configurator.ico"
Copy-Item -Path $icoOutputPath -Destination $appIcoDest -Force
Write-Host "   - Copied to Configurator.App" -ForegroundColor DarkGray

# B. Configurator elevated worker
$elevatedIcoDest = Join-Path $repoRoot "_estudio/installer/src/EstudioSocratico.Configurator.Elevated/logo-configurator.ico"
Copy-Item -Path $icoOutputPath -Destination $elevatedIcoDest -Force
Write-Host "   - Copied to Configurator.Elevated" -ForegroundColor DarkGray

# C. WiX bootstrapper/installer packaging folder
$wixIcoDest = Join-Path $repoRoot "_estudio/installer/packaging/wix-burn/logo-configurator.ico"
Copy-Item -Path $icoOutputPath -Destination $wixIcoDest -Force
Write-Host "   - Copied to WiX Burn folder" -ForegroundColor DarkGray

# D. VS Code extension assets
$vscodePngDest = Join-Path $repoRoot "_estudio/soporte/vscode/estudio-exercism/assets/logo-vscode-extension.png"
Copy-Item -Path $pngExtensionOutputPath -Destination $vscodePngDest -Force
Write-Host "   - Copied VS Code extension logo" -ForegroundColor DarkGray

$uiLogoDest = Join-Path $repoRoot "_estudio/installer/ui/src/assets/logo-app.svg"
Copy-Item -Path $svgAppPath -Destination $uiLogoDest -Force
Write-Host "   - Copied UI logo-app.svg" -ForegroundColor DarkGray

$uiTileDest = Join-Path $repoRoot "_estudio/installer/ui/src/assets/logo-app-tile.svg"
Copy-Item -Path $svgTilePath -Destination $uiTileDest -Force
Write-Host "   - Copied UI logo-app-tile.svg" -ForegroundColor DarkGray

$vscodeActivityDest = Join-Path $brandingDir "logo-vscode-activity.svg"
$vscodeActivityTarget = Join-Path $repoRoot "_estudio/soporte/vscode/estudio-exercism/assets/logo-vscode-activity.svg"
Copy-Item -Path $vscodeActivityDest -Destination $vscodeActivityTarget -Force
Write-Host "   - Copied VS Code activity bar SVG" -ForegroundColor DarkGray

# Cleaning temporary build folder
Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Branding compilation completed successfully!" -ForegroundColor Green
