@echo off
setlocal

set "SETUP_DIR=%~dp0"
set "SETUP_EXE=%SETUP_DIR%Estudio.Setup.exe"
set "SETUP_TEXTUAL_EXE=%SETUP_DIR%Estudio.Setup.Textual.exe"
set "SETUP_TEXTUAL_PY=%SETUP_DIR%textual\setup_textual_app.py"
set "SETUP_PROJECT=%SETUP_DIR%Estudio.Setup\src\Estudio.Setup\Estudio.Setup.csproj"
set "FIRST_ARG=%~1"

if /I "%FIRST_ARG%"=="package" goto run_core
if /I "%FIRST_ARG%"=="pack" goto run_core
if /I "%FIRST_ARG%"=="empaquetar" goto run_core
if /I "%FIRST_ARG%"=="release" goto run_core
if /I "%FIRST_ARG%"=="--help" goto run_core
if /I "%FIRST_ARG%"=="-h" goto run_core
if /I "%FIRST_ARG%"=="/?" goto run_core

if not defined ESTUDIO_SETUP_TEXTUAL_BYPASS if exist "%SETUP_TEXTUAL_EXE%" (
    "%SETUP_TEXTUAL_EXE%" --core "%SETUP_EXE%" %*
    exit /b
)

if not defined ESTUDIO_SETUP_TEXTUAL_BYPASS if exist "%SETUP_TEXTUAL_PY%" (
    python "%SETUP_TEXTUAL_PY%" --core "%SETUP_DIR%Estudio.Setup.cmd" %*
    exit /b
)

:run_core
if not exist "%SETUP_PROJECT%" if exist "%SETUP_EXE%" (
    "%SETUP_EXE%" %*
    exit /b
)

set "ESTUDIO_SETUP_TEXTUAL_BYPASS=1"
dotnet run --project "%SETUP_PROJECT%" -- %*
exit /b
