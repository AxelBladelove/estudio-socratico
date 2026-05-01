function Assert-ProjectRoot {
    param([string]$RepoRoot)

    $required = @(
        "AGENTS.md",
        "compilar_y_grabar.bat",
        ".vscode\tasks.json",
        ".agent\skills\revisar\SKILL.md",
        ".agent\skills\sintetizar\SKILL.md"
    )

    foreach ($item in $required) {
        $path = Join-Path $RepoRoot $item
        if (-not (Test-Path $path)) {
            throw "Falta archivo requerido del proyecto: $item"
        }
    }

    Write-SetupSuccess "Raiz del proyecto verificada."
}

function Ensure-ProjectFolders {
    param(
        [string]$RepoRoot,
        [switch]$SoloVerificar
    )

    foreach ($folder in @("logs", "Ejercicios")) {
        New-SetupDirectory -Path (Join-Path $RepoRoot $folder) -SoloVerificar:$SoloVerificar
    }
}

function Configure-ProjectGit {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitPath,
        [string]$GitNombre,
        [string]$GitCorreo,
        [switch]$SoloVerificar
    )

    if (-not $GitPath) {
        Write-SetupWarning "Git no esta confirmado; no se configurara el repo."
        return
    }

    if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
        Invoke-SetupCommand -FilePath $GitPath -Arguments @("init") -Description "Inicializando repositorio Git..." -SoloVerificar:$SoloVerificar
    } else {
        Write-SetupSuccess "Repositorio Git ya existe."
    }

    Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "user.name", $GitNombre) -Description "Configurando user.name local..." -SoloVerificar:$SoloVerificar
    Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "user.email", $GitCorreo) -Description "Configurando user.email local..." -SoloVerificar:$SoloVerificar
}

function Test-WorkspaceJson {
    param([string]$RepoRoot)

    foreach ($file in @(".vscode\tasks.json", ".vscode\settings.json", ".vscode\extensions.json")) {
        $path = Join-Path $RepoRoot $file
        if (-not (Test-Path $path)) {
            continue
        }

        try {
            Get-Content $path -Raw | ConvertFrom-Json | Out-Null
            Write-SetupSuccess "$file es JSON valido."
        } catch {
            throw "$file no es JSON valido: $($_.Exception.Message)"
        }
    }
}

function Write-SetupReport {
    param([hashtable]$Tools)

    foreach ($name in @("Git", "PowerShell", "Node.js", "Bun", "Python", "GitHub CLI", "MSYS2", "VS Code")) {
        if ($Tools.ContainsKey($name) -and $Tools[$name]) {
            Write-SetupLine ("  {0}: OK" -f $name) Green
        } else {
            Write-SetupLine ("  {0}: no confirmado" -f $name) Yellow
        }
    }
}
