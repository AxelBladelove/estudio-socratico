@echo off
setlocal

set "SETUP_DIR=%~dp0"
set "SETUP_EXE=%SETUP_DIR%Estudio.Setup.exe"
set "SETUP_PROJECT=%SETUP_DIR%Estudio.Setup\src\Estudio.Setup\Estudio.Setup.csproj"

if not exist "%SETUP_PROJECT%" if exist "%SETUP_EXE%" (
    "%SETUP_EXE%" %*
    exit /b
)

dotnet run --project "%SETUP_PROJECT%" -- %*
exit /b
