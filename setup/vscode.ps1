function Get-WindowsTerminalSettingsPath {
    $paths = @(
        (Join-Path $env:LOCALAPPDATA "Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json"),
        (Join-Path $env:LOCALAPPDATA "Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState\settings.json"),
        (Join-Path $env:LOCALAPPDATA "Microsoft\Windows Terminal\settings.json")
    )

    foreach ($path in $paths) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

function Set-TerminalDefaultPowerShell {
    param(
        [AllowNull()][string]$PowerShellPath,
        [switch]$SoloVerificar
    )

    if (-not $PowerShellPath) {
        Write-SetupWarning "PowerShell 7 no esta confirmado; no se cambiara Windows Terminal."
        return
    }

    $settingsPath = Get-WindowsTerminalSettingsPath
    if (-not $settingsPath) {
        Write-SetupWarning "No encontre settings.json de Windows Terminal."
        return
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Configuraria PowerShell 7 como perfil predeterminado en Windows Terminal."
        return
    }

    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    $profiles = @($settings.profiles.list)
    $profile = $profiles | Where-Object {
        ($_.source -eq "Windows.Terminal.PowershellCore") -or
        ($_.commandline -like "*pwsh.exe*") -or
        ($_.name -eq "PowerShell")
    } | Select-Object -First 1

    if (-not $profile) {
        $profile = [pscustomobject]@{
            guid = "{$((New-Guid).Guid)}"
            hidden = $false
            name = "PowerShell"
            commandline = $PowerShellPath
        }
        $profiles += $profile
        $settings.profiles.list = $profiles
    }

    $settings.defaultProfile = $profile.guid
    $settings | ConvertTo-Json -Depth 20 | Set-Content -Path $settingsPath -Encoding utf8
    Write-SetupSuccess "Windows Terminal abrira PowerShell por defecto."
}

function Install-VSCodeExtensions {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$CodePath,
        [switch]$SinExtensiones,
        [switch]$SoloVerificar
    )

    if ($SinExtensiones) {
        Write-SetupWarning "SinExtensiones activo; no se instalaran extensiones."
        return
    }

    if (-not $CodePath) {
        Write-SetupWarning "VS Code no esta confirmado; no se instalaran extensiones."
        return
    }

    $extensionsPath = Join-Path $RepoRoot ".vscode\extensions.json"
    if (-not (Test-Path $extensionsPath)) {
        Write-SetupWarning "No existe .vscode\extensions.json."
        return
    }

    $config = Get-Content $extensionsPath -Raw | ConvertFrom-Json
    foreach ($extension in @($config.recommendations)) {
        $exitCode = Invoke-SetupCommand `
            -FilePath $CodePath `
            -Arguments @("--install-extension", $extension) `
            -Description "Instalando extension VS Code: $extension" `
            -SoloVerificar:$SoloVerificar `
            -AllowFailure

        if (($exitCode -ne 0) -and (-not $SoloVerificar)) {
            Write-SetupWarning "No se pudo instalar la extension $extension. El setup continuara."
        }
    }
}

function Set-VSCodeF9BuildKey {
    param(
        [switch]$SoloVerificar
    )

    $keybindingsPath = Join-Path $env:APPDATA "Code\User\keybindings.json"
    $keybindingsDir = Split-Path -Parent $keybindingsPath

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Configuraria F9 para ejecutar la tarea Compilar y Grabar en VS Code."
        return
    }

    if (-not (Test-Path $keybindingsDir)) {
        New-Item -ItemType Directory -Path $keybindingsDir -Force | Out-Null
    }

    $bindings = @()
    if (Test-Path $keybindingsPath) {
        $raw = Get-Content $keybindingsPath -Raw
        if (-not [string]::IsNullOrWhiteSpace($raw)) {
            try {
                $bindings = @($raw | ConvertFrom-Json)
            } catch {
                Write-SetupWarning "No pude leer keybindings.json como JSON limpio; no modificare los atajos automaticamente."
                return
            }
        }
    }

    $taskLabel = "Compilar y Grabar (Sistema Socratico)"
    $existing = $bindings | Where-Object {
        ($_.key -eq "f9") -and
        ($_.command -eq "workbench.action.tasks.runTask") -and
        ($_.args -eq $taskLabel)
    } | Select-Object -First 1

    if ($existing) {
        Write-SetupSuccess "F9 ya ejecuta la tarea socratica en VS Code."
        return
    }

    $bindings = @(
        $bindings | Where-Object {
            -not (($_.key -eq "f9") -and ($_.when -like "*editorLangId == c*"))
        }
    )

    $bindings += [pscustomobject]@{
        key = "f9"
        command = "workbench.action.tasks.runTask"
        args = $taskLabel
        when = "editorTextFocus && editorLangId == c"
    }

    $bindings | ConvertTo-Json -Depth 20 | Set-Content -Path $keybindingsPath -Encoding utf8
    Write-SetupSuccess "F9 ejecutara Compilar y Grabar para archivos C en VS Code."
}

function Configure-VSCode {
    param(
        [string]$RepoRoot,
        [AllowNull()][string]$CodePath,
        [AllowNull()][string]$PowerShellPath,
        [switch]$SinExtensiones,
        [switch]$SoloVerificar
    )

    Set-TerminalDefaultPowerShell -PowerShellPath $PowerShellPath -SoloVerificar:$SoloVerificar
    Set-VSCodeF9BuildKey -SoloVerificar:$SoloVerificar
    Install-VSCodeExtensions -RepoRoot $RepoRoot -CodePath $CodePath -SinExtensiones:$SinExtensiones -SoloVerificar:$SoloVerificar
}
