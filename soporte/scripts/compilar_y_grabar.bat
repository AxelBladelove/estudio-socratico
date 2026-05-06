@echo off
:: ============================================================
:: compilar_y_grabar.bat
:: Sistema de Estudio Socratico - Capa de Telemetria Local
:: Cero tokens. Cero internet. Pura maquina local.
:: ============================================================
:: USO: compilar_y_grabar.bat <archivo.c>
:: USO Live Share: compilar_y_grabar.bat --inline <archivo.c>
:: Llamado automaticamente por tasks.json al presionar Ctrl+Shift+B
:: Si no se pasa argumento, intenta compilar el .c mas recientemente modificado en Ejercicios/.
:: Esto reduce friccion en terminales compartidas de Live Share.
:: ============================================================

setlocal enabledelayedexpansion

set "RUN_INLINE=%ESTUDIO_INLINE_RUN%"
if /i "%~1"=="--inline" (
    set "RUN_INLINE=1"
    shift
)

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%CD%"
if not exist "%REPO_ROOT%\AGENTS.md" (
    pushd "%SCRIPT_DIR%..\.." >nul
    set "REPO_ROOT=%CD%"
    popd >nul
)

set "INPUT_PATH=%~1"
if "%INPUT_PATH%"=="" (
    for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "$root=Join-Path $env:REPO_ROOT 'Ejercicios'; if(Test-Path -LiteralPath $root){$file=Get-ChildItem -LiteralPath $root -Filter '*.c' | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if($file){$file.FullName}}"`) do set "INPUT_PATH=%%I"
)

if "%INPUT_PATH%"=="" (
    echo [ERROR] Debes pasar un archivo .c o tener al menos un .c dentro de Ejercicios\.
    echo Uso: compilar_y_grabar.bat mi_ejercicio.c
    exit /b 1
)

if /i "%INPUT_PATH:~0,6%"=="vsls:/" (
    set "INPUT_PATH=%INPUT_PATH:~6%"
    set "INPUT_PATH=%INPUT_PATH:/=\%"
    if "%INPUT_PATH:~0,1%"=="\" set "INPUT_PATH=%INPUT_PATH:~1%"
    set "INPUT_PATH=%REPO_ROOT%\%INPUT_PATH%"
)

for %%I in ("%INPUT_PATH%") do (
    set "ARCHIVO_C=%%~fI"
    set "DIR_ARCHIVO=%%~dpI"
    set "NOMBRE_BASE=%%~nI"
    set "EXTENSION=%%~xI"
)

if "%NOMBRE_BASE%"=="" (
    echo [ERROR] No se pudo determinar el nombre del archivo. Verifica que la ruta sea valida.
    exit /b 1
)

if /i not "%EXTENSION%"==".c" (
    echo [ERROR] El archivo debe tener extension .c
    echo Uso: compilar_y_grabar.bat mi_ejercicio.c
    exit /b 1
)

if not exist "%ARCHIVO_C%" (
    echo [ERROR] No se encontro el archivo fuente: %ARCHIVO_C%
    exit /b 1
)

set "RUNTIME_DIR=%REPO_ROOT%\soporte\runtime"
set "OUTPUT_DIR=%RUNTIME_DIR%\builds"
set "LATEST_EXE_FILE=%RUNTIME_DIR%\latest_exe.txt"
set "RUN_LOCK_FILE=%RUNTIME_DIR%\run.lock"
set "CONSOLE_SUPPORT_DIR=%REPO_ROOT%\soporte\consola"
set "BUILD_CONTEXT_SCRIPT=%SCRIPT_DIR%resolve_build_context.ps1"
set "FINALIZE_SCRIPT=%SCRIPT_DIR%finalizar_intento.bat"
set "OUTPUT_LAUNCHER_SRC=%CONSOLE_SUPPORT_DIR%\output_launcher.c"
set "OUTPUT_LAUNCHER_EXE=%RUNTIME_DIR%\_output.exe"
set "USUARIO_CONFIG=%REPO_ROOT%\.estudio_usuario"
set "ERRORES_TEMPLATE=%REPO_ROOT%\errores.template.md"
set "ERRORES_LEGACY=%REPO_ROOT%\errores.md"
set "GCC_EXE="
set "ERRFILE=%RUNTIME_DIR%\gcc_errors.txt"
set "INCLUDE_DIR=%REPO_ROOT%\include"
for %%I in ("%ARCHIVO_C%") do set "ARCHIVO_C_CORTO=%%~nxI"
set "CONIO_SRC=%CONSOLE_SUPPORT_DIR%\conio.c"
set "CONIO_HEADER=%INCLUDE_DIR%\conio.h"
set "CONSOLE_CP437_HEADER=%CONSOLE_SUPPORT_DIR%\console_cp437.h"
set "CONIO_OBJ=%RUNTIME_DIR%\conio_support.o"

if not exist "%RUNTIME_DIR%\" mkdir "%RUNTIME_DIR%\"
if not exist "%OUTPUT_DIR%\" mkdir "%OUTPUT_DIR%\"

set "RUN_LOCK_STATE=FREE"
if exist "%RUN_LOCK_FILE%" (
    for /f "usebackq delims=" %%L in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$lock=$env:RUN_LOCK_FILE; $state='FREE'; if(Test-Path -LiteralPath $lock){$raw=(Get-Content -LiteralPath $lock -Raw).Trim(); $runPid=0; if([int]::TryParse($raw,[ref]$runPid)){if(Get-Process -Id $runPid -ErrorAction SilentlyContinue){$state='ACTIVE'}else{Remove-Item -LiteralPath $lock -Force -ErrorAction SilentlyContinue; $state='STALE'}}elseif((Get-Item -LiteralPath $lock).LastWriteTime -gt (Get-Date).AddSeconds(-30)){$state='ACTIVE'}else{Remove-Item -LiteralPath $lock -Force -ErrorAction SilentlyContinue; $state='STALE'}}; $state"`) do set "RUN_LOCK_STATE=%%L"
)

if /i "%RUN_LOCK_STATE%"=="ACTIVE" (
    echo [RUN] Ya hay una ejecucion abierta de este entorno.
    echo [RUN] Cierra la ventana externa o presiona una tecla en ella antes de compilar de nuevo.
    exit /b 1
)

set "USUARIO_FUENTE="
if exist "%USUARIO_CONFIG%" (
    for /f "usebackq delims=" %%U in ("%USUARIO_CONFIG%") do if not defined USUARIO_FUENTE set "USUARIO_FUENTE=%%U"
)
if "%USUARIO_FUENTE%"=="" set "USUARIO_FUENTE=%ESTUDIO_USUARIO%"

if not exist "%BUILD_CONTEXT_SCRIPT%" (
    echo [ERROR] No existe soporte\scripts\resolve_build_context.ps1. Verifica la integridad del repo.
    exit /b 1
)

if not exist "%FINALIZE_SCRIPT%" (
    echo [ERROR] No existe soporte\scripts\finalizar_intento.bat. Verifica la integridad del repo.
    exit /b 1
)

for /f "usebackq delims=" %%V in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%BUILD_CONTEXT_SCRIPT%" -RepoRoot "%REPO_ROOT%" -BaseName "%NOMBRE_BASE%" -UserSource "%USUARIO_FUENTE%" -OutputLauncherSrc "%OUTPUT_LAUNCHER_SRC%" -OutputLauncherExe "%OUTPUT_LAUNCHER_EXE%" -ConioSrc "%CONIO_SRC%" -ConioHeader "%CONIO_HEADER%" -ConsoleCp437Header "%CONSOLE_CP437_HEADER%" -ConioObj "%CONIO_OBJ%"`) do set "%%V"

set "GIT_COMMIT_NAME=%GIT_AUTHOR_NAME%"
set "GIT_COMMIT_EMAIL=%GIT_AUTHOR_EMAIL%"
if not exist "%USUARIO_CONFIG%" > "%USUARIO_CONFIG%" echo %USUARIO_SLUG%
set "USUARIO_DIR=%REPO_ROOT%\usuarios\%USUARIO_SLUG%"
set "LOGS_ROOT=%USUARIO_DIR%\logs"
set "ERRORES_FILE=%USUARIO_DIR%\errores.md"

if not exist "%REPO_ROOT%\usuarios\" mkdir "%REPO_ROOT%\usuarios\"
if not exist "%USUARIO_DIR%\" mkdir "%USUARIO_DIR%\"
if not exist "%LOGS_ROOT%\" mkdir "%LOGS_ROOT%\"
if not exist "%LOGS_ROOT%\%NOMBRE_BASE%\" mkdir "%LOGS_ROOT%\%NOMBRE_BASE%\"
if not exist "%ERRORES_FILE%" (
    if exist "%ERRORES_LEGACY%" (
        copy /Y "%ERRORES_LEGACY%" "%ERRORES_FILE%" >nul
    ) else if exist "%ERRORES_TEMPLATE%" (
        copy /Y "%ERRORES_TEMPLATE%" "%ERRORES_FILE%" >nul
    ) else (
        > "%ERRORES_FILE%" echo ^<^!-- Archivo de errores inicializado automaticamente. --^>
    )
)

set "LOG=%LOGS_ROOT%\%NOMBRE_BASE%\bloque%BLOQUE_NUM%.log"

set "ARCHIVO_EXE=%OUTPUT_DIR%\%NOMBRE_BASE%_%TIMESTAMP%.exe"
set "COMMIT_MSG=intento_%USUARIO_SLUG%_%TIMESTAMP%_%DURACION_EJERCICIO%_exit0"
set "REL_ARCHIVO_C=%ARCHIVO_C:%REPO_ROOT%\=%"
set "REL_LOG=%LOG:%REPO_ROOT%\=%"
set "REL_ERRORES=%ERRORES_FILE:%REPO_ROOT%\=%"
set "DEFER_COMMIT=0"

:: ============================================================
:: BLOQUE 1: Separador visual en el log
:: ============================================================
echo. >> "%LOG%"
echo ============================================================ >> "%LOG%"
echo INTENTO: %TIMESTAMP% >> "%LOG%"
echo ARCHIVO: %ARCHIVO_C% >> "%LOG%"
echo ============================================================ >> "%LOG%"

:: ============================================================
:: BLOQUE 2: Capturar el codigo fuente completo en el log
:: ============================================================
echo [CODIGO FUENTE] >> "%LOG%"
echo ------------------------------------------------------------ >> "%LOG%"
type "%ARCHIVO_C%" >> "%LOG%" 2>&1
echo. >> "%LOG%"
echo ------------------------------------------------------------ >> "%LOG%"

:: ============================================================
:: BLOQUE 3: Compilar con gcc — diagnostico + captura completa
:: ============================================================

if not exist "%CONIO_SRC%" (
    echo [ERROR] No existe soporte\consola\conio.c. Verifica la integridad del repo.
    exit /b 1
)

if not exist "%OUTPUT_LAUNCHER_SRC%" (
    echo [ERROR] No existe soporte\consola\output_launcher.c. Verifica la integridad del repo.
    exit /b 1
)

echo.
:: === Resolver gcc de forma robusta ===
for /f "usebackq delims=" %%G in (`where.exe gcc.exe 2^>nul`) do if not defined GCC_EXE set "GCC_EXE=%%G"
if not defined GCC_EXE if exist "C:\msys64\mingw64\bin\gcc.exe" set "GCC_EXE=C:\msys64\mingw64\bin\gcc.exe"
if not defined GCC_EXE if exist "C:\msys64\ucrt64\bin\gcc.exe" set "GCC_EXE=C:\msys64\ucrt64\bin\gcc.exe"
if not defined GCC_EXE if exist "C:\msys64\clang64\bin\gcc.exe" set "GCC_EXE=C:\msys64\clang64\bin\gcc.exe"

if not defined GCC_EXE (
    echo [ERROR] No se encontro gcc.exe en PATH ni en rutas comunes de MSYS2.
    echo [TIP] Instala GCC con setup\instalar.cmd o agrega tu compilador al PATH de Windows.
    exit /b 1
)

for %%G in ("%GCC_EXE%") do set "GCC_DIR=%%~dpG"
set "PATH=%GCC_DIR%;%PATH%"

if "%REBUILD_OUTPUT_LAUNCHER%"=="1" (
    echo [INFO] Compilando launcher local soporte\runtime\_output.exe...
    "%GCC_EXE%" "%OUTPUT_LAUNCHER_SRC%" -o "%OUTPUT_LAUNCHER_EXE%" -std=c99 -Wall -Wextra >nul 2>&1
    if exist "%OUTPUT_LAUNCHER_EXE%" (
        echo [OK] Launcher local listo.
    ) else (
        echo [ERROR] No se pudo compilar soporte\runtime\_output.exe.
        exit /b 1
    )
)

if "%REBUILD_CONIO_OBJ%"=="1" (
    echo [INFO] Compilando cache local soporte\runtime\conio_support.o...
    "%GCC_EXE%" "%CONIO_SRC%" -I "%INCLUDE_DIR%" -c -o "%CONIO_OBJ%" -std=c99 -Wall -Wextra >nul 2>&1
    if exist "%CONIO_OBJ%" (
        echo [OK] Cache de conio lista.
    ) else (
        echo [ERROR] No se pudo compilar soporte\runtime\conio_support.o.
        exit /b 1
    )
)

:: Nos movemos al directorio del archivo para que los errores de gcc solo muestren el nombre corto
pushd "%DIR_ARCHIVO%"
"%GCC_EXE%" "%ARCHIVO_C_CORTO%" "%CONIO_OBJ%" -I "%INCLUDE_DIR%" -o "%ARCHIVO_EXE%" -std=c99 -Wall -Wextra > "%ERRFILE%" 2>&1
set "EXIT_CODE=%errorlevel%"
popd
set "COMMIT_MSG=intento_%USUARIO_SLUG%_%TIMESTAMP%_%DURACION_EJERCICIO%_exit%EXIT_CODE%"

echo [OUTPUT DEL COMPILADOR] >> "%LOG%"
type "%ERRFILE%" >> "%LOG%"
for %%A in ("%ERRFILE%") do if %%~zA equ 0 echo (Compilacion limpia. Cero errores y advertencias.) >> "%LOG%"
echo [EXIT CODE: %EXIT_CODE%] >> "%LOG%"

:: ============================================================
:: BLOQUE 4: Ejecutar el programa
:: ============================================================
echo.
if %EXIT_CODE%==0 (
    del "%ERRFILE%" >nul 2>&1
    > "%LATEST_EXE_FILE%" echo %ARCHIVO_EXE%
    if "%RUN_INLINE%"=="1" (
        echo [OK] Compilacion exitosa -^> Ejecutando %NOMBRE_BASE%.exe en esta terminal...
        echo.
        pushd "%REPO_ROOT%"
        "%OUTPUT_LAUNCHER_EXE%" --run "%ARCHIVO_EXE%"
        set "RUN_EXIT_CODE=!errorlevel!"
        popd
        if not "!RUN_EXIT_CODE!"=="0" echo [RUN] El programa devolvio codigo !RUN_EXIT_CODE!.
    ) else (
        set "RUNNER_CMD=%RUNTIME_DIR%\run_%TIMESTAMP%.cmd"
        > "%RUN_LOCK_FILE%" echo STARTING
        > "!RUNNER_CMD!" echo @echo off
        >> "!RUNNER_CMD!" echo setlocal
        >> "!RUNNER_CMD!" echo cd /d "%REPO_ROOT%"
        >> "!RUNNER_CMD!" echo start "%NOMBRE_BASE% - Estudio Socratico" /wait /D "%REPO_ROOT%" "%OUTPUT_LAUNCHER_EXE%" --run "%ARCHIVO_EXE%" --log "%LOG%"
        >> "!RUNNER_CMD!" echo set "RUN_EXIT_CODE=%%errorlevel%%"
        >> "!RUNNER_CMD!" echo if not "%%RUN_EXIT_CODE%%"=="0" echo [RUN] El programa devolvio codigo %%RUN_EXIT_CODE%%.
        >> "!RUNNER_CMD!" echo call "%FINALIZE_SCRIPT%" "%REPO_ROOT%" "%ARCHIVO_C%" "%LOG%" "%ERRORES_FILE%" "%GIT_COMMIT_NAME%" "%GIT_COMMIT_EMAIL%" "%COMMIT_MSG%" "%RUN_LOCK_FILE%"
        >> "!RUNNER_CMD!" echo del "%%~f0" ^>nul 2^>^&1
        >> "!RUNNER_CMD!" echo endlocal ^& exit /b %%RUN_EXIT_CODE%%

        set "RUNNER_PID="
        for /f "usebackq delims=" %%P in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$q=[char]34; $p=Start-Process -FilePath $env:ComSpec -ArgumentList @('/d','/c',($q + $env:RUNNER_CMD + $q)) -WindowStyle Hidden -PassThru; $p.Id"`) do set "RUNNER_PID=%%P"
        if defined RUNNER_PID (
            > "%RUN_LOCK_FILE%" echo !RUNNER_PID!
            set "DEFER_COMMIT=1"
            echo [OK] Compilacion exitosa -^> Abriendo %NOMBRE_BASE%.exe en ventana externa estilo Code::Blocks...
            echo [RUN] VS Code queda libre; el intento se grabara al cerrar la ventana externa.
        ) else (
            del "%RUN_LOCK_FILE%" >nul 2>&1
            echo [ERROR] No se pudo abrir la ventana externa estilo Code::Blocks.
            set "EXIT_CODE=1"
            set "COMMIT_MSG=intento_%USUARIO_SLUG%_%TIMESTAMP%_%DURACION_EJERCICIO%_exit1"
        )
    )
) else (
    echo [COMPILADOR] Errores detectados:
    echo.
    if exist "%ERRFILE%" (
        type "%ERRFILE%"
    ) else (
        echo [ERROR INTERNO] No se pudo crear el archivo de errores. Ruta: %ERRFILE%
    )
    echo.
    echo [TIP] Revisa los errores arriba. Cuando lo soluciones, vuelve a presionar F9.
)

:: ============================================================
:: BLOQUE 5: Git commit automatico (el corazon de la telemetria)
:: ============================================================
if "%DEFER_COMMIT%"=="1" (
    echo [LOG] Commit automatico diferido hasta que cierre la ventana externa.
) else (
    call "%FINALIZE_SCRIPT%" "%REPO_ROOT%" "%ARCHIVO_C%" "%LOG%" "%ERRORES_FILE%" "%GIT_COMMIT_NAME%" "%GIT_COMMIT_EMAIL%" "%COMMIT_MSG%" ""
)

echo.

endlocal & exit /b %EXIT_CODE%
