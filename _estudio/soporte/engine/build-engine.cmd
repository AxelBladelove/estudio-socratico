@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "ENGINE_DIR=%SCRIPT_DIR%estudio-engine"
set "BIN_DIR=%SCRIPT_DIR%bin"
set "ENGINE_EXE=%ENGINE_DIR%\target\release\estudio-engine.exe"
set "TARGET_EXE=%BIN_DIR%\estudio-engine.exe"

if exist "%USERPROFILE%\.cargo\bin\cargo.exe" (
    set "PATH=%USERPROFILE%\.cargo\bin;%PATH%"
)

where cargo >nul 2>nul
if errorlevel 1 (
    echo [ERROR] No se encontro cargo en PATH.
    exit /b 1
)

if not exist "%ENGINE_DIR%\Cargo.toml" (
    echo [ERROR] No se encontro %ENGINE_DIR%\Cargo.toml
    exit /b 1
)

pushd "%ENGINE_DIR%"
cargo build --release
set "BUILD_EXIT=%ERRORLEVEL%"
popd

if not "%BUILD_EXIT%"=="0" (
    exit /b %BUILD_EXIT%
)

if not exist "%BIN_DIR%\" mkdir "%BIN_DIR%\"
copy /Y "%ENGINE_EXE%" "%TARGET_EXE%" >nul
if errorlevel 1 (
    echo [ERROR] No se pudo copiar %ENGINE_EXE% a %TARGET_EXE%.
    exit /b 1
)

echo [OK] Engine listo: %TARGET_EXE%
