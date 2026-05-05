@echo off
:: ============================================================
:: compilar_y_grabar.bat
:: Sistema de Estudio Socratico - Capa de Telemetria Local
:: Cero tokens. Cero internet. Pura maquina local.
:: ============================================================
:: USO: compilar_y_grabar.bat <archivo.c>
:: USO Live Share: compilar_y_grabar.bat --inline <archivo.c>
:: Llamado automaticamente por tasks.json al presionar Ctrl+Shift+B
:: Si no se pasa argumento, intenta compilar el .c mas recientemente modificado en Ejercicios/.
:: Esto reduce friccion en terminales compartidas de Live Share.
:: ============================================================

setlocal enabledelayedexpansion

set "RUN_INLINE=%ESTUDIO_INLINE_RUN%"
if /i "%~1"=="--inline" (
    set "RUN_INLINE=1"
    shift
)

set "INPUT_PATH=%~1"
if "%INPUT_PATH%"=="" (
    for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "$root=Join-Path $PWD 'Ejercicios'; if(Test-Path -LiteralPath $root){$file=Get-ChildItem -LiteralPath $root -Filter '*.c' | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if($file){$file.FullName}}"`) do set "INPUT_PATH=%%I"
)

if "%INPUT_PATH%"=="" (
    echo [ERROR] Debes pasar un archivo .c o tener al menos un .c dentro de Ejercicios\.
    echo Uso: compilar_y_grabar.bat mi_ejercicio.c
    exit /b 1
)

if /i "%INPUT_PATH:~0,6%"=="vsls:/" (
    set "INPUT_PATH=%INPUT_PATH:~6%"
    set "INPUT_PATH=%INPUT_PATH:/=\%"
    if "%INPUT_PATH:~0,1%"=="\" set "INPUT_PATH=%INPUT_PATH:~1%"
    set "INPUT_PATH=%~dp0%INPUT_PATH%"
)

for %%I in ("%INPUT_PATH%") do (
    set "ARCHIVO_C=%%~fI"
    set "DIR_ARCHIVO=%%~dpI"
    set "NOMBRE_BASE=%%~nI"
    set "EXTENSION=%%~xI"
)

if "%NOMBRE_BASE%"=="" (
    echo [ERROR] No se pudo determinar el nombre del archivo. Verifica que la ruta sea valida.
    exit /b 1
)

if /i not "%EXTENSION%"==".c" (
    echo [ERROR] El archivo debe tener extension .c
    echo Uso: compilar_y_grabar.bat mi_ejercicio.c
    exit /b 1
)

if not exist "%ARCHIVO_C%" (
    echo [ERROR] No se encontro el archivo fuente: %ARCHIVO_C%
    exit /b 1
)

set "OUTPUT_DIR=%~dp0.output"
set "LATEST_EXE_FILE=%OUTPUT_DIR%\latest_exe.txt"
set "USUARIO_CONFIG=%~dp0.estudio_usuario"
set "ERRORES_TEMPLATE=%~dp0errores.template.md"
set "ERRORES_LEGACY=%~dp0errores.md"
set "GCC_EXE="

set "USUARIO_FUENTE="
if exist "%USUARIO_CONFIG%" (
    for /f "usebackq delims=" %%U in ("%USUARIO_CONFIG%") do if not defined USUARIO_FUENTE set "USUARIO_FUENTE=%%U"
)
if "%USUARIO_FUENTE%"=="" set "USUARIO_FUENTE=%ESTUDIO_USUARIO%"
if "%USUARIO_FUENTE%"=="" (
    for /f "usebackq delims=" %%U in (`powershell -NoProfile -Command "$name=(git config github.user 2^>$null); if([string]::IsNullOrWhiteSpace($name)){ $name=(git config user.name 2^>$null) }; if([string]::IsNullOrWhiteSpace($name)){ $name=$env:USERNAME }; $name"`) do set "USUARIO_FUENTE=%%U"
)
for /f "usebackq delims=" %%U in (`powershell -NoProfile -Command "$raw=$env:USUARIO_FUENTE; if([string]::IsNullOrWhiteSpace($raw)){ $raw='usuario' }; $slug=$raw.ToLowerInvariant() -replace '[^a-z0-9]+','-'; $slug=$slug.Trim('-'); if([string]::IsNullOrWhiteSpace($slug)){ $slug='usuario' }; $slug"`) do set "USUARIO_SLUG=%%U"
if not exist "%USUARIO_CONFIG%" > "%USUARIO_CONFIG%" echo %USUARIO_SLUG%
set "USUARIO_DIR=%~dp0usuarios\%USUARIO_SLUG%"
set "LOGS_ROOT=%USUARIO_DIR%\logs"
set "ERRORES_FILE=%USUARIO_DIR%\errores.md"

if not exist "%OUTPUT_DIR%\" mkdir "%OUTPUT_DIR%\"
if not exist "%~dp0usuarios\" mkdir "%~dp0usuarios\"
if not exist "%USUARIO_DIR%\" mkdir "%USUARIO_DIR%\"
if not exist "%LOGS_ROOT%\" mkdir "%LOGS_ROOT%\"
if not exist "%LOGS_ROOT%\%NOMBRE_BASE%\" mkdir "%LOGS_ROOT%\%NOMBRE_BASE%\"
if not exist "%ERRORES_FILE%" (
    if exist "%ERRORES_LEGACY%" (
        copy /Y "%ERRORES_LEGACY%" "%ERRORES_FILE%" >nul
    ) else if exist "%ERRORES_TEMPLATE%" (
        copy /Y "%ERRORES_TEMPLATE%" "%ERRORES_FILE%" >nul
    ) else (
        > "%ERRORES_FILE%" echo ^<!-- Archivo de errores inicializado automaticamente. --^>
    )
)

:: --- Determinar numero de bloque (ventana de 45 minutos) ---
:: Usa logs\<ejercicio>\bloque_actual.txt como marcador interno (ignorado por git).
:: Formato del marcador: "<N> <timestamp-ISO>" donde N es el numero de bloque activo.
:: Si han pasado mas de 45 minutos desde que empezo el bloque, N sube automaticamente.
set "PS_MARKER=%LOGS_ROOT%\%NOMBRE_BASE%\bloque_actual.txt"
set "BLOQUE_NUM=1"
for /f "usebackq tokens=*" %%N in (`powershell -NoProfile -Command "$m=$env:PS_MARKER;$now=Get-Date;if(Test-Path -LiteralPath $m){$raw=Get-Content -LiteralPath $m;$i=$raw.IndexOf(' ');$n=[int]$raw.Substring(0,$i);$s=[datetime]$raw.Substring($i+1);if((New-TimeSpan -Start $s -End $now).TotalMinutes -gt 45){$n++;($n.ToString()+' '+$now.ToString('s'))|Set-Content -LiteralPath $m};$n}else{('1 '+$now.ToString('s'))|Set-Content -LiteralPath $m;1}"`) do set "BLOQUE_NUM=%%N"
set "LOG=%LOGS_ROOT%\%NOMBRE_BASE%\bloque%BLOQUE_NUM%.log"

:: --- Timestamp para el commit ---
for /f "usebackq delims=" %%T in (`powershell -NoProfile -Command "(Get-Date).ToString('yyyy-MM-ddTHH-mm-ss')"`) do set "TIMESTAMP=%%T"
set "ARCHIVO_EXE=%OUTPUT_DIR%\%NOMBRE_BASE%_%TIMESTAMP%.exe"
for /f "usebackq delims=" %%D in (`powershell -NoProfile -Command "$userDir=Join-Path $PWD ('usuarios/' + $env:USUARIO_SLUG + '/logs/' + $env:NOMBRE_BASE);$legacyDir=Join-Path $PWD ('logs/' + $env:NOMBRE_BASE);$now=Get-Date;$start=$null;$candidate=$null;if(Test-Path -LiteralPath $userDir){$candidate=$userDir}elseif(Test-Path -LiteralPath $legacyDir){$candidate=$legacyDir};if($candidate){$firstLog=Get-ChildItem -LiteralPath $candidate -Filter 'bloque*.log' | Sort-Object Name | Select-Object -First 1;if($firstLog){$start=$firstLog.CreationTime}};if(-not $start){$start=$now};$span=New-TimeSpan -Start $start -End $now;if($span.TotalHours -ge 1){'{0:00}h{1:00}m' -f [int]$span.TotalHours,$span.Minutes}else{'{0:00}m' -f [int][Math]::Max(1,[Math]::Round($span.TotalMinutes))}"`) do set "DURACION_EJERCICIO=%%D"

:: ============================================================
:: BLOQUE 1: Separador visual en el log
:: ============================================================
echo. >> "%LOG%"
echo ============================================================ >> "%LOG%"
echo INTENTO: %TIMESTAMP% >> "%LOG%"
echo ARCHIVO: %ARCHIVO_C% >> "%LOG%"
echo ============================================================ >> "%LOG%"

:: ============================================================
:: BLOQUE 2: Capturar el codigo fuente completo en el log
:: ============================================================
echo [CODIGO FUENTE] >> "%LOG%"
echo ------------------------------------------------------------ >> "%LOG%"
type "%ARCHIVO_C%" >> "%LOG%" 2>&1
echo. >> "%LOG%"
echo ------------------------------------------------------------ >> "%LOG%"

:: ============================================================
:: BLOQUE 3: Compilar con gcc — diagnostico + captura completa
:: ============================================================
set "ERRFILE=%~dp0gcc_errors.txt"
set "INCLUDE_DIR=%~dp0include"
for %%I in ("%ARCHIVO_C%") do set "ARCHIVO_C_CORTO=%%~nxI"
set "SYS_DUMP_SRC=%~dp0.agent\sys_dump_console.c"
set "SYS_DUMP_EXE=%~dp0.agent\sys_dump_console.exe"
set "OUTPUT_LAUNCHER_SRC=%~dp0.agent\output_launcher.c"
set "OUTPUT_LAUNCHER_EXE=%~dp0_output.exe"
set "REBUILD_OUTPUT_LAUNCHER="

echo.
:: === Resolver gcc de forma robusta ===
for /f "usebackq delims=" %%G in (`where.exe gcc.exe 2^>nul`) do if not defined GCC_EXE set "GCC_EXE=%%G"
if not defined GCC_EXE if exist "C:\msys64\mingw64\bin\gcc.exe" set "GCC_EXE=C:\msys64\mingw64\bin\gcc.exe"
if not defined GCC_EXE if exist "C:\msys64\ucrt64\bin\gcc.exe" set "GCC_EXE=C:\msys64\ucrt64\bin\gcc.exe"
if not defined GCC_EXE if exist "C:\msys64\clang64\bin\gcc.exe" set "GCC_EXE=C:\msys64\clang64\bin\gcc.exe"

if not defined GCC_EXE (
    echo [ERROR] No se encontro gcc.exe en PATH ni en rutas comunes de MSYS2.
    echo [TIP] Instala GCC con setup\instalar.cmd o agrega tu compilador al PATH de Windows.
    exit /b 1
)

for %%G in ("%GCC_EXE%") do set "GCC_DIR=%%~dpG"
set "PATH=%GCC_DIR%;%PATH%"

if exist "%OUTPUT_LAUNCHER_SRC%" (
    for /f "usebackq delims=" %%R in (`powershell -NoProfile -Command "$src=Get-Item -LiteralPath $env:OUTPUT_LAUNCHER_SRC; $exe=Get-Item -LiteralPath $env:OUTPUT_LAUNCHER_EXE -ErrorAction SilentlyContinue; if(-not $exe -or $src.LastWriteTimeUtc -gt $exe.LastWriteTimeUtc){'1'}"`) do set "REBUILD_OUTPUT_LAUNCHER=%%R"
    if defined REBUILD_OUTPUT_LAUNCHER (
        echo [INFO] Compilando launcher fijo _output.exe...
        "%GCC_EXE%" "%OUTPUT_LAUNCHER_SRC%" -o "%OUTPUT_LAUNCHER_EXE%" -std=c99 -Wall -Wextra >nul 2>&1
        if exist "%OUTPUT_LAUNCHER_EXE%" (
            echo [OK] Launcher fijo listo.
        ) else (
            echo [ERROR] No se pudo compilar el launcher fijo _output.exe.
            exit /b 1
        )
    )
) else (
    echo [ERROR] No existe .agent\output_launcher.c.
    exit /b 1
)

if not exist "%SYS_DUMP_EXE%" (
    if exist "%SYS_DUMP_SRC%" (
        echo [INFO] Compilando helper local .agent\sys_dump_console.exe...
        "%GCC_EXE%" "%SYS_DUMP_SRC%" -o "%SYS_DUMP_EXE%" -std=c99 -Wall -Wextra >nul 2>&1
        if exist "%SYS_DUMP_EXE%" (
            echo [OK] Helper local listo.
        ) else (
            echo [AVISO] No se pudo compilar .agent\sys_dump_console.exe. Se omitira el volcado de consola.
        )
    ) else (
        echo [AVISO] No existe .agent\sys_dump_console.c. Se omitira el volcado de consola.
    )
)

:: Nos movemos al directorio del archivo para que los errores de gcc solo muestren el nombre corto
pushd "%DIR_ARCHIVO%"
"%GCC_EXE%" "%ARCHIVO_C_CORTO%" -I "%INCLUDE_DIR%" -o "%ARCHIVO_EXE%" -std=c99 -Wall -Wextra > "%ERRFILE%" 2>&1
set "EXIT_CODE=%errorlevel%"
popd

echo [OUTPUT DEL COMPILADOR] >> "%LOG%"
type "%ERRFILE%" >> "%LOG%"
for %%A in ("%ERRFILE%") do if %%~zA equ 0 echo (Compilacion limpia. Cero errores y advertencias.) >> "%LOG%"
echo [EXIT CODE: %EXIT_CODE%] >> "%LOG%"

:: ============================================================
:: BLOQUE 4: Ejecutar el programa
:: ============================================================
echo.
if %EXIT_CODE%==0 (
    del "%ERRFILE%" >nul 2>&1
    > "%LATEST_EXE_FILE%" echo %ARCHIVO_EXE%
    if "%RUN_INLINE%"=="1" (
        echo [OK] Compilacion exitosa -^> Ejecutando %NOMBRE_BASE%.exe en esta terminal...
        echo.
        "%OUTPUT_LAUNCHER_EXE%"
        set "RUN_EXIT_CODE=!errorlevel!"
        echo.
        echo ================================
        echo  Programa finalizado.
        echo ================================
        if exist "%SYS_DUMP_EXE%" (
            echo [AVISO] El volcado automatico de consola solo esta disponible en ventana externa.
            echo [AVISO] La salida interactiva ya quedo visible en esta terminal compartida.
        )
    ) else (
        echo [OK] Compilacion exitosa -^> Abriendo %NOMBRE_BASE%.exe en ventana externa...
        if exist "%SYS_DUMP_EXE%" (
            start "%NOMBRE_BASE% — Estudio Socratico" cmd /c ""%OUTPUT_LAUNCHER_EXE%" & echo. & echo ================================ & echo  Programa finalizado. & "%SYS_DUMP_EXE%" "%LOG%" & echo  Presiona cualquier tecla para cerrar esta ventana. & echo ================================ & pause > nul"
        ) else (
            start "%NOMBRE_BASE% — Estudio Socratico" cmd /c ""%OUTPUT_LAUNCHER_EXE%" & echo. & echo ================================ & echo  Programa finalizado. & echo  [AVISO] No se pudo registrar el volcado de consola en el log. & echo  Presiona cualquier tecla para cerrar esta ventana. & echo ================================ & pause > nul"
        )
    )
) else (
    echo [COMPILADOR] Errores detectados:
    echo.
    if exist "%ERRFILE%" (
        type "%ERRFILE%"
    ) else (
        echo [ERROR INTERNO] No se pudo crear el archivo de errores. Ruta: %ERRFILE%
    )
    echo.
    echo [TIP] Revisa los errores arriba. Cuando lo soluciones, vuelve a presionar F9.
)

:: ============================================================
:: BLOQUE 5: Git commit automatico (el corazon de la telemetria)
:: ============================================================
cd /d "%~dp0"
set "REL_ARCHIVO_C=%ARCHIVO_C:%~dp0=%"
set "REL_LOG=%LOG:%~dp0=%"
set "REL_ERRORES=%ERRORES_FILE:%~dp0=%"

git add -- "%REL_ARCHIVO_C%" "%REL_LOG%" "%REL_ERRORES%" >nul 2>&1
git diff --cached --quiet >nul 2>&1
if errorlevel 1 (
    git commit -m "intento_%USUARIO_SLUG%_%TIMESTAMP%_%DURACION_EJERCICIO%_exit%EXIT_CODE%" >nul 2>&1
    if errorlevel 1 (
        echo [LOG] No se pudo crear el commit automatico. Verifica git status y la configuracion de Git.
    ) else (
        echo [LOG] Sesion grabada: intento_%USUARIO_SLUG%_%TIMESTAMP%_%DURACION_EJERCICIO%_exit%EXIT_CODE%
    )
) else (
    echo [LOG] No habia cambios rastreables para grabar en git.
)

echo.

endlocal
exit /b %EXIT_CODE%
