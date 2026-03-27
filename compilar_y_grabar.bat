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
set "ARCHIVO_EXE=%DIR_ARCHIVO%%NOMBRE_BASE%.exe"
set "LOG=%~dp0compiler_log.txt"

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
:: BLOQUE 3: Compilar con gcc y capturar output en el log
:: ============================================================
echo [OUTPUT DEL COMPILADOR] >> "%LOG%"
"C:\MinGW\bin\gcc.exe" "%ARCHIVO_C%" -o "%ARCHIVO_EXE%" -std=c99 -Wall -Wextra >> "%LOG%" 2>&1
set "EXIT_CODE=%errorlevel%"
echo [EXIT CODE: %EXIT_CODE%] >> "%LOG%"

:: ============================================================
:: BLOQUE 4: Mostrar resultado al estudiante en terminal
:: ============================================================
echo.
if %EXIT_CODE%==0 (
    echo [OK] Compilacion exitosa ^^^> %NOMBRE_BASE%.exe
    echo Ejecutando...
    echo --------------------------------------------------------
    "%ARCHIVO_EXE%"
    echo --------------------------------------------------------
) else (
    echo [COMPILADOR] Errores encontrados:
    echo.
    "C:\MinGW\bin\gcc.exe" "%ARCHIVO_C%" -o "%ARCHIVO_EXE%" -std=c99 -Wall -Wextra
    echo.
    echo Consulta compiler_log.txt para el historial completo.
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
