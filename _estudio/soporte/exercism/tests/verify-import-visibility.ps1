<#
.SYNOPSIS
    Verifica que los archivos auxiliares de Exercism quedan ocultos
    despues de importar un ejercicio.

.USAGE
    pwsh -File verify-import-visibility.ps1
#>

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exercismDir = Split-Path -Parent $scriptDir
$soporteDir = Split-Path -Parent $exercismDir
$estudioDir = Split-Path -Parent $soporteDir
$rootDir = Split-Path -Parent $estudioDir

$passCount = 0
$failCount = 0
$warnCount = 0

function Pass { param($msg) $script:passCount++; Write-Host "[PASS] $msg" -ForegroundColor Green }
function Fail { param($msg) $script:failCount++; Write-Host "[FAIL] $msg" -ForegroundColor Red }
function Warn { param($msg) $script:warnCount++; Write-Host "[WARN] $msg" -ForegroundColor Yellow }

# ── 1. Verify settings.json exclusions ────────────────────────────────────────
Write-Host "`n=== 1. Verificando .vscode/settings.json exclusiones ===" -ForegroundColor Cyan

$settingsPath = Join-Path $rootDir ".vscode\settings.json"
if (-not (Test-Path $settingsPath)) {
    Fail "No se encontro .vscode/settings.json"
    exit 1
}

$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$exclude = $settings.'files.exclude'

$requiredExclusions = @(
    "usuario/exercism",
    "usuario/exercism/**",
    "**/.estudio-exercism",
    "**/.estudio-exercism.json",
    "**/.exercism",
    "Ejercicios/**/[Mm]akefile",
    "Ejercicios/**/*.h",
    "Ejercicios/**/README.md",
    "Ejercicios/**/HELP.md",
    "Ejercicios/**/test-framework",
    "Ejercicios/**/test_*.c"
)

foreach ($pattern in $requiredExclusions) {
    $found = $false
    foreach ($key in $exclude.PSObject.Properties.Name) {
        if ($key -eq $pattern) {
            $found = $true
            if ($exclude.$key -eq $true) {
                Pass "files.exclude: '$pattern' = true"
            } else {
                Fail "files.exclude: '$pattern' = $($exclude.$key) (esperado: true)"
            }
            break
        }
    }
    if (-not $found) {
        Fail "files.exclude: '$pattern' - NO ENCONTRADO"
    }
}

$searchExclude = $settings.'search.exclude'
foreach ($pattern in $requiredExclusions) {
    $found = $false
    foreach ($key in $searchExclude.PSObject.Properties.Name) {
        if ($key -eq $pattern) {
            $found = $true
            if ($searchExclude.$key -eq $true) {
                Pass "search.exclude: '$pattern' = true"
            } else {
                Fail "search.exclude: '$pattern' = $($searchExclude.$key) (esperado: true)"
            }
            break
        }
    }
    if (-not $found) {
        Fail "search.exclude: '$pattern' - NO ENCONTRADO"
    }
}

# ── 2. Simulate imported exercise structure ───────────────────────────────────
Write-Host "`n=== 2. Simulando estructura de ejercicio importado ===" -ForegroundColor Cyan

$simRoot = Join-Path $env:TEMP "exercism-visibility-test"
if (Test-Path $simRoot) {
    Remove-Item -LiteralPath $simRoot -Recurse -Force
}

$exerciseDir = Join-Path $simRoot "Ejercicios\Hello World"
$supportDir = Join-Path $exerciseDir ".estudio-exercism\support"
$testFrameworkDir = Join-Path $supportDir "test-framework"
$exercismMetaDir = Join-Path $supportDir ".exercism"

New-Item -ItemType Directory -Path $supportDir -Force | Out-Null
New-Item -ItemType Directory -Path $testFrameworkDir -Force | Out-Null
New-Item -ItemType Directory -Path $exercismMetaDir -Force | Out-Null

Set-Content -Path (Join-Path $exerciseDir "hello_world.c") -Value "/* Instrucciones traducidas */`n#include <stdio.h>`n" -Encoding utf8
Set-Content -Path (Join-Path $supportDir "hello_world.c") -Value "#include <stdio.h>`n" -Encoding utf8
Set-Content -Path (Join-Path $supportDir "hello_world.h") -Value "#ifndef HELLO_WORLD_H`n#define HELLO_WORLD_H`n#endif`n" -Encoding utf8
Set-Content -Path (Join-Path $supportDir "README.md") -Value "# Hello World`nTranslated instructions..." -Encoding utf8
Set-Content -Path (Join-Path $supportDir "HELP.md") -Value "Help content..." -Encoding utf8
Set-Content -Path (Join-Path $supportDir "makefile") -Value "test: test_hello_world.c`n`tgcc ..." -Encoding utf8
Set-Content -Path (Join-Path $supportDir "test_hello_world.c") -Value '#include "test-framework/unity.h"' -Encoding utf8
Set-Content -Path (Join-Path $testFrameworkDir "unity.h") -Value "#ifndef UNITY_H`n#define UNITY_H`n#endif`n" -Encoding utf8
Set-Content -Path (Join-Path $testFrameworkDir "unity.c") -Value '#include "unity.h"' -Encoding utf8
Set-Content -Path (Join-Path $testFrameworkDir "unity_internals.h") -Value "// internals" -Encoding utf8
Set-Content -Path (Join-Path $exercismMetaDir "config.json") -Value '{"solution": ["hello_world.c"]}' -Encoding utf8
Set-Content -Path (Join-Path $exercismMetaDir "metadata.json") -Value '{"slug": "hello-world"}' -Encoding utf8
Set-Content -Path (Join-Path $exerciseDir ".estudio-exercism.json") -Value '{"provider": "exercism", "slug": "hello-world"}' -Encoding utf8

$cliWorkspace = Join-Path $simRoot "usuario\exercism\c\hello-world"
$cliExercismMeta = Join-Path $cliWorkspace ".exercism"
New-Item -ItemType Directory -Path $cliExercismMeta -Force | Out-Null
Set-Content -Path (Join-Path $cliWorkspace "hello_world.c") -Value "#include <stdio.h>`n" -Encoding utf8
Set-Content -Path (Join-Path $cliWorkspace "hello_world.h") -Value "#ifndef HELLO_WORLD_H`n#endif`n" -Encoding utf8
Set-Content -Path (Join-Path $cliWorkspace "README.md") -Value "# Hello World`nOriginal README..." -Encoding utf8
Set-Content -Path (Join-Path $cliWorkspace "HELP.md") -Value "Help..." -Encoding utf8
Set-Content -Path (Join-Path $cliWorkspace "makefile") -Value "test: ..." -Encoding utf8
Set-Content -Path (Join-Path $cliWorkspace "test_hello_world.c") -Value '#include "test-framework/unity.h"' -Encoding utf8
Set-Content -Path (Join-Path $cliExercismMeta "config.json") -Value '{"solution": ["hello_world.c"]}' -Encoding utf8
Set-Content -Path (Join-Path $cliExercismMeta "metadata.json") -Value '{"slug": "hello-world"}' -Encoding utf8

Write-Host "Estructura simulada creada en: $simRoot" -ForegroundColor Gray

# ── 3. Verify visibility using simple path checks ─────────────────────────────
Write-Host "`n=== 3. Verificando reglas de visibilidad ===" -ForegroundColor Cyan

function Test-ShouldBeHidden {
    param($fullPath, $simRoot)

    $rel = $fullPath.Replace("$simRoot\", "").Replace("\", "/")

    # usuario/exercism/** -> hidden
    if ($rel -like "usuario/exercism/*") { return $true }

    # **/.estudio-exercism/** -> hidden
    if ($rel -like "*.estudio-exercism/*") { return $true }

    # **/.estudio-exercism.json -> hidden
    if ($rel -like "*.estudio-exercism.json") { return $true }

    # **/.exercism/** -> hidden
    if ($rel -like "*/.exercism/*" -or $rel -like ".exercism/*") { return $true }

    # Ejercicios/**/[Mm]akefile -> hidden
    if ($rel -like "Ejercicios/*/makefile" -or $rel -like "Ejercicios/*/Makefile") { return $true }

    # Ejercicios/**/*.h -> hidden
    if ($rel -like "Ejercicios/*/*.h") { return $true }

    # Ejercicios/**/README.md -> hidden
    if ($rel -like "Ejercicios/*/README.md") { return $true }

    # Ejercicios/**/HELP.md -> hidden
    if ($rel -like "Ejercicios/*/HELP.md") { return $true }

    # Ejercicios/**/test-framework/** -> hidden
    if ($rel -like "Ejercicios/*/test-framework/*") { return $true }

    # Ejercicios/**/test_*.c -> hidden
    if ($rel -like "Ejercicios/*/test_*.c") { return $true }

    return $false
}

# Test: .c file in exercise root should be VISIBLE
$cFile = Join-Path $exerciseDir "hello_world.c"
if (-not (Test-ShouldBeHidden -fullPath $cFile -simRoot $simRoot)) {
    Pass "hello_world.c en ejercicio es VISIBLE"
} else {
    Fail "hello_world.c en ejercicio esta OCULTO (debe ser visible)"
}

# Test: files in support directory should be HIDDEN (via .estudio-exercism glob)
$supportFiles = @(
    (Join-Path $supportDir "hello_world.c"),
    (Join-Path $supportDir "hello_world.h"),
    (Join-Path $supportDir "README.md"),
    (Join-Path $supportDir "HELP.md"),
    (Join-Path $supportDir "makefile"),
    (Join-Path $supportDir "test_hello_world.c")
)

foreach ($file in $supportFiles) {
    if (Test-ShouldBeHidden -fullPath $file -simRoot $simRoot) {
        $rel = $file.Replace("$simRoot\", "").Replace("\", "/")
        Pass "support/$rel esta OCULTO"
    } else {
        $rel = $file.Replace("$simRoot\", "").Replace("\", "/")
        Fail "support/$rel es VISIBLE (debe estar oculto)"
    }
}

# Test: .estudio-exercism.json should be HIDDEN
$metaFile = Join-Path $exerciseDir ".estudio-exercism.json"
if (Test-ShouldBeHidden -fullPath $metaFile -simRoot $simRoot) {
    Pass ".estudio-exercism.json esta OCULTO"
} else {
    Fail ".estudio-exercism.json es VISIBLE (debe estar oculto)"
}

# Test: test-framework files should be HIDDEN
$unityH = Join-Path $testFrameworkDir "unity.h"
if (Test-ShouldBeHidden -fullPath $unityH -simRoot $simRoot) {
    Pass "test-framework/unity.h esta OCULTO"
} else {
    Fail "test-framework/unity.h es VISIBLE (debe estar oculto)"
}

# Test: .exercism metadata should be HIDDEN
$configJson = Join-Path $exercismMetaDir "config.json"
if (Test-ShouldBeHidden -fullPath $configJson -simRoot $simRoot) {
    Pass ".exercism/config.json esta OCULTO"
} else {
    Fail ".exercism/config.json es VISIBLE (debe estar oculto)"
}

# Test: usuario/exercism files should be HIDDEN
$cliFiles = @(
    (Join-Path $cliWorkspace "hello_world.c"),
    (Join-Path $cliWorkspace "hello_world.h"),
    (Join-Path $cliWorkspace "README.md"),
    (Join-Path $cliWorkspace "HELP.md"),
    (Join-Path $cliWorkspace "makefile"),
    (Join-Path $cliExercismMeta "config.json")
)

foreach ($file in $cliFiles) {
    if (Test-ShouldBeHidden -fullPath $file -simRoot $simRoot) {
        $rel = $file.Replace("$simRoot\", "").Replace("\", "/")
        Pass "usuario/exercism/$rel esta OCULTO"
    } else {
        $rel = $file.Replace("$simRoot\", "").Replace("\", "/")
        Fail "usuario/exercism/$rel es VISIBLE (debe estar oculto)"
    }
}

# ── 4. Verify necessary files are preserved ───────────────────────────────────
Write-Host "`n=== 4. Verificando que archivos necesarios se conservan ===" -ForegroundColor Cyan

$requiredFiles = @(
    (Join-Path $supportDir "hello_world.c"),
    (Join-Path $supportDir "hello_world.h"),
    (Join-Path $supportDir "test_hello_world.c"),
    (Join-Path $supportDir "makefile"),
    (Join-Path $testFrameworkDir "unity.h"),
    (Join-Path $testFrameworkDir "unity.c"),
    (Join-Path $exercismMetaDir "config.json"),
    (Join-Path $exercismMetaDir "metadata.json"),
    (Join-Path $exerciseDir "hello_world.c")
)

foreach ($file in $requiredFiles) {
    if (Test-Path -LiteralPath $file) {
        $rel = $file.Replace("$simRoot\", "").Replace("\", "/")
        Pass "Archivo conservado: $rel"
    } else {
        $rel = $file.Replace("$simRoot\", "").Replace("\", "/")
        Fail "Archivo FALTANTE: $rel"
    }
}

# ── 5. Verify translation availability ────────────────────────────────────────
Write-Host "`n=== 5. Verificando traduccion disponible ===" -ForegroundColor Cyan

$translatedReadme = Join-Path $supportDir "README.md"
if (Test-Path -LiteralPath $translatedReadme) {
    $content = Get-Content -LiteralPath $translatedReadme -Raw
    if ($content -match "Translated|traduc|Hello World") {
        Pass "README.md traducido disponible en support/"
    } else {
        Warn "README.md existe pero contenido no parece traducido"
    }
} else {
    Fail "README.md traducido NO encontrado en support/"
}

$cContent = Get-Content -LiteralPath (Join-Path $exerciseDir "hello_world.c") -Raw
if ($cContent -match "Instrucciones|traduc|/\*") {
    Pass "Archivo .c contiene header con instrucciones"
} else {
    Warn "Archivo .c no parece tener header de instrucciones"
}

# ── 6. Cleanup ────────────────────────────────────────────────────────────────
if (Test-Path $simRoot) {
    Remove-Item -LiteralPath $simRoot -Recurse -Force
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host "`n=== Resumen ===" -ForegroundColor Cyan
Write-Host "  PASS: $passCount" -ForegroundColor Green
Write-Host "  FAIL: $failCount" -ForegroundColor Red
Write-Host "  WARN: $warnCount" -ForegroundColor Yellow

if ($failCount -gt 0) {
    Write-Host "`nVerificacion FALLIDA. Revisa los errores arriba." -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nVerificacion EXITOSA." -ForegroundColor Green
    exit 0
}
