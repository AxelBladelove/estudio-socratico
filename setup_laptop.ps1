# setup_laptop.ps1
# Automatización de Entorno para Estudio Socrático
# NO MODIFICAR EL ARCHIVO .BAT ORIGINAL

Write-Host "--- Iniciando Configuración de Estudio Socrático v4.0 ---" -ForegroundColor Cyan

# 1. Instalar MSYS2 (Compilador GCC)
if (-not (Test-Path "C:\msys64")) {
    Write-Host "[1/3] Instalando MSYS2 (GCC)..." -ForegroundColor Yellow
    winget install MSYS2.MSYS2 --silent --accept-package-agreements --accept-source-agreements
    
    # Esperar a que se cree la carpeta
    while (-not (Test-Path "C:\msys64")) { Start-Sleep -Seconds 2 }
    
    # Instalar el paquete de GCC dentro de MSYS2
    Write-Host "Configurando paquetes de MinGW-w64..." -ForegroundColor Yellow
    & "C:\msys64\usr\bin\bash.exe" -lc "pacman -S --noconfirm mingw-w64-x86_64-gcc"
} else {
    Write-Host "[OK] MSYS2 ya está instalado." -ForegroundColor Green
}

# 2. Instalar Antigravity
Write-Host "[2/3] Instalando Antigravity..." -ForegroundColor Yellow
$installerPath = "$env:TEMP\AntigravitySetup.exe"
$url = "https://antigravity.google/download/latest/win/AntigravitySetup.exe" # URL ficticia de descarga

if (-not (Test-Path "C:\Users\$env:USERNAME\AppData\Local\Programs\Antigravity")) {
    Invoke-WebRequest -Uri $url -OutFile $installerPath
    Start-Process -FilePath $installerPath -ArgumentList "/S" -Wait
    Remove-Item $installerPath
} else {
    Write-Host "[OK] Antigravity ya está instalado." -ForegroundColor Green
}

# 3. Verificar estructura de Git
Write-Host "[3/3] Verificando Repositorio..." -ForegroundColor Yellow
if (Test-Path ".git") {
    Write-Host "[OK] Git configurado correctamente." -ForegroundColor Green
}

Write-Host "`n--- CONFIGURACIÓN COMPLETADA ---" -ForegroundColor Cyan
Write-Host "Ya puedes abrir Antigravity y cargar la carpeta del proyecto."
Write-Host "Recuerda: F9 o Ctrl+Shift+B para compilar."
