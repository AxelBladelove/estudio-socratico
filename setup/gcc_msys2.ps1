function Ensure-GccToolchain {
    param(
        [string]$RepoRoot,
        [switch]$SoloVerificar,
        [switch]$SinWinget
    )

    $bashPath = "C:\msys64\usr\bin\bash.exe"
    $gccPath = "C:\msys64\mingw64\bin\gcc.exe"
    $makePath = "C:\msys64\usr\bin\make.exe"
    $mingwMakePath = "C:\msys64\mingw64\bin\mingw32-make.exe"

    if ((Test-Path $gccPath) -and (Test-Path $makePath) -and (Test-Path $mingwMakePath)) {
        Invoke-SetupCommand -FilePath $gccPath -Arguments @("--version") -Description "Validando gcc..." -SoloVerificar:$false
        Invoke-SetupCommand -FilePath $makePath -Arguments @("--version") -Description "Validando make..." -SoloVerificar:$false
        Invoke-SetupCommand -FilePath $mingwMakePath -Arguments @("--version") -Description "Validando mingw32-make..." -SoloVerificar:$false
        Add-UserPath -PathToAdd (Split-Path -Parent $gccPath) -SoloVerificar:$SoloVerificar
        Write-SetupSuccess "GCC disponible: $gccPath"
        Write-SetupSuccess "Make disponible para tests Exercism: $makePath"
        return
    }

    if (-not (Test-Path $bashPath)) {
        if ($SinWinget) {
            Write-SetupWarning "MSYS2 no esta instalado y SinWinget esta activo."
            return
        }

        Install-WingetPackage -PackageId "MSYS2.MSYS2" -DisplayName "MSYS2" -SoloVerificar:$SoloVerificar -SinWinget:$SinWinget
    }

    if ($SoloVerificar) {
        Write-SetupInfo "[SoloVerificar] Instalaria GCC, make y mingw32-make dentro de MSYS2 con pacman."
        return
    }

    $deadline = (Get-Date).AddMinutes(5)
    while ((-not (Test-Path $bashPath)) -and ((Get-Date) -lt $deadline)) {
        Start-Sleep -Seconds 2
    }

    if (-not (Test-Path $bashPath)) {
        throw "MSYS2 no quedo disponible en $bashPath."
    }

    $maxAttempts = 4
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        if ((Test-Path $gccPath) -and (Test-Path $makePath) -and (Test-Path $mingwMakePath)) {
            break
        }

        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $gccLog = Join-Path $RepoRoot ("logs\setup\gcc_intento_{0}_{1}.log" -f $attempt, $timestamp)
        $gccScript = Join-Path $RepoRoot ("logs\setup\gcc_instalar_{0}_{1}.sh" -f $attempt, $timestamp)

        $script = @'
set -euo pipefail
LOG_PATH="$(cygpath -u "$ESTUDIO_GCC_LOG")"
exec > >(tee -a "$LOG_PATH") 2>&1
echo "=== Instalando GCC $(date '+%Y-%m-%d %H:%M:%S') ==="
pacman -Sy --noconfirm msys2-keyring
yes | pacman -Syuu --noconfirm || true
yes | pacman -Syuu --noconfirm || true
yes | pacman -S --needed --noconfirm mingw-w64-x86_64-gcc mingw-w64-x86_64-binutils mingw-w64-x86_64-crt-git mingw-w64-x86_64-make make
/mingw64/bin/gcc.exe --version
/usr/bin/make.exe --version
/mingw64/bin/mingw32-make.exe --version
echo "=== GCC listo $(date '+%Y-%m-%d %H:%M:%S') ==="
'@

        Set-Content -Path $gccScript -Value $script -Encoding ascii
        $env:ESTUDIO_GCC_LOG = $gccLog

        try {
            $exitCode = Invoke-SetupCommand `
                -FilePath $bashPath `
                -Arguments @($gccScript) `
                -Description "Instalando GCC con pacman. Intento $attempt/$maxAttempts. Log: $gccLog" `
                -SoloVerificar:$false `
                -AllowFailure
        } finally {
            Remove-Item Env:ESTUDIO_GCC_LOG -ErrorAction SilentlyContinue
        }

        if ((Test-Path $gccPath) -and (Test-Path $makePath) -and (Test-Path $mingwMakePath) -and ($exitCode -eq 0)) {
            break
        }

        Write-SetupWarning "MSYS2/GCC/make aun no estan listos despues del intento $attempt. Se reintentara si quedan intentos."
        Start-Sleep -Seconds 2
    }

    if (-not (Test-Path $gccPath)) {
        throw "No se encontro gcc.exe despues de instalar GCC."
    }
    if (-not (Test-Path $makePath)) {
        throw "No se encontro make.exe despues de instalar herramientas para Exercism."
    }
    if (-not (Test-Path $mingwMakePath)) {
        throw "No se encontro mingw32-make.exe despues de instalar herramientas para Exercism."
    }

    Invoke-SetupCommand -FilePath $gccPath -Arguments @("--version") -Description "Validando gcc..." -SoloVerificar:$false
    Invoke-SetupCommand -FilePath $makePath -Arguments @("--version") -Description "Validando make..." -SoloVerificar:$false
    Invoke-SetupCommand -FilePath $mingwMakePath -Arguments @("--version") -Description "Validando mingw32-make..." -SoloVerificar:$false
    Add-UserPath -PathToAdd (Split-Path -Parent $gccPath) -SoloVerificar:$SoloVerificar
    Write-SetupSuccess "GCC disponible: $gccPath"
    Write-SetupSuccess "Make disponible para tests Exercism: $makePath"
}
