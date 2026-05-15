$ErrorActionPreference = 'Continue'
$runExitCode = 1
try {
    Set-Location -LiteralPath 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico'
    $program = Start-Process -FilePath 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\soporte\runtime\_output.exe' -ArgumentList @('--run', 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\soporte\runtime\builds\Bingo_2026-05-12T21-04-22.exe', '--log', 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\usuarios\axel\logs\Bingo\bloque3.log') -WorkingDirectory 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico' -WindowStyle Normal -Wait -PassThru
    if ($null -ne $program.ExitCode) { $runExitCode = $program.ExitCode } else { $runExitCode = 0 }
    if ($runExitCode -ne 0) { Write-Host "[RUN] El programa devolvio codigo $runExitCode." }
    & 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\soporte\scripts\finalizar_intento.bat' 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico' 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\Ejercicios\Bingo.c' 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\usuarios\axel\logs\Bingo\bloque3.log' 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\usuarios\axel\errores.md' 'axel' '99521944+AxelBladelove@users.noreply.github.com' 'intento_axel_2026-05-12T21-04-22_06h38m_exit0' 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\soporte\runtime\run.lock'
} finally {
    Remove-Item -LiteralPath 'C:\Users\AxelBlade\.gemini\antigravity\scratch\estudio-socratico\soporte\runtime\run.lock' -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
}
exit $runExitCode
