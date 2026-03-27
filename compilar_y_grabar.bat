@echo off
:: ============================================================
:: compilar_y_grabar.bat
:: Sistema de Estudio Socratico — Capa de Telemetria Local
:: Cero tokens. Cero internet. Pura maquina local.
:: ============================================================
:: USO: compilar_y_grabar.bat <archivo.c>
:: Llamado automaticamente por tasks.json al presionar F9
:: ============================================================

setlocal enabledelayedexpansion

:: --- Validar argumento ---
if "%~1"=="" (
    echo [ERROR] Debes pasar el nombre del archivo .c como argumento.
    echo Uso: compilar_y_grabar.bat mi_ejercicio.c
    exit /b 1
)

set "ARCHIVO_C=%~1"
set "ARCHIVO_EXE=%~n1.exe"
set "LOG=compiler_log.txt"
set "TIMESTAMP=%date:~6,4%-%date:~3,2%-%date:~0,2%T%time:~0,2%-%time:~3,2%-%time:~6,2%"
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
:: BLOQUE 3: Compilar con gcc y capturar output
:: ============================================================
echo [OUTPUT DEL COMPILADOR] >> "%LOG%"
gcc "%ARCHIVO_C%" -o "%ARCHIVO_EXE%" -Wall -Wextra 2>> "%LOG%"
set "EXIT_CODE=%errorlevel%"
echo [EXIT CODE: %EXIT_CODE%] >> "%LOG%"

:: ============================================================
:: BLOQUE 4: Mostrar resultado al estudiante en terminal
:: ============================================================
echo.
if %EXIT_CODE%==0 (
    echo [OK] Compilacion exitosa: %ARCHIVO_EXE%
    echo Ejecutando...
    echo.
    "%ARCHIVO_EXE%"
) else (
    echo [ERROR] Fallo de compilacion. Revisa el panel de problemas.
    echo Consulta compiler_log.txt para el detalle completo.
)

:: ============================================================
:: BLOQUE 5: Git commit automatico (el corazon de la telemetria)
:: ============================================================
git add -A >nul 2>&1
git commit -m "intento_%TIMESTAMP%_exit%EXIT_CODE%" >nul 2>&1

echo.
echo [LOG] Intento registrado: intento_%TIMESTAMP%_exit%EXIT_CODE%

endlocal
exit /b %EXIT_CODE%
