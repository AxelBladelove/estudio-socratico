@echo off
setlocal

cd /d "%~dp0"
call "%~dp0setup\instalar.cmd"
exit /b %ERRORLEVEL%
