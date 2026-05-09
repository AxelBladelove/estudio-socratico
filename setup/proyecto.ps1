function Assert-ProjectRoot {
    param([string]$RepoRoot)

    $required = @(
        "AGENTS.md",
        "soporte\scripts\compilar_y_grabar.bat",
        "soporte\scripts\build.cmd",
        ".vscode\tasks.json",
        ".agent\skills\revisar\SKILL.md",
        ".agent\skills\ver\SKILL.md",
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

    foreach ($folder in @("logs", "Ejercicios", "usuarios")) {
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
    $cp437Header = Join-Path $includeDir "estudio_stdio_cp437.h"
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

function ConvertTo-ProjectUserSlug {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "usuario"
    }

    $slug = $Value.Trim().ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $slug = $slug.Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) {
        return "usuario"
    }

    return $slug
}

function Get-ProjectGitConfigValue {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitPath,
        [string]$Name
    )

    if (-not $GitPath) {
        return $null
    }

    if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
        return $null
    }

    Push-Location $RepoRoot
    try {
        $value = (& $GitPath config --local --get $Name 2>$null | Select-Object -First 1)
        if (Test-UsableGitIdentityValue -Value $value) {
            return $value.Trim()
        }
    } catch {
    } finally {
        Pop-Location
    }

    return $null
}

function Get-ProjectCurrentUserSlug {
    param([string]$RepoRoot)

    $path = Join-Path $RepoRoot ".estudio_usuario"
    if (-not (Test-Path $path)) {
        return $null
    }

    $value = (Get-Content -LiteralPath $path -ErrorAction SilentlyContinue | Select-Object -First 1)
    if (Test-UsableGitIdentityValue -Value $value) {
        return (ConvertTo-ProjectUserSlug -Value $value)
    }

    return $null
}

function Read-SetupValue {
    param(
        [string]$Prompt,
        [AllowNull()][string]$DefaultValue,
        [switch]$Required
    )

    do {
        $suffix = ""
        if (Test-UsableGitIdentityValue -Value $DefaultValue) {
            $suffix = " [$DefaultValue]"
        }

        $value = Read-Host "$Prompt$suffix"
        if ([string]::IsNullOrWhiteSpace($value)) {
            $value = $DefaultValue
        }

        if ((-not $Required) -or (Test-UsableGitIdentityValue -Value $value)) {
            return $value
        }

        Write-SetupWarning "Ese valor no parece util para esta configuracion."
    } while ($true)
}

function Show-SetupInteractiveIntro {
    param([string]$RepoRoot)

    Write-SetupTitle "Estudio Socratico - Asistente interactivo"
    Write-SetupLine "Este asistente preparara el repo para estudiar C con VS Code, GCC, Exercism y las skills socraticas." Cyan
    Write-SetupLine "No tienes que elegir componentes: se instala y valida todo lo necesario por defecto." Cyan
    Write-SetupLine ""
    Write-SetupLine "La TUI solo se detendra si necesita datos tuyos:" DarkGray
    Write-SetupLine "  1. Identidad local del estudiante y Git." DarkGray
    Write-SetupLine "  2. Token global de Exercism si falta." DarkGray
    Write-SetupLine "  3. GEMINI_API_KEY si quieres traducciones automaticas." DarkGray
    Write-SetupLine "  4. Confirmaciones puntuales como abrir enlaces o recargar PATH." DarkGray
    Write-SetupLine ""
    Write-SetupLine ("Repo: {0}" -f $RepoRoot) DarkGray
    Write-SetupLine ""
}

function Wait-SetupInteractiveStart {
    Read-Host "Presiona Enter para empezar la instalacion completa"
}

function Resolve-ProjectOnboarding {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitPath,
        [AllowNull()][string]$UsuarioSlug,
        [AllowNull()][string]$GitHubUsuario,
        [AllowNull()][string]$GitNombre,
        [AllowNull()][string]$GitCorreo,
        [switch]$SoloVerificar,
        [switch]$SinOnboarding
    )

    $configuredGitHubUser = Get-ProjectGitConfigValue -RepoRoot $RepoRoot -GitPath $GitPath -Name "github.user"
    $configuredGitName = Get-ProjectGitConfigValue -RepoRoot $RepoRoot -GitPath $GitPath -Name "user.name"
    $configuredGitEmail = Get-ProjectGitConfigValue -RepoRoot $RepoRoot -GitPath $GitPath -Name "user.email"
    $configuredSlug = Get-ProjectCurrentUserSlug -RepoRoot $RepoRoot

    if (-not (Test-UsableGitIdentityValue -Value $UsuarioSlug)) {
        $UsuarioSlug = $configuredSlug
    }
    if (-not (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        $GitHubUsuario = $configuredGitHubUser
    }
    if (-not (Test-UsableGitIdentityValue -Value $GitNombre)) {
        $GitNombre = $configuredGitName
    }
    if (-not (Test-UsableGitIdentityValue -Value $GitCorreo)) {
        $GitCorreo = $configuredGitEmail
    }

    $defaultSlugSeed = $UsuarioSlug
    if (-not (Test-UsableGitIdentityValue -Value $defaultSlugSeed)) {
        $defaultSlugSeed = $GitHubUsuario
    }
    if (-not (Test-UsableGitIdentityValue -Value $defaultSlugSeed)) {
        $defaultSlugSeed = $env:USERNAME
    }
    $defaultSlug = ConvertTo-ProjectUserSlug -Value $defaultSlugSeed

    $hasCompleteInput = (
        (Test-UsableGitIdentityValue -Value $UsuarioSlug) -and
        (Test-UsableGitIdentityValue -Value $GitHubUsuario) -and
        (Test-UsableGitIdentityValue -Value $GitNombre) -and
        (Test-UsableGitIdentityValue -Value $GitCorreo)
    )

    if ((-not $SoloVerificar) -and (-not $SinOnboarding)) {
        Show-SetupInteractiveIntro -RepoRoot $RepoRoot
        Wait-SetupInteractiveStart

        Write-SetupStep "Configurando tu usuario de estudio"
        Write-SetupInfo "Estos datos solo se guardan en este clon local."

        $UsuarioSlug = Read-SetupValue -Prompt "Nombre corto para tu carpeta y rama (ej. axel)" -DefaultValue $defaultSlug -Required
        $UsuarioSlug = ConvertTo-ProjectUserSlug -Value $UsuarioSlug

        $GitHubUsuario = Read-SetupValue -Prompt "Usuario de GitHub para commits y ramas" -DefaultValue $GitHubUsuario -Required
        if (-not (Test-UsableGitIdentityValue -Value $GitNombre)) {
            $GitNombre = $GitHubUsuario
        }
        $GitNombre = Read-SetupValue -Prompt "Nombre que aparecera en los commits" -DefaultValue $GitNombre -Required

        if (-not (Test-UsableGitIdentityValue -Value $GitCorreo)) {
            $GitCorreo = ("{0}@users.noreply.github.com" -f $GitHubUsuario)
        }
        Open-SetupUrlIfWanted -Url "https://github.com/settings/emails" -Reason "Si quieres verificar o copiar tu correo de GitHub, abre esta pagina. Si no, usa el noreply sugerido."
        $GitCorreo = Read-SetupValue -Prompt "Correo para commits (usa uno verificado o noreply de GitHub)" -DefaultValue $GitCorreo -Required
    } elseif ((-not $SoloVerificar) -and (-not $hasCompleteInput)) {
        Write-SetupInfo "SinOnboarding activo; se usaran valores por defecto para la identidad local."
    }

    if (-not (Test-UsableGitIdentityValue -Value $UsuarioSlug)) {
        $UsuarioSlug = $defaultSlug
    }
    $UsuarioSlug = ConvertTo-ProjectUserSlug -Value $UsuarioSlug

    if (-not (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        $GitHubUsuario = $UsuarioSlug
    }
    if (-not (Test-UsableGitIdentityValue -Value $GitNombre)) {
        $GitNombre = $GitHubUsuario
    }
    if (-not (Test-UsableGitIdentityValue -Value $GitCorreo)) {
        $GitCorreo = ("{0}@users.noreply.github.com" -f $GitHubUsuario)
    }

    return @{
        UsuarioSlug = $UsuarioSlug
        GitHubUsuario = $GitHubUsuario.Trim()
        GitNombre = $GitNombre.Trim()
        GitCorreo = $GitCorreo.Trim()
    }
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

function Test-ProjectGitRefExists {
    param(
        [string]$RepoRoot,
        [string]$GitPath,
        [string]$RefName
    )

    Push-Location $RepoRoot
    try {
        & $GitPath show-ref --verify --quiet $RefName
        return ($LASTEXITCODE -eq 0)
    } finally {
        Pop-Location
    }
}

function Initialize-ProjectUser {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitPath,
        [string]$UsuarioSlug,
        [switch]$SoloVerificar,
        [switch]$SinRamaUsuario
    )

    $usuarioDir = Join-Path $RepoRoot ("usuarios\" + $UsuarioSlug)
    $logsDir = Join-Path $usuarioDir "logs"
    $erroresPath = Join-Path $usuarioDir "errores.md"
    $usuarioConfig = Join-Path $RepoRoot ".estudio_usuario"

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Escribiria .estudio_usuario con '$UsuarioSlug'."
        Write-SetupInfo "[SoloVerificar] Prepararia usuarios\$UsuarioSlug\errores.md vacio si no existe."
    } else {
        if (-not (Test-Path $usuarioDir)) {
            New-Item -ItemType Directory -Path $usuarioDir -Force | Out-Null
        }
        if (-not (Test-Path $logsDir)) {
            New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
        }
        Set-Content -LiteralPath $usuarioConfig -Value $UsuarioSlug -NoNewline -Encoding ascii
        if (-not (Test-Path $erroresPath)) {
            New-Item -ItemType File -Path $erroresPath -Force | Out-Null
        }
        Write-SetupSuccess "Usuario local activo: $UsuarioSlug."
    }

    if ($SinRamaUsuario) {
        Write-SetupWarning "SinRamaUsuario activo; no se cambiara de rama."
        return
    }

    if (-not $GitPath) {
        Write-SetupWarning "Git no esta confirmado; no se preparara la rama personal."
        return
    }

    if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
        Write-SetupWarning "El repositorio Git aun no existe; no se preparara la rama personal."
        return
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Prepararia o cambiaria a la rama '$UsuarioSlug'."
        return
    }

    Push-Location $RepoRoot
    try {
        $currentBranch = (& $GitPath branch --show-current 2>$null | Select-Object -First 1)
    } finally {
        Pop-Location
    }

    if ($currentBranch -eq $UsuarioSlug) {
        Write-SetupSuccess "Ya estas en la rama personal '$UsuarioSlug'."
        return
    }

    try {
        if (Test-ProjectGitRefExists -RepoRoot $RepoRoot -GitPath $GitPath -RefName ("refs/heads/" + $UsuarioSlug)) {
            Invoke-SetupCommand -FilePath $GitPath -Arguments @("switch", $UsuarioSlug) -Description "Cambiando a la rama personal $UsuarioSlug..." -SoloVerificar:$false
        } elseif (Test-ProjectGitRefExists -RepoRoot $RepoRoot -GitPath $GitPath -RefName ("refs/remotes/origin/" + $UsuarioSlug)) {
            Invoke-SetupCommand -FilePath $GitPath -Arguments @("switch", "-c", $UsuarioSlug, "--track", ("origin/" + $UsuarioSlug)) -Description "Conectando rama local $UsuarioSlug con origin/$UsuarioSlug..." -SoloVerificar:$false
        } else {
            Invoke-SetupCommand -FilePath $GitPath -Arguments @("switch", "-c", $UsuarioSlug) -Description "Creando rama personal $UsuarioSlug..." -SoloVerificar:$false
        }
        Write-SetupSuccess "Rama personal lista: $UsuarioSlug."
    } catch {
        Write-SetupWarning "No pude cambiar a la rama personal automaticamente: $($_.Exception.Message)"
        Write-SetupInfo "Cuando tengas limpio tu trabajo local, ejecuta: git switch -c $UsuarioSlug"
    }
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

    foreach ($name in @("Git", "PowerShell", "Node.js", "Bun", "Python", "GitHub CLI", "Exercism CLI", "MSYS2", "VS Code")) {
        if ($Tools.ContainsKey($name) -and $Tools[$name]) {
            Write-SetupLine ("  {0}: OK" -f $name) Green
        } else {
            Write-SetupLine ("  {0}: no confirmado" -f $name) Yellow
        }
    }
}
