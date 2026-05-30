@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "ENGINE_DIR=%SCRIPT_DIR%estudio-engine"
set "BIN_DIR=%SCRIPT_DIR%bin"
set "ENGINE_EXE=%ENGINE_DIR%\target\release\estudio-engine.exe"
set "TARGET_EXE=%BIN_DIR%\estudio-engine.exe"
set "EXTENSION_ENGINE_DIR=%SCRIPT_DIR%..\vscode\estudio-exercism\engine"
set "EXTENSION_ENGINE_EXE=%EXTENSION_ENGINE_DIR%\estudio-engine.exe"
set "CATALOG_SOURCE_DIR=%SCRIPT_DIR%..\catalog\fundamentos-c"
set "EXTENSION_CATALOG_DIR=%SCRIPT_DIR%..\vscode\estudio-exercism\catalog\fundamentos-c"

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

if not exist "%EXTENSION_ENGINE_DIR%\" mkdir "%EXTENSION_ENGINE_DIR%\"
copy /Y "%ENGINE_EXE%" "%EXTENSION_ENGINE_EXE%" >nul
if errorlevel 1 (
    echo [ERROR] No se pudo copiar %ENGINE_EXE% a %EXTENSION_ENGINE_EXE%.
    exit /b 1
)

echo [OK] Engine VSIX listo: %EXTENSION_ENGINE_EXE%

if not exist "%CATALOG_SOURCE_DIR%\" (
    echo [ERROR] No se encontro %CATALOG_SOURCE_DIR%
    exit /b 1
)

if not exist "%EXTENSION_CATALOG_DIR%\" mkdir "%EXTENSION_CATALOG_DIR%\"
xcopy "%CATALOG_SOURCE_DIR%\*.json" "%EXTENSION_CATALOG_DIR%\" /Y /I >nul
if errorlevel 1 (
    echo [ERROR] No se pudo copiar el catalogo de Fundamentos C a %EXTENSION_CATALOG_DIR%.
    exit /b 1
)

echo [OK] Catalogo VSIX listo: %EXTENSION_CATALOG_DIR%
