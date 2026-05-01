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

if not exist "%~dp0logs\" mkdir "%~dp0logs\"
if not exist "%~dp0logs\%NOMBRE_BASE%\" mkdir "%~dp0logs\%NOMBRE_BASE%\"

:: --- Determinar numero de bloque (ventana de 45 minutos) ---
:: Usa logs\<ejercicio>\bloque_actual.txt como marcador interno (ignorado por git).
:: Formato del marcador: "<N> <timestamp-ISO>" donde N es el numero de bloque activo.
:: Si han pasado mas de 45 minutos desde que empezo el bloque, N sube automaticamente.
set "PS_MARKER=%~dp0logs\%NOMBRE_BASE%\bloque_actual.txt"
set "BLOQUE_NUM=1"
for /f "usebackq tokens=*" %%N in (`powershell -NoProfile -Command "$m=$env:PS_MARKER;$now=Get-Date;if(Test-Path -LiteralPath $m){$raw=Get-Content -LiteralPath $m;$i=$raw.IndexOf(' ');$n=[int]$raw.Substring(0,$i);$s=[datetime]$raw.Substring($i+1);if((New-TimeSpan -Start $s -End $now).TotalMinutes -gt 45){$n++;($n.ToString()+' '+$now.ToString('s'))|Set-Content -LiteralPath $m};$n}else{('1 '+$now.ToString('s'))|Set-Content -LiteralPath $m;1}"`) do set "BLOQUE_NUM=%%N"
set "LOG=%~dp0logs\%NOMBRE_BASE%\bloque%BLOQUE_NUM%.log"

:: --- Timestamp para el commit ---
set "TIMESTAMP=%date:~6,4%-%date:~3,2%-%date:~0,2%T%time:~0,2%h%time:~3,2%m%time:~6,2%s"
set "TIMESTAMP=%TIMESTAMP: =0%"

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
git add -A >nul 2>&1
git commit -m "intento_%TIMESTAMP%_exit%EXIT_CODE%" >nul 2>&1

echo.
echo [LOG] Sesion grabada: intento_%TIMESTAMP%_exit%EXIT_CODE%

endlocal
exit /b %EXIT_CODE%
