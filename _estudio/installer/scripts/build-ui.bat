@echo off
REM Build the React UI and copy to App/wwwroot
setlocal

set UI_DIR=%~dp0..\ui
set WWWROOT=%~dp0..\src\EstudioSocratico.Configurator.App\wwwroot

echo [1/3] Installing npm dependencies...
cd /d "%UI_DIR%"
call npm ci
if errorlevel 1 (
    echo ERROR: npm ci failed
    exit /b 1
)

echo [2/3] Building React UI...
call npm run build
if errorlevel 1 (
    echo ERROR: npm run build failed
    exit /b 1
)

echo [3/3] Copying dist to wwwroot...
if exist "%WWWROOT%" rmdir /s /q "%WWWROOT%"
xcopy /E /I /Y "%UI_DIR%\dist" "%WWWROOT%"
if errorlevel 1 (
    echo ERROR: xcopy failed
    exit /b 1
)

echo.
echo UI built and copied to wwwroot successfully.
