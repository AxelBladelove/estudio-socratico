$script:SetupLogPath = $null

function Start-SetupLog {
    param([string]$Path)

    $script:SetupLogPath = $Path
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    "=== Inicio setup $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" | Set-Content -Path $Path -Encoding utf8
}

function Stop-SetupLog {
    if ($script:SetupLogPath) {
        Add-Content -Path $script:SetupLogPath -Value ("=== Fin setup {0} ===" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
    }
}

function Write-SetupLine {
    param(
        [string]$Message,
        [ConsoleColor]$Color = [ConsoleColor]::White
    )

    Write-Host $Message -ForegroundColor $Color
    if ($script:SetupLogPath) {
        Add-Content -Path $script:SetupLogPath -Value $Message
    }
}

function Write-SetupTitle {
    param([string]$Message)

    Write-SetupLine ""
    Write-SetupLine "==============================================" Cyan
    Write-SetupLine "  $Message" Cyan
    Write-SetupLine "==============================================" Cyan
}

function Write-SetupStep {
    param([string]$Message)
    Write-SetupLine ""
    Write-SetupLine "==> $Message" Cyan
}

function Write-SetupSuccess {
    param([string]$Message)
    Write-SetupLine "[OK] $Message" Green
}

function Write-SetupWarning {
    param([string]$Message)
    Write-SetupLine "[AVISO] $Message" Yellow
}

function Write-SetupInfo {
    param([string]$Message)
    Write-SetupLine "[INFO] $Message" DarkCyan
}

function Open-SetupUrlIfWanted {
    param(
        [string]$Url,
        [string]$Reason
    )

    Write-SetupInfo $Reason
    Write-SetupInfo $Url
    $answer = Read-Host "Abrir ese enlace en el navegador? [S/n]"
    if ($answer -notmatch '(?i)^n(o)?$') {
        Start-Process $Url | Out-Null
    }
}

function Write-SetupError {
    param([string]$Message)
    Write-SetupLine "[ERROR] $Message" Red
}

function New-SetupDirectory {
    param(
        [string]$Path,
        [switch]$SoloVerificar
    )

    if (Test-Path $Path) {
        return
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Crearia carpeta: $Path"
        return
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Add-SessionPath {
    param([string]$PathToAdd)

    if (-not $PathToAdd) {
        return
    }

    $entries = @($env:Path -split ";")
    if ($entries -notcontains $PathToAdd) {
        $env:Path = "$PathToAdd;$env:Path"
    }
}

function Test-SessionPathContains {
    param([string]$PathToFind)

    if (-not $PathToFind) {
        return $false
    }

    $entries = @($env:Path -split ";")
    return ($entries -contains $PathToFind)
}

function Add-UserPath {
    param(
        [string]$PathToAdd,
        [switch]$SoloVerificar
    )

    if (-not (Test-Path $PathToAdd)) {
        return
    }

    $current = [Environment]::GetEnvironmentVariable("Path", "User")
    $entries = if ($current) { @($current -split ";") } else { @() }
    if ($entries -contains $PathToAdd) {
        Write-SetupSuccess "PATH de usuario ya contiene $PathToAdd"
        if (-not (Test-SessionPathContains -PathToFind $PathToAdd)) {
            if (-not $SoloVerificar) {
                Read-Host "Presiona Enter para recargar el PATH de esta terminal"
            }
        }
        Add-SessionPath -PathToAdd $PathToAdd
        return
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Agregaria al PATH de usuario: $PathToAdd"
        return
    }

    $newPath = if ($current) { "$current;$PathToAdd" } else { $PathToAdd }
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    Read-Host "Presiona Enter para recargar el PATH de esta terminal"
    Add-SessionPath -PathToAdd $PathToAdd
    Write-SetupSuccess "Agregado al PATH de usuario: $PathToAdd"
}

function Invoke-SetupCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$Description,
        [switch]$SoloVerificar,
        [switch]$AllowFailure
    )

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] $Description"
        Write-SetupInfo "  $FilePath $($Arguments -join ' ')"
        return
    }

    Write-SetupInfo $Description
    $output = & $FilePath @Arguments 2>&1
    $exitCode = $LASTEXITCODE

    foreach ($line in @($output)) {
        Write-Host $line
        if ($script:SetupLogPath) {
            Add-Content -Path $script:SetupLogPath -Value $line
        }
    }

    if (($exitCode -ne 0) -and (-not $AllowFailure)) {
        throw "Fallo el comando: $FilePath $($Arguments -join ' ')"
    }

    if ($AllowFailure) {
        return $exitCode
    }
}

function Resolve-SetupTool {
    param(
        [AllowNull()][string]$CommandName,
        [string[]]$Candidates = @()
    )

    if ($CommandName) {
        $command = Get-Command $CommandName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($command) {
            return $command.Source
        }
    }

    foreach ($candidate in $Candidates) {
        $match = @(Get-Item -Path $candidate -ErrorAction SilentlyContinue) | Select-Object -First 1
        if ($match) {
            return $match.FullName
        }
    }

    return $null
}

function Wait-SetupTool {
    param(
        [AllowNull()][string]$CommandName,
        [string[]]$Candidates = @(),
        [int]$TimeoutSeconds = 90
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $tool = Resolve-SetupTool -CommandName $CommandName -Candidates $Candidates
        if ($tool) {
            return $tool
        }

        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)

    return $null
}

function Convert-ToArgumentString {
    param([string[]]$Arguments)

    $items = foreach ($argument in $Arguments) {
        if ([string]::IsNullOrEmpty($argument)) {
            '""'
        } elseif ($argument -match '[\s"]') {
            '"' + ($argument -replace '"', '\"') + '"'
        } else {
            $argument
        }
    }

    return ($items -join " ")
}
