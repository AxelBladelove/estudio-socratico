@echo off
setlocal

cd /d "%~dp0"
call "%~dp0_estudio\setup\instalar.cmd" -Actualizar
exit /b %ERRORLEVEL%
