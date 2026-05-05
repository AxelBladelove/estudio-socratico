@echo off
setlocal

set "BUILD_SCRIPT=%CD%\soporte\scripts\compilar_y_grabar.bat"
if not exist "%BUILD_SCRIPT%" set "BUILD_SCRIPT=%~dp0compilar_y_grabar.bat"

if /i "%~1"=="--inline" (
    set "ESTUDIO_INLINE_RUN=1"
    shift
)

if "%~1"=="" (
    call "%BUILD_SCRIPT%"
) else (
    call "%BUILD_SCRIPT%" "%~1"
)
set "BUILD_EXIT_CODE=%errorlevel%"

endlocal & exit /b %BUILD_EXIT_CODE%
