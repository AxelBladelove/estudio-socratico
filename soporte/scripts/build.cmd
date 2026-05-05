@echo off
setlocal

if "%~1"=="" (
    call "%~dp0compilar_y_grabar.bat"
) else (
    call "%~dp0compilar_y_grabar.bat" %*
)

endlocal