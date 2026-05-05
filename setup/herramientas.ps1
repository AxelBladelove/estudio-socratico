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

function Request-SetupElevationIfNeeded {
    param(
        [array]$ToolSpecs,
        [string]$SetupScript,
        [string]$RepoRoot,
        [switch]$SoloVerificar,
        [switch]$SinWinget,
        [switch]$Elevado,
        [switch]$SinExtensiones,
        [AllowNull()][string]$GitHubUsuario,
        [string]$GitNombre,
        [string]$GitCorreo
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
        "-Elevado",
        "-GitHubUsuario", $GitHubUsuario,
        "-GitNombre", $GitNombre,
        "-GitCorreo", $GitCorreo
    )

    if ($SinExtensiones) {
        $args += "-SinExtensiones"
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
