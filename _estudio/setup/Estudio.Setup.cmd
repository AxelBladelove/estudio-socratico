@echo off
setlocal

set "SETUP_DIR=%~dp0"
set "SETUP_EXE=%SETUP_DIR%Estudio.Setup.exe"
set "SETUP_TEXTUAL_EXE=%SETUP_DIR%Estudio.Setup.Textual.exe"
set "SETUP_TEXTUAL_PY=%SETUP_DIR%textual\setup_textual_app.py"
set "SETUP_PROJECT=%SETUP_DIR%Estudio.Setup\src\Estudio.Setup\Estudio.Setup.csproj"
set "FIRST_ARG=%~1"

if /I "%FIRST_ARG%"=="package" goto run_core
if /I "%FIRST_ARG%"=="pack" goto run_core
if /I "%FIRST_ARG%"=="empaquetar" goto run_core
if /I "%FIRST_ARG%"=="release" goto run_core
if /I "%FIRST_ARG%"=="--help" goto run_core
if /I "%FIRST_ARG%"=="-h" goto run_core
if /I "%FIRST_ARG%"=="/?" goto run_core

if not defined ESTUDIO_SETUP_TEXTUAL_BYPASS if exist "%SETUP_TEXTUAL_EXE%" (
    "%SETUP_TEXTUAL_EXE%" --core "%SETUP_EXE%" %*
    exit /b
)

if not defined ESTUDIO_SETUP_TEXTUAL_BYPASS if exist "%SETUP_TEXTUAL_PY%" (
    call :resolve_textual_python
    if defined SETUP_TEXTUAL_PY_EXE (
        call :run_textual_python %*
        exit /b
    )

    >&2 echo [WARN] No encontre un interprete Python con Textual; continuo con el backend C#.
)

:run_core
if not exist "%SETUP_PROJECT%" if exist "%SETUP_EXE%" (
    "%SETUP_EXE%" %*
    exit /b
)

set "ESTUDIO_SETUP_TEXTUAL_BYPASS=1"
dotnet run --project "%SETUP_PROJECT%" -- %*
exit /b

:resolve_textual_python
set "SETUP_TEXTUAL_PY_EXE="
set "SETUP_TEXTUAL_PY_ARG="

call :probe_textual_python python
if defined SETUP_TEXTUAL_PY_EXE exit /b

call :probe_textual_python py -3
if defined SETUP_TEXTUAL_PY_EXE exit /b

call :probe_textual_python py -3.10
exit /b

:run_textual_python
if "%SETUP_TEXTUAL_PY_ARG%"=="" (
    "%SETUP_TEXTUAL_PY_EXE%" "%SETUP_TEXTUAL_PY%" --core "%SETUP_DIR%Estudio.Setup.cmd" %*
    exit /b
)

"%SETUP_TEXTUAL_PY_EXE%" %SETUP_TEXTUAL_PY_ARG% "%SETUP_TEXTUAL_PY%" --core "%SETUP_DIR%Estudio.Setup.cmd" %*
exit /b

:probe_textual_python
if "%~2"=="" (
    "%~1" -c "import textual" >nul 2>&1
) else (
    "%~1" %~2 -c "import textual" >nul 2>&1
)

if not errorlevel 1 (
    set "SETUP_TEXTUAL_PY_EXE=%~1"
    set "SETUP_TEXTUAL_PY_ARG=%~2"
)
exit /b
