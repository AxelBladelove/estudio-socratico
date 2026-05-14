@echo off
setlocal

cd /d "%~dp0"
call "%~dp0_estudio\setup\instalar.cmd" -Reconfigurar
exit /b %ERRORLEVEL%
