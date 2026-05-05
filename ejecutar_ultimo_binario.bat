@echo off
setlocal

set "LATEST_EXE_FILE=%~dp0.output\latest_exe.txt"

if not exist "%LATEST_EXE_FILE%" (
    echo [ERROR] No hay un binario compilado reciente. Compila primero.
    exit /b 1
)

set "ARCHIVO_EXE="
set /p ARCHIVO_EXE=<"%LATEST_EXE_FILE%"

if "%ARCHIVO_EXE%"=="" (
    echo [ERROR] La ruta del ultimo binario esta vacia. Vuelve a compilar.
    exit /b 1
)

if not exist "%ARCHIVO_EXE%" (
    echo [ERROR] La ruta del ultimo binario no es valida: %ARCHIVO_EXE%
    echo [TIP] Vuelve a compilar para regenerar el ejecutable.
    exit /b 1
)

for %%I in ("%ARCHIVO_EXE%") do set "NOMBRE_BASE=%%~nI"

set "RUNNER_EXE=%~dp0.agent\codeblocks_console_runner.exe"

if exist "%RUNNER_EXE%" (
    start "%NOMBRE_BASE% - Estudio Socratico" "%RUNNER_EXE%" "%ARCHIVO_EXE%"
) else (
    start "%NOMBRE_BASE% — Estudio Socratico" cmd /c "chcp 437 >nul & "%ARCHIVO_EXE%" & echo. & echo ================================ & echo  Programa finalizado. & echo  Presiona cualquier tecla para cerrar esta ventana. & echo ================================ & pause > nul"
)

endlocal
exit /b 0