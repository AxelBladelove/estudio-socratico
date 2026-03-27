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
:: BLOQUE 3: Compilar con gcc — diagnostico + captura completa
:: ============================================================
set "ERRFILE=%~dp0gcc_errors.txt"
set "ARCHIVO_C_CORTO=%~nx1"

echo.
:: === SOLUCION === 
:: El compilador cc1 interno requiere que sus DLLs estén en el PATH de Windows
set "PATH=C:\msys64\mingw64\bin;%PATH%"

:: Nos movemos al directorio del archivo para que los errores de gcc solo muestren el nombre corto
pushd "%DIR_ARCHIVO%"
"gcc.exe" "%ARCHIVO_C_CORTO%" -o "%ARCHIVO_EXE%" -std=c99 -Wall -Wextra > "%ERRFILE%" 2>&1
set "EXIT_CODE=%errorlevel%"
popd

echo [OUTPUT DEL COMPILADOR] >> "%LOG%"
type "%ERRFILE%" >> "%LOG%"
echo [EXIT CODE: %EXIT_CODE%] >> "%LOG%"

:: ============================================================
:: BLOQUE 4: Abrir el programa en ventana separada (estilo Code::Blocks)
:: ============================================================
echo.
if %EXIT_CODE%==0 (
    echo [OK] Compilacion exitosa -^> Abriendo %NOMBRE_BASE%.exe en ventana externa...
    del "%ERRFILE%" >nul 2>&1
    start "%NOMBRE_BASE% — Estudio Socratico" cmd /k ""%ARCHIVO_EXE%" & echo. & echo ================================ & echo  Programa finalizado. & echo  Presiona cualquier tecla para cerrar esta ventana. & echo ================================ & pause > nul"
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
