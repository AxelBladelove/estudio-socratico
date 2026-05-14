function Get-ToolSpecs {
    return @(
        @{
            Name = "Git"
            WingetId = "Git.Git"
            Command = "git"
            Candidates = @("$env:ProgramFiles\Git\cmd\git.exe", "${env:ProgramFiles(x86)}\Git\cmd\git.exe")
        },
        @{
            Name = "PowerShell"
            WingetId = "Microsoft.PowerShell"
            Command = "pwsh"
            Candidates = @("$env:ProgramFiles\PowerShell\7\pwsh.exe")
        },
        @{
            Name = "Node.js"
            WingetId = "OpenJS.NodeJS.LTS"
            Command = "node"
            Candidates = @("$env:ProgramFiles\nodejs\node.exe")
        },
        @{
            Name = "Bun"
            WingetId = "Oven-sh.Bun"
            Command = "bun"
            Candidates = @(
                "$env:USERPROFILE\.bun\bin\bun.exe",
                "$env:LOCALAPPDATA\Microsoft\WinGet\Links\bun.exe"
            )
        },
        @{
            Name = "Python"
            WingetId = "Python.Python.3.12"
            Command = "python"
            Candidates = @("$env:LOCALAPPDATA\Programs\Python\Python*\python.exe", "$env:ProgramFiles\Python*\python.exe")
        },
        @{
            Name = "GitHub CLI"
            WingetId = "GitHub.cli"
            Command = "gh"
            Candidates = @("$env:ProgramFiles\GitHub CLI\gh.exe")
        },
        @{
            Name = "Exercism CLI"
            WingetId = "Exercism.CLI"
            Command = "exercism"
            Candidates = @("$env:LOCALAPPDATA\Microsoft\WindowsApps\exercism.exe")
        },
        @{
            Name = "MSYS2"
            WingetId = "MSYS2.MSYS2"
            Command = $null
            Candidates = @("C:\msys64\usr\bin\bash.exe")
        },
        @{
            Name = "VS Code"
            WingetId = "Microsoft.VisualStudioCode"
            Command = "code"
            Candidates = @(
                "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd",
                "$env:ProgramFiles\Microsoft VS Code\bin\code.cmd"
            )
        }
    )
}

function ConvertFrom-SetupSecureString {
    param([System.Security.SecureString]$SecureValue)

    if (-not $SecureValue -or $SecureValue.Length -eq 0) {
        return $null
    }

    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Request-SetupElevationIfNeeded {
    param(
        [array]$ToolSpecs,
        [string]$SetupScript,
        [string]$RepoRoot,
        [switch]$SoloVerificar,
        [switch]$SinWinget,
        [switch]$Elevado,
        [switch]$SinExtensiones,
        [switch]$SinOnboarding,
        [switch]$SinRamaUsuario,
        [switch]$Actualizar,
        [switch]$Reconfigurar,
        [AllowNull()][string]$UsuarioSlug,
        [AllowNull()][string]$GitHubUsuario,
        [AllowNull()][string]$GitNombre,
        [AllowNull()][string]$GitCorreo
    )

    if ($Elevado -and (-not (Test-IsAdministrator))) {
        throw "Windows no entrego permisos de administrador al proceso elevado."
    }

    if ($SoloVerificar -or $SinWinget -or (Test-IsAdministrator)) {
        return
    }

    $missing = @(
        foreach ($tool in $ToolSpecs) {
            if (-not (Resolve-SetupTool -CommandName $tool.Command -Candidates $tool.Candidates)) {
                $tool
            }
        }
    )

    if ($missing.Count -eq 0) {
        return
    }

    $names = ($missing | ForEach-Object { $_.Name }) -join ", "
    Write-SetupWarning "Faltan herramientas que normalmente instala winget: $names"
    Write-SetupWarning "Se pediran permisos de administrador una sola vez."

    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $SetupScript,
        "-Elevado"
    )

    if ($UsuarioSlug) { $args += @("-UsuarioSlug", $UsuarioSlug) }
    if ($GitHubUsuario) { $args += @("-GitHubUsuario", $GitHubUsuario) }
    if ($GitNombre) { $args += @("-GitNombre", $GitNombre) }
    if ($GitCorreo) { $args += @("-GitCorreo", $GitCorreo) }
    if ($Actualizar) {
        $args += "-Actualizar"
    }
    if ($Reconfigurar) {
        $args += "-Reconfigurar"
    }
    if ($SinExtensiones) {
        $args += "-SinExtensiones"
    }
    if ($SinOnboarding) {
        $args += "-SinOnboarding"
    }
    if ($SinRamaUsuario) {
        $args += "-SinRamaUsuario"
    }

    $powershell = Resolve-SetupTool -CommandName "pwsh" -Candidates @("$env:ProgramFiles\PowerShell\7\pwsh.exe")
    if (-not $powershell) {
        $powershell = "powershell.exe"
    }

    $process = Start-Process `
        -FilePath $powershell `
        -ArgumentList (Convert-ToArgumentString -Arguments $args) `
        -Verb RunAs `
        -WorkingDirectory $RepoRoot `
        -Wait `
        -PassThru

    exit $process.ExitCode
}

function Install-WingetPackage {
    param(
        [string]$PackageId,
        [string]$DisplayName,
        [switch]$SoloVerificar,
        [switch]$SinWinget
    )

    if ($SinWinget) {
        Write-SetupWarning "SinWinget activo; instala $DisplayName manualmente."
        return
    }

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        Write-SetupWarning "winget no esta disponible. Instala $DisplayName manualmente."
        return
    }

    Invoke-SetupCommand `
        -FilePath "winget" `
        -Arguments @("install", "--id", $PackageId, "-e", "--silent", "--accept-package-agreements", "--accept-source-agreements") `
        -Description "Instalando $DisplayName con winget..." `
        -SoloVerificar:$SoloVerificar
}

function Ensure-Tools {
    param(
        [array]$ToolSpecs,
        [switch]$SoloVerificar,
        [switch]$SinWinget
    )

    $resolved = @{}

    foreach ($tool in $ToolSpecs) {
        $path = Resolve-SetupTool -CommandName $tool.Command -Candidates $tool.Candidates
        if ($path) {
            Write-SetupSuccess "$($tool.Name) disponible: $path"
            Add-SessionPath -PathToAdd (Split-Path -Parent $path)
            $resolved[$tool.Name] = $path
            continue
        }

        Install-WingetPackage -PackageId $tool.WingetId -DisplayName $tool.Name -SoloVerificar:$SoloVerificar -SinWinget:$SinWinget
        $path = Wait-SetupTool -CommandName $tool.Command -Candidates $tool.Candidates

        if ($path) {
            Write-SetupSuccess "$($tool.Name) instalado: $path"
            Add-SessionPath -PathToAdd (Split-Path -Parent $path)
            $resolved[$tool.Name] = $path
        } else {
            Write-SetupWarning "No pude confirmar $($tool.Name) en esta sesion."
            $resolved[$tool.Name] = $null
        }
    }

    return $resolved
}

function Test-ExercismCliConfiguration {
    param(
        [AllowNull()][string]$ExercismPath,
        [switch]$SoloVerificar,
        [switch]$SinOnboarding
    )

    if (-not $ExercismPath) {
        Write-SetupWarning "Exercism CLI no esta confirmado; podras instalarlo luego con winget install Exercism.CLI."
        return
    }

    $hasToken = $false
    $configPath = Join-Path $env:APPDATA "exercism\user.json"
    if (Test-Path -LiteralPath $configPath) {
        try {
            $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace($config.token)) {
                $hasToken = $true
            }
        } catch {
            Write-SetupWarning "No pude leer el archivo local de configuracion de Exercism; intentare validar con el CLI."
        }
    }

    if (-not $hasToken) {
        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = "Continue"
            $output = & $ExercismPath configure --show 2>&1
            $exitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if ($exitCode -eq 0) {
            $tokenLine = @($output) | Where-Object { $_ -match '^\s*Token:' } | Select-Object -First 1
            if ((-not [string]::IsNullOrWhiteSpace($tokenLine)) -and
                ($tokenLine -notmatch '(?i)<not configured>|not configured') -and
                ($tokenLine -match '^\s*Token:\s*(?:\(-t,\s*--token\)\s*)?\S+')) {
                $hasToken = $true
            }
        } else {
            Write-SetupWarning "No pude consultar la configuracion de Exercism CLI con 'configure --show'."
        }
    }

    if ($hasToken) {
        Write-SetupSuccess "Exercism CLI tiene un token configurado para esta PC."
        Ensure-ExercismCTrackReady -ExercismPath $ExercismPath -SoloVerificar:$SoloVerificar -SinOnboarding:$SinOnboarding | Out-Null
        return
    }

    Write-SetupWarning "Exercism CLI no tiene token configurado."
    if ((-not $SoloVerificar) -and (-not $SinOnboarding)) {
        Open-SetupUrlIfWanted -Url "https://exercism.org/settings/api_cli" -Reason "Para obtener tu token de Exercism, inicia sesion y copia el token del CLI."
        Write-SetupInfo "Pega tu token global de Exercism. Puedes presionar Enter y configurarlo luego."
        $secureToken = Read-Host "Token de Exercism" -AsSecureString
        $token = ConvertFrom-SetupSecureString -SecureValue $secureToken
        if (-not [string]::IsNullOrWhiteSpace($token)) {
            $previousErrorActionPreference = $ErrorActionPreference
            try {
                $ErrorActionPreference = "Continue"
                & $ExercismPath configure --token $token | Out-Null
                $configureExit = $LASTEXITCODE
            } finally {
                $ErrorActionPreference = $previousErrorActionPreference
            }

            if ($configureExit -eq 0) {
                Write-SetupSuccess "Token de Exercism configurado para esta PC."
                Ensure-ExercismCTrackReady -ExercismPath $ExercismPath -SoloVerificar:$SoloVerificar -SinOnboarding:$SinOnboarding | Out-Null
                return
            }

            Write-SetupWarning "No pude guardar el token de Exercism desde la TUI."
        }
    }

    Write-SetupInfo "Configuralo luego con: exercism configure --token TU_TOKEN"
}

function Ensure-ExercismCTrackReady {
    param(
        [string]$ExercismPath,
        [switch]$SoloVerificar,
        [switch]$SinOnboarding
    )

    if (-not $ExercismPath) {
        return $false
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Ejecutaria exercism prepare y validaria el track C."
        return $true
    }

    $prepareExit = Invoke-SetupCommand `
        -FilePath $ExercismPath `
        -Arguments @("prepare") `
        -Description "Preparando configuracion local de Exercism..." `
        -SoloVerificar:$false `
        -AllowFailure

    if ($prepareExit -ne 0) {
        Write-SetupWarning "exercism prepare no termino correctamente; intentare validar el track C de todos modos."
    }

    $maxAttempts = if ($SinOnboarding) { 1 } else { 6 }
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = "Continue"
            $output = & $ExercismPath download --track c --exercise hello-world 2>&1
            $exitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if ($exitCode -eq 0) {
            Write-SetupSuccess "Track C de Exercism listo para descargar ejercicios."
            return $true
        }

        $message = (($output | ForEach-Object { "$_" }) -join " ").Trim()
        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = "exercism download devolvio codigo $exitCode sin salida legible"
        }

        if ($message -match '(?i)already exists|ya existe') {
            Write-SetupSuccess "Track C de Exercism listo; hello-world ya existe en el workspace local."
            return $true
        }

        if ($message -notmatch '(?i)not joined|no has unido|join this track') {
            Write-SetupWarning "No pude validar el track C de Exercism: $message"
            return $false
        }

        Write-SetupWarning "Tu cuenta de Exercism aun no se ha unido al track C."
        if ($SinOnboarding) {
            return $false
        }

        Open-SetupUrlIfWanted -Url "https://exercism.org/tracks/c" -Reason "Unete al track C en Exercism y vuelve a la TUI para continuar."
        Read-Host "Cuando ya estes unido al track C, presiona Enter para reintentar"
    }

    Write-SetupWarning "No pude validar el track C de Exercism despues de varios intentos."
    return $false
}

function Test-GeminiConfiguration {
    param(
        [string]$RepoRoot,
        [switch]$SoloVerificar,
        [switch]$SinOnboarding
    )

    $projectGeminiKey = $null
    if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
        foreach ($configPath in @(
            (Join-Path $RepoRoot "_estudio\soporte\exercism\config.local.json"),
            (Join-Path $RepoRoot "_estudio\soporte\exercism\config.json"),
            (Join-Path $RepoRoot ".estudio_exercism.local.json")
        )) {
            if (-not (Test-Path -LiteralPath $configPath)) {
                continue
            }
            try {
                $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
                $gemini = if ($config.gemini) { $config.gemini } else { $config }
                if (-not [string]::IsNullOrWhiteSpace($gemini.apiKey)) { $projectGeminiKey = $gemini.apiKey }
                elseif (-not [string]::IsNullOrWhiteSpace($gemini.geminiApiKey)) { $projectGeminiKey = $gemini.geminiApiKey }
                elseif (-not [string]::IsNullOrWhiteSpace($gemini.GEMINI_API_KEY)) { $projectGeminiKey = $gemini.GEMINI_API_KEY }

                if (-not [string]::IsNullOrWhiteSpace($projectGeminiKey)) {
                    break
                }
            } catch {
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($projectGeminiKey)) {
        Write-SetupSuccess "Gemini queda configurado desde la configuracion compartida del repo."
        return
    }

    $geminiKey = $env:GEMINI_API_KEY
    if ([string]::IsNullOrWhiteSpace($geminiKey)) {
        $geminiKey = [Environment]::GetEnvironmentVariable("GEMINI_API_KEY", "User")
    }
    if ([string]::IsNullOrWhiteSpace($geminiKey)) {
        $geminiKey = [Environment]::GetEnvironmentVariable("GEMINI_API_KEY", "Machine")
    }

    if ([string]::IsNullOrWhiteSpace($geminiKey)) {
        Write-SetupWarning "Gemini no tiene clave compartida del repo ni GEMINI_API_KEY local; los README importados quedaran con traduccion pendiente."
        Write-SetupInfo "Para una clave compartida del proyecto, configura _estudio\soporte\exercism\config.json."
    } else {
        Write-SetupSuccess "GEMINI_API_KEY local esta configurado para traducciones automaticas."
    }
}
