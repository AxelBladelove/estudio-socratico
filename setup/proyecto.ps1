function Assert-ProjectRoot {
    param([string]$RepoRoot)

    $required = @(
        "AGENTS.md",
        "soporte\scripts\compilar_y_grabar.bat",
        "soporte\scripts\build.cmd",
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

function Ensure-AgentRuntimeTools {
    param(
        [string]$RepoRoot,
        [switch]$SoloVerificar
    )

    $sourcePath = Join-Path $RepoRoot "soporte\consola\sys_dump_console.c"
    $runtimePath = Join-Path $RepoRoot "soporte\runtime"
    $binaryPath = Join-Path $runtimePath "sys_dump_console.exe"
    $gccPath = "C:\msys64\mingw64\bin\gcc.exe"

    if (-not (Test-Path $sourcePath)) {
        Write-SetupWarning "No existe soporte\consola\sys_dump_console.c; se omitira el helper de consola."
        return
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Compilaria soporte\runtime\sys_dump_console.exe con GCC."
        return
    }

    if (-not (Test-Path $runtimePath)) {
        New-Item -ItemType Directory -Path $runtimePath -Force | Out-Null
    }

    if (-not (Test-Path $gccPath)) {
        throw "No se encontro gcc en $gccPath para compilar soporte\consola\sys_dump_console.exe."
    }

    $needsBuild = $true
    if ((Test-Path $binaryPath) -and ((Get-Item $binaryPath).LastWriteTimeUtc -ge (Get-Item $sourcePath).LastWriteTimeUtc)) {
        $needsBuild = $false
    }

    if (-not $needsBuild) {
        Write-SetupSuccess "soporte\runtime\sys_dump_console.exe ya esta actualizado."
        return
    }

    Invoke-SetupCommand `
        -FilePath $gccPath `
        -Arguments @($sourcePath, "-o", $binaryPath, "-std=c99", "-Wall", "-Wextra") `
        -Description "Compilando soporte\runtime\sys_dump_console.exe..." `
        -SoloVerificar:$false

    Write-SetupSuccess "soporte\runtime\sys_dump_console.exe listo."
}

function Resolve-ProjectGitIdentity {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitPath,
        [AllowNull()][string]$GitHubUsuario,
        [AllowNull()][string]$GitNombre,
        [AllowNull()][string]$GitCorreo
    )

    $configuredGitHubUser = $null
    $configuredGitName = $null
    $configuredGitEmail = $null

    if ($GitPath -and (Test-Path (Join-Path $RepoRoot ".git"))) {
        Push-Location $RepoRoot
        try {
            $configuredGitHubUser = (& $GitPath config --local --get github.user 2>$null | Select-Object -First 1)
            $configuredGitName = (& $GitPath config --local --get user.name 2>$null | Select-Object -First 1)
            $configuredGitEmail = (& $GitPath config --local --get user.email 2>$null | Select-Object -First 1)
        } finally {
            Pop-Location
        }
    }

    if ([string]::IsNullOrWhiteSpace($GitHubUsuario)) {
        $GitHubUsuario = $configuredGitHubUser
    }

    if ([string]::IsNullOrWhiteSpace($GitHubUsuario)) {
        throw "No se encontro github.user. Configura tu usuario de GitHub con 'git config --local github.user <tu_usuario>' o ejecuta el setup pasando -GitHubUsuario <tu_usuario>."
    }

    if ([string]::IsNullOrWhiteSpace($GitNombre) -or ($GitNombre -eq "Estudiante")) {
        if (-not [string]::IsNullOrWhiteSpace($configuredGitName) -and ($configuredGitName -ne "Estudiante")) {
            $GitNombre = $configuredGitName
        } else {
            $GitNombre = $GitHubUsuario
        }
    }

    if ([string]::IsNullOrWhiteSpace($GitCorreo) -or ($GitCorreo -eq "estudiante@estudio.local")) {
        if (-not [string]::IsNullOrWhiteSpace($configuredGitEmail) -and ($configuredGitEmail -ne "estudiante@estudio.local")) {
            $GitCorreo = $configuredGitEmail
        } else {
            $GitCorreo = ("{0}@users.noreply.github.com" -f $GitHubUsuario.ToLowerInvariant())
        }
    }

    return @{
        GitHubUsuario = $GitHubUsuario
        GitNombre = $GitNombre
        GitCorreo = $GitCorreo
    }
}

function Configure-ProjectGit {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitPath,
        [string]$GitHubUsuario,
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

    Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "github.user", $GitHubUsuario) -Description "Configurando github.user local..." -SoloVerificar:$SoloVerificar
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
