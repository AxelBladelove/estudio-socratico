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

function Read-SetupMenu {
    param(
        [string]$Title,
        [array]$Options,
        [int]$DefaultIndex = 0
    )

    if (-not $Options -or $Options.Count -eq 0) {
        throw "Read-SetupMenu necesita al menos una opcion."
    }

    if ($DefaultIndex -lt 0 -or $DefaultIndex -ge $Options.Count) {
        $DefaultIndex = 0
    }

    Write-SetupLine ""
    Write-SetupLine $Title Cyan
    if ($script:SetupLogPath) {
        foreach ($option in $Options) {
            Add-Content -Path $script:SetupLogPath -Value ("  - {0}" -f $option.Label)
        }
    }

    try {
        $raw = $Host.UI.RawUI
        $width = [Math]::Max(50, $raw.WindowSize.Width - 1)
        $selected = $DefaultIndex
        $top = $raw.CursorPosition.Y

        function Write-MenuLine {
            param(
                [int]$Index,
                [bool]$Selected
            )

            $option = $Options[$Index]
            $prefix = if ($Selected) { "> " } else { "  " }
            $line = "{0}{1}" -f $prefix, $option.Label
            if (-not [string]::IsNullOrWhiteSpace($option.Description)) {
                $line = "{0} - {1}" -f $line, $option.Description
            }
            if ($line.Length -gt $width) {
                $line = $line.Substring(0, $width)
            }
            $line = $line.PadRight($width)
            $color = if ($Selected) { [ConsoleColor]::Cyan } else { [ConsoleColor]::Gray }
            Write-Host $line -ForegroundColor $color
        }

        while ($true) {
            $raw.CursorPosition = New-Object System.Management.Automation.Host.Coordinates 0, $top
            for ($i = 0; $i -lt $Options.Count; $i++) {
                Write-MenuLine -Index $i -Selected:($i -eq $selected)
            }

            $key = $raw.ReadKey("NoEcho,IncludeKeyDown")
            switch ($key.VirtualKeyCode) {
                38 {
                    $selected--
                    if ($selected -lt 0) { $selected = $Options.Count - 1 }
                }
                40 {
                    $selected++
                    if ($selected -ge $Options.Count) { $selected = 0 }
                }
                13 {
                    Write-SetupInfo ("Seleccionado: {0}" -f $Options[$selected].Label)
                    return $Options[$selected].Value
                }
            }
        }
    } catch {
        for ($i = 0; $i -lt $Options.Count; $i++) {
            $option = $Options[$i]
            Write-SetupLine ("  {0}. {1}" -f ($i + 1), $option.Label) Gray
            if (-not [string]::IsNullOrWhiteSpace($option.Description)) {
                Write-SetupLine ("     {0}" -f $option.Description) DarkGray
            }
        }

        do {
            $answer = Read-Host ("Elige una opcion [1-{0}]" -f $Options.Count)
            if ([string]::IsNullOrWhiteSpace($answer)) {
                $answer = ($DefaultIndex + 1)
            }
            $number = 0
            if ([int]::TryParse("$answer", [ref]$number) -and $number -ge 1 -and $number -le $Options.Count) {
                Write-SetupInfo ("Seleccionado: {0}" -f $Options[$number - 1].Label)
                return $Options[$number - 1].Value
            }
            Write-SetupWarning "Opcion invalida."
        } while ($true)
    }
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
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = & $FilePath @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

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

function Invoke-SetupInteractiveCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$Description,
        [switch]$SoloVerificar,
        [switch]$AllowFailure,
        [int]$TimeoutSeconds = 300
    )

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] $Description"
        Write-SetupInfo "  $FilePath $($Arguments -join ' ')"
        return
    }

    Write-SetupInfo $Description
    Write-SetupInfo "Si el navegador no vuelve a la terminal, cancela la pagina y reintenta desde el setup."
    if ($script:SetupLogPath) {
        Add-Content -Path $script:SetupLogPath -Value ("[CMD] {0} {1}" -f $FilePath, ($Arguments -join " "))
    }

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList (Convert-ToArgumentString -Arguments $Arguments) `
        -NoNewWindow `
        -PassThru

    $completed = $process.WaitForExit($TimeoutSeconds * 1000)
    if (-not $completed) {
        try {
            $process.Kill($true)
        } catch {
            try { $process.Kill() } catch {}
        }
        $message = "El comando interactivo no termino despues de $TimeoutSeconds segundos: $FilePath $($Arguments -join ' ')"
        if ($AllowFailure) {
            Write-SetupWarning $message
            return 124
        }
        throw $message
    }

    $exitCode = $process.ExitCode
    if (($exitCode -ne 0) -and (-not $AllowFailure)) {
        throw "Fallo el comando interactivo: $FilePath $($Arguments -join ' ')"
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
