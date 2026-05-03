@echo off
:: ============================================================
:: compilar_y_grabar.bat
:: Sistema de Estudio Socratico - Capa de Telemetria Local
:: Cero tokens. Cero internet. Pura maquina local.
:: ============================================================
:: USO: compilar_y_grabar.bat <archivo.c>
:: Llamado automaticamente por tasks.json al presionar Ctrl+Shift+B
:: ============================================================

setlocal enabledelayedexpansion

:: --- Validar argumento ---
if "%~1"=="" (
    echo [ERROR] Debes pasar el nombre del archivo .c como argumento.
    echo Uso: compilar_y_grabar.bat mi_ejercicio.c
    exit /b 1
)

:: --- Resolver rutas absolutas correctamente ---
:: %~f1 = ruta completa del archivo .c (aunque se pase relativo)
:: %~dp1 = directorio del archivo (con backslash al final)
:: %~n1  = nombre sin extension
set "ARCHIVO_C=%~f1"
set "DIR_ARCHIVO=%~dp1"
set "NOMBRE_BASE=%~n1"
set "ARCHIVO_EXE=%~dp0_output.exe"
set "USUARIO_CONFIG=%~dp0.estudio_usuario"
set "ERRORES_TEMPLATE=%~dp0errores.template.md"
set "ERRORES_LEGACY=%~dp0errores.md"

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

:: --- Validar nombre base y extension ---
if "%NOMBRE_BASE%"=="" (
    echo [ERROR] No se pudo determinar el nombre del archivo. Verifica que la ruta sea valida.
    exit /b 1
)
if /i not "%~x1"==".c" (
    echo [ERROR] El archivo debe tener extension .c
    echo Uso: compilar_y_grabar.bat mi_ejercicio.c
    exit /b 1
)

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
set "ARCHIVO_C_CORTO=%~nx1"
set "SYS_DUMP_SRC=%~dp0.agent\sys_dump_console.c"
set "SYS_DUMP_EXE=%~dp0.agent\sys_dump_console.exe"

echo.
:: === SOLUCION === 
:: El compilador cc1 interno requiere que sus DLLs esten en el PATH de Windows.
:: Ruta oficial del proyecto: MSYS2 MinGW64.
set "PATH=C:\msys64\mingw64\bin;%PATH%"

if not exist "%SYS_DUMP_EXE%" (
    if exist "%SYS_DUMP_SRC%" (
        echo [INFO] Compilando helper local .agent\sys_dump_console.exe...
        "gcc.exe" "%SYS_DUMP_SRC%" -o "%SYS_DUMP_EXE%" -std=c99 -Wall -Wextra >nul 2>&1
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
"gcc.exe" "%ARCHIVO_C_CORTO%" -o "%ARCHIVO_EXE%" -std=c99 -Wall -Wextra > "%ERRFILE%" 2>&1
set "EXIT_CODE=%errorlevel%"
popd

echo [OUTPUT DEL COMPILADOR] >> "%LOG%"
type "%ERRFILE%" >> "%LOG%"
for %%A in ("%ERRFILE%") do if %%~zA equ 0 echo (Compilacion limpia. Cero errores y advertencias.) >> "%LOG%"
echo [EXIT CODE: %EXIT_CODE%] >> "%LOG%"

:: ============================================================
:: BLOQUE 4: Abrir el programa en ventana separada (estilo Code::Blocks)
:: ============================================================
echo.
if %EXIT_CODE%==0 (
    echo [OK] Compilacion exitosa -^> Abriendo %NOMBRE_BASE%.exe en ventana externa...
    del "%ERRFILE%" >nul 2>&1
    if exist "%SYS_DUMP_EXE%" (
        start "%NOMBRE_BASE% — Estudio Socratico" cmd /c ""%ARCHIVO_EXE%" & echo. & echo ================================ & echo  Programa finalizado. & "%SYS_DUMP_EXE%" "%LOG%" & echo  Presiona cualquier tecla para cerrar esta ventana. & echo ================================ & pause > nul"
    ) else (
        start "%NOMBRE_BASE% — Estudio Socratico" cmd /c ""%ARCHIVO_EXE%" & echo. & echo ================================ & echo  Programa finalizado. & echo  [AVISO] No se pudo registrar el volcado de consola en el log. & echo  Presiona cualquier tecla para cerrar esta ventana. & echo ================================ & pause > nul"
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
