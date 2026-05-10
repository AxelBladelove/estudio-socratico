@echo off
setlocal

cd /d "%~dp0"
call "%~dp0setup\instalar.cmd" -Reconfigurar
exit /b %ERRORLEVEL%
