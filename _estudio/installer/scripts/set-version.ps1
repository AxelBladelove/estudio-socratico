[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$PublicDisplayVersion,

    [Parameter(Mandatory = $true)]
    [string]$InternalPackageVersion
)

$ErrorActionPreference = "Stop"

# Get Repo Root (script is under _estudio/installer/scripts)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..\..")
Write-Host "Repo root resolved to: $repoRoot"

# 1. Update Directory.Build.props
$propsPath = Join-Path $repoRoot "_estudio\installer\Directory.Build.props"
if (Test-Path $propsPath) {
    Write-Host "Updating $propsPath..."
    $content = Get-Content $propsPath -Raw
    $content = $content -replace '<ProductVersion>[^<]+</ProductVersion>', "<ProductVersion>$InternalPackageVersion</ProductVersion>"
    Set-Content $propsPath $content -NoNewline
}

# 2. Update ProductInfo.cs
$productInfoPath = Join-Path $repoRoot "_estudio\installer\src\EstudioSocratico.Configurator.Core\ProductInfo.cs"
if (Test-Path $productInfoPath) {
    Write-Host "Updating $productInfoPath..."
    $content = Get-Content $productInfoPath -Raw
    $content = $content -replace 'public const string PublicDisplayVersion = "[^"]+";', "public const string PublicDisplayVersion = `"$PublicDisplayVersion`";"
    $content = $content -replace 'public const string Version = "[^"]+";', "public const string Version = `"$InternalPackageVersion`";"
    $content = $content -replace 'public const string SetupFileName = "[^"]+";', "public const string SetupFileName = `"Estudio-Socratico-Setup-v$InternalPackageVersion-x64.exe`";"
    Set-Content $productInfoPath $content -NoNewline
}

# 3. Update wixproj files
$wixBurnDir = Join-Path $repoRoot "_estudio\installer\packaging\wix-burn"
if (Test-Path $wixBurnDir) {
    Get-ChildItem (Join-Path $wixBurnDir "*.wixproj") | ForEach-Object {
        Write-Host "Updating $_..."
        $content = Get-Content $_.FullName -Raw
        $content = $content -replace '<ProductVersion Condition=[^>]+>[^<]+</ProductVersion>', "<ProductVersion Condition=`"'`$(ProductVersion)' == ''`">$InternalPackageVersion</ProductVersion>"
        Set-Content $_.FullName $content -NoNewline
    }
}

# 4. Update root package.json
$rootPackageJson = Join-Path $repoRoot "package.json"
if (Test-Path $rootPackageJson) {
    Write-Host "Updating $rootPackageJson..."
    $content = Get-Content $rootPackageJson -Raw
    $content = $content -replace '"version": "[^"]+"', "`"version`": `"$InternalPackageVersion`""
    Set-Content $rootPackageJson $content -NoNewline
}

# 5. Update README.md and other Markdown files
$markdownFiles = @(
    (Join-Path $repoRoot "README.md"),
    (Join-Path $repoRoot "_estudio\installer\README.md"),
    (Join-Path $repoRoot "_estudio\installer\docs\release-v2.md")
)

foreach ($mdFile in $markdownFiles) {
    if (Test-Path $mdFile) {
        Write-Host "Updating references in $mdFile..."
        $content = Get-Content $mdFile -Raw
        # Replace setup installer executable pattern (e.g. Estudio-Socratico-Setup-vX.Y.Z-x64.exe)
        $content = $content -replace 'Estudio-Socratico-Setup-v\d+\.\d+\.\d+-x64\.exe', "Estudio-Socratico-Setup-v$InternalPackageVersion-x64.exe"
        # Replace display version tags if any (e.g. Estudio Socrático X.Y)
        $content = $content -replace 'Estudio Socrático \d+\.\d+(?!\.\d+)', "Estudio Socrático $PublicDisplayVersion"
        Set-Content $mdFile $content -NoNewline
    }
}

Write-Host "Version setup completed successfully!"
Write-Host "PublicDisplayVersion: $PublicDisplayVersion"
Write-Host "InternalPackageVersion: $InternalPackageVersion"
