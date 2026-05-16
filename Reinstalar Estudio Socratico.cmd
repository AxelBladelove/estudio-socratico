@echo off
setlocal

cd /d "%~dp0"
call "%~dp0_estudio\setup\Estudio.Setup.cmd" reinstall --tui %*
exit /b
