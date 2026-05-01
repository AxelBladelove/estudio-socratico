@echo off
setlocal EnableDelayedExpansion

set "SETUP_DIR=%~dp0"
set "INSTALLER=%SETUP_DIR%instalar.ps1"
set "PWSH=%ProgramFiles%\PowerShell\7\pwsh.exe"
set "SHOULD_PAUSE=1"
set "SETUP_ARGS="

if not exist "%PWSH%" set "PWSH=powershell.exe"

if /i "%~1"=="/sin-pausa" (
  set "SHOULD_PAUSE=0"
  shift
)

:collect_args
if "%~1"=="" goto run_setup
set "SETUP_ARGS=!SETUP_ARGS! "%~1""
shift
goto collect_args

:run_setup
echo.
echo ==============================================
echo   Estudio Socratico - Instalacion Windows
echo ==============================================
echo.

"%PWSH%" -NoProfile -ExecutionPolicy Bypass -File "%INSTALLER%" !SETUP_ARGS!
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if "%EXIT_CODE%"=="0" (
  echo Instalacion finalizada correctamente.
) else (
  echo La instalacion termino con error. Codigo: %EXIT_CODE%
)
echo.
echo Puedes cerrar esta ventana.
if "%SHOULD_PAUSE%"=="1" pause >nul

endlocal & exit /b %EXIT_CODE%
