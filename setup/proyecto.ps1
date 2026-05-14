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

    foreach ($folder in @("logs", "Ejercicios", "usuario")) {
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

function Get-ProjectUserRegistryPath {
    param([string]$RepoRoot)
    return (Join-Path $RepoRoot "usuario\registro.json")
}

function Get-LegacyProjectUserRegistryPath {
    param([string]$RepoRoot)
    return (Join-Path $RepoRoot "usuarios\registro.json")
}

function Resolve-ProjectUserDirectory {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$UsuarioSlug,
        [switch]$Create,
        [switch]$SoloVerificar
    )

    $canonical = Join-Path $RepoRoot "usuario"
    if (Test-Path -LiteralPath $canonical) {
        return $canonical
    }

    if (Test-UsableGitIdentityValue -Value $UsuarioSlug) {
        $legacy = Join-Path $RepoRoot ("usuarios\" + (ConvertTo-ProjectUserSlug -Value $UsuarioSlug))
        if (Test-Path -LiteralPath $legacy) {
            if ($Create) {
                if ($SoloVerificar) {
                    Write-SetupInfo "[SoloVerificar] Migraria $legacy a usuario."
                } else {
                    Move-Item -LiteralPath $legacy -Destination $canonical
                    Write-SetupSuccess "Carpeta de usuario migrada: $legacy -> usuario."
                }
                return $canonical
            }
            return $legacy
        }
    }

    if ($Create -and -not $SoloVerificar) {
        New-Item -ItemType Directory -Path $canonical -Force | Out-Null
    }
    return $canonical
}

function Read-ProjectUserRegistry {
    param([string]$RepoRoot)

    $path = Get-ProjectUserRegistryPath -RepoRoot $RepoRoot
    $legacyPath = Get-LegacyProjectUserRegistryPath -RepoRoot $RepoRoot
    if ((-not (Test-Path -LiteralPath $path)) -and (Test-Path -LiteralPath $legacyPath)) {
        $path = $legacyPath
    }
    if (-not (Test-Path -LiteralPath $path)) {
        return [pscustomobject]@{
            version = 1
            users = @()
        }
    }

    try {
        $registry = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($null -eq $registry.users) {
            $registry | Add-Member -NotePropertyName users -NotePropertyValue @() -Force
        }
        return $registry
    } catch {
        Write-SetupWarning "No pude leer el registro de usuario; se ignorara este registro."
        return [pscustomobject]@{
            version = 1
            users = @()
        }
    }
}

function Find-ProjectRegisteredUser {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitHubUsuario
    )

    if (-not (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        return $null
    }

    $registry = Read-ProjectUserRegistry -RepoRoot $RepoRoot
    foreach ($user in @($registry.users)) {
        if ("$($user.githubLogin)".Trim().ToLowerInvariant() -eq $GitHubUsuario.Trim().ToLowerInvariant()) {
            return $user
        }
    }

    return $null
}

function Set-ProjectUserRegistryEntry {
    param(
        [string]$RepoRoot,
        [string]$GitHubUsuario,
        [string]$UsuarioSlug,
        [switch]$SoloVerificar
    )

    if (-not (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        return
    }
    if (-not (Test-UsableGitIdentityValue -Value $UsuarioSlug)) {
        return
    }

    $UsuarioSlug = ConvertTo-ProjectUserSlug -Value $UsuarioSlug
    $path = Get-ProjectUserRegistryPath -RepoRoot $RepoRoot
    if ($SoloVerificar) {
        Write-SetupInfo ("[SoloVerificar] Actualizaria usuario\registro.json: {0} -> {1}." -f $GitHubUsuario, $UsuarioSlug)
        return
    }

    $registry = Read-ProjectUserRegistry -RepoRoot $RepoRoot
    $users = @(@($registry.users) | Where-Object {
        "$($_.githubLogin)".Trim().ToLowerInvariant() -ne $GitHubUsuario.Trim().ToLowerInvariant()
    })
    $newEntry = [pscustomobject]@{
        githubLogin = $GitHubUsuario.Trim()
        branch = $UsuarioSlug
        alias = $UsuarioSlug
        updatedAt = (Get-Date).ToString("o")
    }
    $users = @($users) + @($newEntry)

    $updated = [pscustomobject]@{
        version = 1
        users = @($users | Sort-Object githubLogin)
    }

    $dir = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $updated | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8
    Write-SetupSuccess ("Registro GitHub/rama actualizado: {0} -> {1}." -f $GitHubUsuario, $UsuarioSlug)
}

function Test-GhAuthenticated {
    param([AllowNull()][string]$GhPath)

    if (-not $GhPath) {
        return $false
    }

    try {
        & $GhPath auth status 1>$null 2>$null
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    }
}

function Get-GhAuthenticatedProfile {
    param([AllowNull()][string]$GhPath)

    if (-not $GhPath) {
        return $null
    }

    if (-not (Test-GhAuthenticated -GhPath $GhPath)) {
        return $null
    }

    try {
        $json = & $GhPath api user 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
            return $null
        }
        $profile = $json | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($profile.login)) {
            return $null
        }

        $name = $profile.name
        if ([string]::IsNullOrWhiteSpace($name)) {
            $name = $profile.login
        }

        $email = $profile.email
        if ([string]::IsNullOrWhiteSpace($email)) {
            if ($profile.id) {
                $email = ("{0}+{1}@users.noreply.github.com" -f $profile.id, $profile.login)
            } else {
                $email = ("{0}@users.noreply.github.com" -f $profile.login)
            }
        }

        return [pscustomobject]@{
            login = $profile.login
            name = $name
            email = $email
        }
    } catch {
        return $null
    }
}

function Ensure-GhAuthenticatedProfile {
    param(
        [AllowNull()][string]$GhPath,
        [switch]$SoloVerificar,
        [switch]$SinOnboarding,
        [switch]$ForceWebValidation
    )

    if (-not $GhPath) {
        Write-SetupWarning "GitHub CLI no esta confirmado; se usara la configuracion Git local si existe."
        return $null
    }

    $profile = Get-GhAuthenticatedProfile -GhPath $GhPath

    if ($SoloVerificar) {
        if ($ForceWebValidation) {
            if ($profile) {
                Write-SetupInfo ("[SoloVerificar] Revalidaria GitHub CLI en el navegador para {0} o permitiria cambiar de cuenta." -f $profile.login)
            } else {
                Write-SetupInfo "[SoloVerificar] Abriria el flujo web de GitHub CLI para iniciar sesion."
            }
        } elseif ($profile) {
            Write-SetupSuccess ("GitHub autenticado como {0}." -f $profile.login)
        } else {
            Write-SetupInfo "[SoloVerificar] Validaria GitHub CLI con gh auth status."
        }

        return $profile
    }

    if ($ForceWebValidation) {
        Write-SetupStep "Conectando GitHub"

        if ($profile) {
            Write-SetupSuccess ("GitHub CLI responde como {0}." -f $profile.login)
            if ($SinOnboarding) {
                return $profile
            }

            $authChoice = Read-SetupMenu `
                -Title "Cuenta GitHub detectada: $($profile.login)" `
                -Options @(
                    [pscustomobject]@{
                        Label = "Revalidar esta cuenta"
                        Description = "Abre GitHub en el navegador y conserva $($profile.login)."
                        Value = "refresh"
                    },
                    [pscustomobject]@{
                        Label = "Cambiar de cuenta"
                        Description = "Cierra la sesion local de gh y autentica otra cuenta."
                        Value = "change"
                    }
                )

            if ($authChoice -ne "change") {
                Invoke-SetupInteractiveCommand `
                    -FilePath $GhPath `
                    -Arguments @("auth", "refresh", "--hostname", "github.com", "--scopes", "repo,read:user,user:email") `
                    -Description ("Revalidando GitHub CLI en el navegador para {0}..." -f $profile.login) `
                    -SoloVerificar:$false `
                    -AllowFailure `
                    -TimeoutSeconds 300 | Out-Null

                $profile = Get-GhAuthenticatedProfile -GhPath $GhPath
                if ($profile) {
                    Write-SetupSuccess ("GitHub autenticado como {0}." -f $profile.login)
                    return $profile
                }

                Write-SetupWarning "No pude confirmar la revalidacion con gh auth refresh; intentare un login web completo."
            } else {
                Invoke-SetupInteractiveCommand `
                    -FilePath $GhPath `
                    -Arguments @("auth", "logout", "--hostname", "github.com", "--user", $profile.login) `
                    -Description "Cerrando la sesion GitHub CLI actual..." `
                    -SoloVerificar:$false `
                    -AllowFailure `
                    -TimeoutSeconds 60 | Out-Null
            }
        }

        Invoke-SetupInteractiveCommand `
            -FilePath $GhPath `
            -Arguments @("auth", "login", "--web", "--hostname", "github.com", "--scopes", "repo,read:user,user:email") `
            -Description "Abriendo autenticacion web de GitHub CLI..." `
            -SoloVerificar:$false `
            -AllowFailure `
            -TimeoutSeconds 300 | Out-Null

        $profile = Get-GhAuthenticatedProfile -GhPath $GhPath
        if (-not $profile) {
            throw "No pude confirmar la sesion de GitHub CLI despues de la autenticacion web."
        }

        Write-SetupSuccess ("GitHub autenticado como {0}." -f $profile.login)
        return $profile
    }

    if ($profile) {
        Write-SetupSuccess ("GitHub autenticado como {0}." -f $profile.login)
        return $profile
    }

    if ($SinOnboarding) {
        Write-SetupWarning "GitHub CLI no tiene sesion activa y SinOnboarding esta activo."
        return $null
    }

    Write-SetupStep "Conectando GitHub"
    Invoke-SetupInteractiveCommand `
        -FilePath $GhPath `
        -Arguments @("auth", "login", "--web", "--hostname", "github.com", "--scopes", "repo,read:user,user:email") `
        -Description "Abriendo autenticacion web de GitHub CLI..." `
        -SoloVerificar:$false `
        -TimeoutSeconds 300

    $profile = Get-GhAuthenticatedProfile -GhPath $GhPath
    if (-not $profile) {
        throw "No pude confirmar la sesion de GitHub CLI despues de gh auth login."
    }

    Write-SetupSuccess ("GitHub autenticado como {0}." -f $profile.login)
    return $profile
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
        [AllowNull()][string]$GhPath,
        [AllowNull()][string]$UsuarioSlug,
        [AllowNull()][string]$GitHubUsuario,
        [AllowNull()][string]$GitNombre,
        [AllowNull()][string]$GitCorreo,
        [switch]$SoloVerificar,
        [switch]$SinOnboarding,
        [switch]$Actualizar,
        [switch]$Reconfigurar
    )

    $configuredGitHubUser = Get-ProjectGitConfigValue -RepoRoot $RepoRoot -GitPath $GitPath -Name "github.user"
    $configuredGitName = Get-ProjectGitConfigValue -RepoRoot $RepoRoot -GitPath $GitPath -Name "user.name"
    $configuredGitEmail = Get-ProjectGitConfigValue -RepoRoot $RepoRoot -GitPath $GitPath -Name "user.email"
    $configuredAlias = Get-ProjectGitConfigValue -RepoRoot $RepoRoot -GitPath $GitPath -Name "estudio.usuario.alias"
    $configuredSlug = Get-ProjectCurrentUserSlug -RepoRoot $RepoRoot
    if (-not (Test-UsableGitIdentityValue -Value $configuredSlug)) {
        $configuredSlug = $configuredAlias
    }

    $ghProfile = Ensure-GhAuthenticatedProfile `
        -GhPath $GhPath `
        -SoloVerificar:$SoloVerificar `
        -SinOnboarding:$SinOnboarding `
        -ForceWebValidation:($Actualizar -or $Reconfigurar)

    if ($ghProfile -and (Test-UsableGitIdentityValue -Value $ghProfile.login)) {
        $GitHubUsuario = $ghProfile.login
    }
    if ($ghProfile -and (Test-UsableGitIdentityValue -Value $ghProfile.email)) {
        $GitCorreo = $ghProfile.email
    }

    $registeredUser = $null
    $registeredSlug = $null
    if (Test-UsableGitIdentityValue -Value $GitHubUsuario) {
        $registeredUser = Find-ProjectRegisteredUser -RepoRoot $RepoRoot -GitHubUsuario $GitHubUsuario
        if ($registeredUser) {
            if (Test-UsableGitIdentityValue -Value $registeredUser.branch) {
                $registeredSlug = ConvertTo-ProjectUserSlug -Value $registeredUser.branch
            } elseif (Test-UsableGitIdentityValue -Value $registeredUser.alias) {
                $registeredSlug = ConvertTo-ProjectUserSlug -Value $registeredUser.alias
            }
        }
    }

    if (-not (Test-UsableGitIdentityValue -Value $UsuarioSlug)) {
        if (Test-UsableGitIdentityValue -Value $configuredSlug) {
            $UsuarioSlug = $configuredSlug
        } elseif (Test-UsableGitIdentityValue -Value $registeredSlug) {
            $UsuarioSlug = $registeredSlug
        }
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

    $hasAlias = (Test-UsableGitIdentityValue -Value $UsuarioSlug)
    $hasGitHubIdentity = (Test-UsableGitIdentityValue -Value $GitHubUsuario)
    $shouldPromptAlias = ((-not $SoloVerificar) -and (-not $SinOnboarding) -and (($Actualizar) -or ($Reconfigurar) -or (-not $hasAlias)))

    if ($hasAlias -and $hasGitHubIdentity -and (-not $Actualizar) -and (-not $Reconfigurar)) {
        Write-SetupSuccess ("Identidad local reutilizada: {0} vinculado a GitHub {1}." -f (ConvertTo-ProjectUserSlug -Value $UsuarioSlug), $GitHubUsuario)
    } elseif ($shouldPromptAlias) {
        if (Test-UsableGitIdentityValue -Value $registeredSlug) {
            Show-SetupInteractiveIntro -RepoRoot $RepoRoot
            Write-SetupStep "Cuenta GitHub reconocida"
            Write-SetupSuccess ("{0} ya esta vinculada a la rama '{1}'." -f $GitHubUsuario, $registeredSlug)
            $aliasChoice = Read-SetupMenu `
                -Title "Que quieres hacer con esta identidad?" `
                -Options @(
                    [pscustomobject]@{
                        Label = "Usar rama '$registeredSlug'"
                        Description = "Reutiliza la identidad registrada en este repo."
                        Value = "use"
                    },
                    [pscustomobject]@{
                        Label = "Renombrar alias/rama"
                        Description = "Cambia el nombre visible de commits, logs y rama vinculada."
                        Value = "rename"
                    }
                )

            if ($aliasChoice -eq "rename") {
                $UsuarioSlug = Read-SetupValue -Prompt "Nuevo alias/rama para $GitHubUsuario" -DefaultValue $registeredSlug -Required
                $UsuarioSlug = ConvertTo-ProjectUserSlug -Value $UsuarioSlug
            } else {
                $UsuarioSlug = $registeredSlug
            }
        } else {
            Show-SetupInteractiveIntro -RepoRoot $RepoRoot

            Write-SetupStep "Configurando tu usuario de estudio"
            Write-SetupInfo ("GitHub CLI resolvera tu usuario y correo automaticamente desde la cuenta {0}." -f $GitHubUsuario)
            if (($Actualizar -or $Reconfigurar) -and $hasAlias) {
                Write-SetupInfo ("Alias actual detectado: {0}. Presiona Enter para conservarlo o escribe uno nuevo para renombrar tu identidad local." -f $defaultSlug)
            } else {
                Write-SetupInfo "Solo necesitas elegir el alias local que se usara en tus commits, logs y rama personal."
            }

            $UsuarioSlug = Read-SetupValue -Prompt "Alias local del estudiante (ej. axel)" -DefaultValue $defaultSlug -Required
            $UsuarioSlug = ConvertTo-ProjectUserSlug -Value $UsuarioSlug
        }
    } elseif ((-not $SoloVerificar) -and (-not $SinOnboarding) -and $Actualizar) {
        Write-SetupInfo ("Actualizar reutilizara el alias local '{0}'." -f $defaultSlug)
    } elseif ((-not $SoloVerificar) -and (-not $hasGitHubIdentity)) {
        Write-SetupInfo "SinOnboarding activo; se usaran valores por defecto para la identidad local."
    }

    if (-not (Test-UsableGitIdentityValue -Value $UsuarioSlug)) {
        $UsuarioSlug = $defaultSlug
    }
    $UsuarioSlug = ConvertTo-ProjectUserSlug -Value $UsuarioSlug

    if (-not (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        throw "No pude resolver la cuenta de GitHub para este clon. Usa la sesion de GitHub CLI o ejecuta el setup pasando -GitHubUsuario <tu_usuario>."
    }
    $GitNombre = $UsuarioSlug
    if (-not (Test-UsableGitIdentityValue -Value $GitCorreo)) {
        $GitCorreo = ("{0}@users.noreply.github.com" -f $GitHubUsuario)
    }

    $previousSlugForReturn = $configuredSlug
    if (-not (Test-UsableGitIdentityValue -Value $previousSlugForReturn)) {
        $previousSlugForReturn = $registeredSlug
    }

    return @{
        UsuarioSlug = $UsuarioSlug
        GitHubUsuario = $GitHubUsuario.Trim()
        GitNombre = $GitNombre.Trim()
        GitCorreo = $GitCorreo.Trim()
        PreviousUsuarioSlug = if (Test-UsableGitIdentityValue -Value $previousSlugForReturn) { (ConvertTo-ProjectUserSlug -Value $previousSlugForReturn) } else { $null }
        PreviousGitHubUsuario = if (Test-UsableGitIdentityValue -Value $configuredGitHubUser) { $configuredGitHubUser.Trim() } else { $null }
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
        [string]$UsuarioSlug,
        [string]$GitHubUsuario,
        [string]$GitNombre,
        [string]$GitCorreo,
        [switch]$SoloVerificar
    )

    if (-not $GitPath) {
        Write-SetupWarning "Git no esta confirmado; no se configurara el repo."
        return
    }

    if (-not (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        throw "No se puede configurar Git local sin una cuenta GitHub valida."
    }

    $UsuarioSlug = ConvertTo-ProjectUserSlug -Value $UsuarioSlug
    $GitNombre = $UsuarioSlug
    if (-not (Test-UsableGitIdentityValue -Value $GitCorreo)) {
        $GitCorreo = ("{0}@users.noreply.github.com" -f $GitHubUsuario)
    }

    Push-Location $RepoRoot
    try {
        if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
            Invoke-SetupCommand -FilePath $GitPath -Arguments @("init") -Description "Inicializando repositorio Git..." -SoloVerificar:$SoloVerificar
        } else {
            Write-SetupSuccess "Repositorio Git ya existe."
        }

        Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "--local", "github.user", $GitHubUsuario) -Description "Configurando github.user local..." -SoloVerificar:$SoloVerificar
        Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "--local", "user.name", $GitNombre) -Description "Configurando user.name local..." -SoloVerificar:$SoloVerificar
        Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "--local", "user.email", $GitCorreo) -Description "Configurando user.email local..." -SoloVerificar:$SoloVerificar
        Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "--local", "estudio.github.login", $GitHubUsuario) -Description "Vinculando alias local con GitHub..." -SoloVerificar:$SoloVerificar
        Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "--local", "estudio.usuario.alias", $UsuarioSlug) -Description "Guardando alias local de estudio..." -SoloVerificar:$SoloVerificar
        Invoke-SetupCommand -FilePath $GitPath -Arguments @("config", "--local", "estudio.github.branch", $UsuarioSlug) -Description "Guardando rama vinculada a esta cuenta GitHub..." -SoloVerificar:$SoloVerificar
        Set-ProjectUserRegistryEntry -RepoRoot $RepoRoot -GitHubUsuario $GitHubUsuario -UsuarioSlug $UsuarioSlug -SoloVerificar:$SoloVerificar
    } finally {
        Pop-Location
    }
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

function Test-ProjectGitDirty {
    param(
        [string]$RepoRoot,
        [string]$GitPath
    )

    Push-Location $RepoRoot
    try {
        $status = (& $GitPath status --porcelain 2>$null)
        return (-not [string]::IsNullOrWhiteSpace(($status -join "")))
    } finally {
        Pop-Location
    }
}

function Move-ProjectUserDirectory {
    param(
        [string]$RepoRoot,
        [string]$PreviousUsuarioSlug,
        [string]$UsuarioSlug,
        [switch]$SoloVerificar
    )

    if (-not (Test-UsableGitIdentityValue -Value $PreviousUsuarioSlug)) {
        return
    }
    if ($PreviousUsuarioSlug -eq $UsuarioSlug) {
        return
    }

    $oldDir = Join-Path $RepoRoot ("usuarios\" + $PreviousUsuarioSlug)
    $newDir = Join-Path $RepoRoot "usuario"
    if (-not (Test-Path -LiteralPath $oldDir)) {
        return
    }
    if (Test-Path -LiteralPath $newDir) {
        Write-SetupWarning "Ya existe usuario; no se movera usuarios\$PreviousUsuarioSlug automaticamente."
        return
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Migraria usuarios\$PreviousUsuarioSlug a usuario."
        return
    }

    Move-Item -LiteralPath $oldDir -Destination $newDir
    Write-SetupSuccess "Carpeta de usuario migrada: usuarios\$PreviousUsuarioSlug -> usuario."
}

function Rename-ProjectUserBranch {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitPath,
        [string]$PreviousUsuarioSlug,
        [string]$UsuarioSlug,
        [switch]$SoloVerificar
    )

    if (-not $GitPath) {
        return
    }
    if (-not (Test-UsableGitIdentityValue -Value $PreviousUsuarioSlug)) {
        return
    }
    if ($PreviousUsuarioSlug -eq $UsuarioSlug) {
        return
    }
    if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
        return
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Renombraria la rama local '$PreviousUsuarioSlug' a '$UsuarioSlug' si existe."
        return
    }

    Push-Location $RepoRoot
    try {
        $currentBranch = (& $GitPath branch --show-current 2>$null | Select-Object -First 1)
        $hasOldLocal = Test-ProjectGitRefExists -RepoRoot $RepoRoot -GitPath $GitPath -RefName ("refs/heads/" + $PreviousUsuarioSlug)
        $hasNewLocal = Test-ProjectGitRefExists -RepoRoot $RepoRoot -GitPath $GitPath -RefName ("refs/heads/" + $UsuarioSlug)

        if ($hasNewLocal) {
            Write-SetupWarning "La rama local '$UsuarioSlug' ya existe; no se renombrara '$PreviousUsuarioSlug'."
            return
        }
        if (-not $hasOldLocal) {
            Write-SetupWarning "No existe una rama local '$PreviousUsuarioSlug' que pueda renombrarse a '$UsuarioSlug'."
            return
        }

        if ($currentBranch -eq $PreviousUsuarioSlug) {
            Invoke-SetupCommand -FilePath $GitPath -Arguments @("branch", "-m", $UsuarioSlug) -Description "Renombrando rama actual $PreviousUsuarioSlug -> $UsuarioSlug..." -SoloVerificar:$false
        } else {
            Invoke-SetupCommand -FilePath $GitPath -Arguments @("branch", "-m", $PreviousUsuarioSlug, $UsuarioSlug) -Description "Renombrando rama local $PreviousUsuarioSlug -> $UsuarioSlug..." -SoloVerificar:$false
        }
        Write-SetupSuccess "Rama local vinculada actualizada: $PreviousUsuarioSlug -> $UsuarioSlug."

        $hasOldRemote = Test-ProjectGitRefExists -RepoRoot $RepoRoot -GitPath $GitPath -RefName ("refs/remotes/origin/" + $PreviousUsuarioSlug)
        if ($hasOldRemote) {
            $remoteChoice = Read-SetupMenu `
                -Title "Rama remota detectada: origin/$PreviousUsuarioSlug" `
                -Options @(
                    [pscustomobject]@{
                        Label = "Renombrar tambien en GitHub"
                        Description = "Sube origin/$UsuarioSlug y borra origin/$PreviousUsuarioSlug."
                        Value = "rename"
                    },
                    [pscustomobject]@{
                        Label = "Dejar remoto como esta"
                        Description = "Solo renombra este clon local por ahora."
                        Value = "keep"
                    }
                ) `
                -DefaultIndex 1
            if ($remoteChoice -eq "rename") {
                Invoke-SetupCommand -FilePath $GitPath -Arguments @("push", "-u", "origin", $UsuarioSlug) -Description "Subiendo rama remota origin/$UsuarioSlug..." -SoloVerificar:$false
                $deleteExit = Invoke-SetupCommand -FilePath $GitPath -Arguments @("push", "origin", "--delete", $PreviousUsuarioSlug) -Description "Eliminando rama remota origin/$PreviousUsuarioSlug..." -SoloVerificar:$false -AllowFailure
                if ($deleteExit -eq 0) {
                    Write-SetupSuccess "Rama remota renombrada: origin/$PreviousUsuarioSlug -> origin/$UsuarioSlug."
                } else {
                    Write-SetupWarning "La nueva rama se subio, pero no pude borrar origin/$PreviousUsuarioSlug automaticamente."
                    Write-SetupInfo "Cuando confirmes permisos, ejecuta: git push origin --delete $PreviousUsuarioSlug"
                }
            } else {
                Write-SetupWarning "origin/$PreviousUsuarioSlug sigue existiendo."
                Write-SetupInfo "Cuando quieras renombrarla en GitHub: git push -u origin $UsuarioSlug ; git push origin --delete $PreviousUsuarioSlug"
            }
        }
    } finally {
        Pop-Location
    }
}

function Initialize-ProjectUser {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$GitPath,
        [string]$UsuarioSlug,
        [AllowNull()][string]$PreviousUsuarioSlug,
        [AllowNull()][string]$GitHubUsuario,
        [AllowNull()][string]$PreviousGitHubUsuario,
        [switch]$SoloVerificar,
        [switch]$SinRamaUsuario,
        [switch]$Actualizar,
        [switch]$Reconfigurar
    )

    $usuarioDir = Resolve-ProjectUserDirectory -RepoRoot $RepoRoot -UsuarioSlug $UsuarioSlug -Create -SoloVerificar:$SoloVerificar
    $logsDir = Join-Path $usuarioDir "logs"
    $erroresPath = Join-Path $usuarioDir "errores.md"
    $usuarioConfig = Join-Path $RepoRoot ".estudio_usuario"
    $sameGithubAccount = $true
    if ((Test-UsableGitIdentityValue -Value $PreviousGitHubUsuario) -and (Test-UsableGitIdentityValue -Value $GitHubUsuario)) {
        $sameGithubAccount = ($PreviousGitHubUsuario.Trim().ToLowerInvariant() -eq $GitHubUsuario.Trim().ToLowerInvariant())
    }

    if ($sameGithubAccount) {
        Move-ProjectUserDirectory -RepoRoot $RepoRoot -PreviousUsuarioSlug $PreviousUsuarioSlug -UsuarioSlug $UsuarioSlug -SoloVerificar:$SoloVerificar
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Escribiria .estudio_usuario con '$UsuarioSlug'."
        Write-SetupInfo "[SoloVerificar] Prepararia usuario\errores.md vacio si no existe."
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

    if ($Actualizar) {
        if ($sameGithubAccount) {
            Rename-ProjectUserBranch -RepoRoot $RepoRoot -GitPath $GitPath -PreviousUsuarioSlug $PreviousUsuarioSlug -UsuarioSlug $UsuarioSlug -SoloVerificar:$SoloVerificar
        }
        Write-SetupInfo "Modo actualizar activo; no se cambiara de rama salvo renombrar la rama vinculada si cambio el alias."
        return
    }

    if ($Reconfigurar) {
        if ($sameGithubAccount) {
            Rename-ProjectUserBranch -RepoRoot $RepoRoot -GitPath $GitPath -PreviousUsuarioSlug $PreviousUsuarioSlug -UsuarioSlug $UsuarioSlug -SoloVerificar:$SoloVerificar
        }
        Write-SetupInfo "Modo reconfigurar activo; no se cambiara de rama salvo renombrar la rama vinculada si cambio el alias."
        return
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

    if (Test-ProjectGitDirty -RepoRoot $RepoRoot -GitPath $GitPath) {
        Write-SetupWarning "Hay cambios locales sin guardar; no se cambiara de '$currentBranch' a '$UsuarioSlug' automaticamente."
        Write-SetupInfo "Esto es esperado si estas actualizando o editando el framework desde main."
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
