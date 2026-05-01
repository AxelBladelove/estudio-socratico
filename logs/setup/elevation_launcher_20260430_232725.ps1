$ErrorActionPreference = 'Stop'
$logPath = 'C:\Users\axelb\estudio-socratico\logs\setup\elevation_20260430_232725.log'
$scriptPath = 'C:\Users\axelb\estudio-socratico\setup_laptop.ps1'
Add-Content -Path $logPath -Value ('[' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '] Launcher elevado iniciado.')
& $scriptPath -Elevated -ElevationLogPath $logPath -GitName 'Estudiante' -GitEmail 'estudiante@estudio.local'
exit $LASTEXITCODE
