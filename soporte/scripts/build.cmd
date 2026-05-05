@echo off
setlocal

if /i "%~1"=="--inline" (
    set "ESTUDIO_INLINE_RUN=1"
    shift
)

if "%~1"=="" (
    call "%~dp0compilar_y_grabar.bat"
) else (
    call "%~dp0compilar_y_grabar.bat" "%~1"
)
set "BUILD_EXIT_CODE=%errorlevel%"

endlocal & exit /b %BUILD_EXIT_CODE%
