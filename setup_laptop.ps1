param(
    [switch]$DryRun,
    [switch]$SkipWinget,
    [switch]$SkipExtensions,
    [string]$GitName = "Estudiante",
    [string]$GitEmail = "estudiante@estudio.local"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[AVISO] $Message" -ForegroundColor Yellow
}

function Run-Command {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$Description
    )

    if ($DryRun) {
        Write-Host "[DRY RUN] $Description" -ForegroundColor DarkYellow
        Write-Host "          $FilePath $($Arguments -join ' ')" -ForegroundColor DarkGray
        return
    }

    Write-Host $Description -ForegroundColor Yellow
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo el comando: $FilePath $($Arguments -join ' ')"
    }
}

function Test-CommandExists {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Add-UserPathEntry {
    param([string]$PathToAdd)

    if (-not (Test-Path $PathToAdd)) {
        return
    }

    $currentUserPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $entries = @()
    if ($currentUserPath) {
        $entries = $currentUserPath -split ";"
    }

    if ($entries -contains $PathToAdd) {
        Write-Ok "PATH de usuario ya contiene $PathToAdd"
        return
    }

    if ($DryRun) {
        Write-Host "[DRY RUN] Agregaria al PATH de usuario: $PathToAdd" -ForegroundColor DarkYellow
        return
    }

    $newPath = if ($currentUserPath) { "$currentUserPath;$PathToAdd" } else { $PathToAdd }
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    $env:Path = "$env:Path;$PathToAdd"
    Write-Ok "Agregado al PATH de usuario: $PathToAdd"
}

function Install-WithWinget {
    param(
        [string]$PackageId,
        [string]$DisplayName
    )

    if ($SkipWinget) {
        Write-Warn "SkipWinget activo; no se instalara $DisplayName automaticamente."
        return
    }

    if (-not (Test-CommandExists "winget")) {
        Write-Warn "winget no esta disponible. Instala $DisplayName manualmente."
        return
    }

    Run-Command -FilePath "winget" -Arguments @(
        "install",
        "--id", $PackageId,
        "-e",
        "--accept-package-agreements",
        "--accept-source-agreements"
    ) -Description "Instalando $DisplayName con winget..."
}

function Get-CodeCommand {
    if (Test-CommandExists "code") {
        return "code"
    }

    $commonPath = Join-Path $env:LOCALAPPDATA "Programs\Microsoft VS Code\bin\code.cmd"
    if (Test-Path $commonPath) {
        return $commonPath
    }

    return $null
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  Estudio Socratico - Setup del Proyecto       " -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Warn "Modo DryRun activo: se mostraran acciones sin modificar el sistema."
}

Write-Step "Verificando estructura del proyecto"

$requiredFiles = @(
    "compilar_y_grabar.bat",
    ".vscode\tasks.json",
    ".agent\skills\revisar\SKILL.md",
    ".agent\skills\sintetizar\SKILL.md",
    "AGENTS.md"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path (Join-Path $repoRoot $file))) {
        throw "Falta archivo requerido: $file"
    }
}

foreach ($dir in @("logs", "Ejercicios")) {
    if (-not (Test-Path $dir)) {
        if ($DryRun) {
            Write-Host "[DRY RUN] Crearia carpeta $dir" -ForegroundColor DarkYellow
        } else {
            New-Item -ItemType Directory -Path $dir | Out-Null
        }
    }
}

Write-Ok "Estructura base verificada."

Write-Step "Verificando Git"

if (-not (Test-CommandExists "git")) {
    Install-WithWinget -PackageId "Git.Git" -DisplayName "Git"
} else {
    Write-Ok "Git ya esta disponible."
}

if (Test-CommandExists "git") {
    if (-not (Test-Path ".git")) {
        Run-Command -FilePath "git" -Arguments @("init") -Description "Inicializando repositorio Git..."
    } else {
        Write-Ok "Repositorio Git ya existe."
    }

    Run-Command -FilePath "git" -Arguments @("config", "user.name", $GitName) -Description "Configurando user.name local..."
    Run-Command -FilePath "git" -Arguments @("config", "user.email", $GitEmail) -Description "Configurando user.email local..."
}

Write-Step "Verificando MSYS2 y GCC"

if (-not (Test-Path "C:\msys64")) {
    Install-WithWinget -PackageId "MSYS2.MSYS2" -DisplayName "MSYS2"
} else {
    Write-Ok "MSYS2 ya esta instalado."
}

$bashPath = "C:\msys64\usr\bin\bash.exe"
$gccPath = "C:\msys64\mingw64\bin\gcc.exe"

if ((Test-Path $bashPath) -and (-not (Test-Path $gccPath))) {
    Run-Command -FilePath $bashPath -Arguments @("-lc", "pacman -S --needed --noconfirm mingw-w64-x86_64-gcc") -Description "Instalando GCC MinGW64 dentro de MSYS2..."
}

if (Test-Path $gccPath) {
    Write-Ok "GCC disponible en $gccPath"
    Add-UserPathEntry -PathToAdd (Split-Path -Parent $gccPath)
} else {
    Write-Warn "No se encontro GCC. Abre MSYS2 MinGW64 y ejecuta: pacman -S --needed mingw-w64-x86_64-gcc"
}

Write-Step "Verificando VS Code ya instalado"

$codeCommand = Get-CodeCommand
if ($codeCommand) {
    Write-Ok "VS Code disponible: $codeCommand"
} else {
    Write-Warn "No encontre el comando de VS Code. Se asume que VS Code ya esta instalado; abre el proyecto manualmente."
}

Write-Ok "El setup no modifica tus settings de usuario ni tu tema de VS Code."

if ((-not $SkipExtensions) -and $codeCommand -and (Test-Path ".vscode\extensions.json")) {
    $extensionsConfig = Get-Content ".vscode\extensions.json" | ConvertFrom-Json
    foreach ($extension in $extensionsConfig.recommendations) {
        Run-Command -FilePath $codeCommand -Arguments @("--install-extension", $extension) -Description "Instalando/revisando extension VS Code: $extension"
    }
} elseif ($SkipExtensions) {
    Write-Warn "SkipExtensions activo; no se instalaran extensiones de VS Code."
}

Write-Step "Validando configuracion de VS Code"

foreach ($jsonFile in @(".vscode\tasks.json", ".vscode\settings.json", ".vscode\extensions.json")) {
    if (Test-Path $jsonFile) {
        try {
            Get-Content $jsonFile | ConvertFrom-Json | Out-Null
            Write-Ok "$jsonFile es JSON valido."
        } catch {
            throw "$jsonFile no es JSON valido: $($_.Exception.Message)"
        }
    }
}

Write-Step "Gestores JS disponibles"

$managerStatus = @()
foreach ($manager in @("npm", "pnpm", "bun", "npx")) {
    if (Test-CommandExists $manager) {
        $managerStatus += "${manager}: OK"
    } else {
        $managerStatus += "${manager}: no disponible"
    }
}
$managerStatus | ForEach-Object { Write-Host "  $_" }

Write-Step "Resultado"

Write-Host "Setup completado." -ForegroundColor Green
Write-Host ""
Write-Host "Comandos soportados:" -ForegroundColor Cyan
Write-Host "  npm run setup"
Write-Host "  pnpm run setup"
Write-Host "  bun run setup"
Write-Host "  npx --yes npm@latest run setup"
Write-Host ""
Write-Host "Despues abre un .c en Ejercicios/ y presiona Ctrl+Shift+B."
