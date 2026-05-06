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

    $runtimePath = Join-Path $RepoRoot "soporte\runtime"
    $launcherSource = Join-Path $RepoRoot "soporte\consola\output_launcher.c"
    $launcherBinary = Join-Path $runtimePath "_output.exe"
    $conioSource = Join-Path $RepoRoot "soporte\consola\conio.c"
    $conioObject = Join-Path $runtimePath "conio_support.o"
    $includeDir = Join-Path $RepoRoot "include"
    $conioHeader = Join-Path $includeDir "conio.h"
    $cp437Header = Join-Path $RepoRoot "soporte\consola\console_cp437.h"
    $gccPath = "C:\msys64\mingw64\bin\gcc.exe"

    foreach ($required in @($launcherSource, $conioSource, $conioHeader, $cp437Header)) {
        if (-not (Test-Path $required)) {
            throw "Falta archivo requerido para compilar runtime local: $required"
        }
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Compilaria soporte\runtime\_output.exe y soporte\runtime\conio_support.o con GCC."
        return
    }

    if (-not (Test-Path $runtimePath)) {
        New-Item -ItemType Directory -Path $runtimePath -Force | Out-Null
    }

    if (-not (Test-Path $gccPath)) {
        throw "No se encontro gcc en $gccPath para compilar el runtime local."
    }

    $needsLauncherBuild = $true
    if ((Test-Path $launcherBinary) -and ((Get-Item $launcherBinary).LastWriteTimeUtc -ge (Get-Item $launcherSource).LastWriteTimeUtc)) {
        $needsLauncherBuild = $false
    }

    $needsConioBuild = $true
    if (Test-Path $conioObject) {
        $objectTime = (Get-Item $conioObject).LastWriteTimeUtc
        $latestDependencyTime = @($conioSource, $conioHeader, $cp437Header) |
            ForEach-Object { (Get-Item $_).LastWriteTimeUtc } |
            Sort-Object -Descending |
            Select-Object -First 1
        if ($objectTime -ge $latestDependencyTime) {
            $needsConioBuild = $false
        }
    }

    if ($needsLauncherBuild) {
        Invoke-SetupCommand `
            -FilePath $gccPath `
            -Arguments @($launcherSource, "-o", $launcherBinary, "-std=c99", "-Wall", "-Wextra") `
            -Description "Compilando soporte\runtime\_output.exe..." `
            -SoloVerificar:$false

        Write-SetupSuccess "soporte\runtime\_output.exe listo."
    } else {
        Write-SetupSuccess "soporte\runtime\_output.exe ya esta actualizado."
    }

    if ($needsConioBuild) {
        Invoke-SetupCommand `
            -FilePath $gccPath `
            -Arguments @($conioSource, "-I", $includeDir, "-c", "-o", $conioObject, "-std=c99", "-Wall", "-Wextra") `
            -Description "Compilando soporte\runtime\conio_support.o..." `
            -SoloVerificar:$false

        Write-SetupSuccess "soporte\runtime\conio_support.o listo."
    } else {
        Write-SetupSuccess "soporte\runtime\conio_support.o ya esta actualizado."
    }
}

function Test-UsableGitIdentityValue {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }

    $trimmed = $Value.Trim()
    $badValues = @(
        "Estudiante",
        "estudiante",
        "estudiante@estudio.local",
        "2>",
        "2^>",
        "nul",
        "null"
    )

    if ($badValues -contains $trimmed) {
        return $false
    }

    if ($trimmed -match "\^?>|^2\^?>$") {
        return $false
    }

    return $true
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

    if (-not (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        $GitHubUsuario = $configuredGitHubUser
    }

    if (-not (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        throw "No se encontro github.user. Configura tu usuario de GitHub con 'git config --local github.user <tu_usuario>' o ejecuta el setup pasando -GitHubUsuario <tu_usuario>."
    }

    if (-not (Test-UsableGitIdentityValue -Value $GitNombre)) {
        if (Test-UsableGitIdentityValue -Value $configuredGitName) {
            $GitNombre = $configuredGitName
        } else {
            $GitNombre = $GitHubUsuario
        }
    }

    if (-not (Test-UsableGitIdentityValue -Value $GitCorreo)) {
        if (Test-UsableGitIdentityValue -Value $configuredGitEmail) {
            $GitCorreo = $configuredGitEmail
        } else {
            $GitCorreo = ("{0}@users.noreply.github.com" -f $GitHubUsuario)
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
