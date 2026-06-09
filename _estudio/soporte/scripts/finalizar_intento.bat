@echo off
setlocal

set "REPO_ROOT=%~1"
set "ARCHIVO_C=%~2"
set "LOG=%~3"
set "ERRORES_FILE=%~4"
set "GIT_COMMIT_NAME=%~5"
set "GIT_COMMIT_EMAIL=%~6"
set "COMMIT_MSG=%~7"
set "RUN_LOCK_FILE=%~8"

if "%REPO_ROOT%"=="" exit /b 1
cd /d "%REPO_ROOT%" || exit /b 1

if "%GIT_COMMIT_NAME%"=="" for /f "usebackq delims=" %%G in (`git config --local --get user.name 2^>nul`) do if not defined GIT_COMMIT_NAME set "GIT_COMMIT_NAME=%%G"
echo %GIT_COMMIT_EMAIL% | findstr /i /c:"@estudio.local" >nul 2>&1
if "%GIT_COMMIT_EMAIL%"=="" (
    for /f "usebackq delims=" %%G in (`git config --local --get user.email 2^>nul`) do if not defined GIT_COMMIT_EMAIL set "GIT_COMMIT_EMAIL=%%G"
) else if not errorlevel 1 (
    set "GIT_COMMIT_EMAIL="
    for /f "usebackq delims=" %%G in (`git config --local --get user.email 2^>nul`) do if not defined GIT_COMMIT_EMAIL set "GIT_COMMIT_EMAIL=%%G"
)
if "%GIT_COMMIT_NAME%"=="" set "GIT_COMMIT_NAME=Estudio Socratico"
if "%GIT_COMMIT_EMAIL%"=="" set "GIT_COMMIT_EMAIL=estudio@estudio.local"

set "REL_ARCHIVO_C=%ARCHIVO_C:%REPO_ROOT%\=%"
set "REL_LOG=%LOG:%REPO_ROOT%\=%"
set "REL_ERRORES=%ERRORES_FILE:%REPO_ROOT%\=%"

git add -- "%REL_ARCHIVO_C%" "%REL_LOG%" "%REL_ERRORES%" >nul 2>&1
git diff --cached --quiet >nul 2>&1
if errorlevel 1 (
    git -c "user.name=%GIT_COMMIT_NAME%" -c "user.email=%GIT_COMMIT_EMAIL%" commit -m "%COMMIT_MSG%" >nul 2>&1
    if errorlevel 1 (
        echo [LOG] No se pudo crear el commit automatico. Verifica git status y la configuracion de Git.
    ) else (
        echo [LOG] Sesion grabada: %COMMIT_MSG%
    )
) else (
    echo [LOG] No habia cambios rastreables para grabar en git.
)

if not "%RUN_LOCK_FILE%"=="" del "%RUN_LOCK_FILE%" >nul 2>&1

endlocal & exit /b 0
