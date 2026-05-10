param(
    [switch]$SoloVerificar,
    [switch]$SinWinget,
    [switch]$SinExtensiones,
    [switch]$Elevado,
    [switch]$Actualizar,
    [switch]$Reconfigurar,
    [switch]$SinOnboarding,
    [switch]$SinRamaUsuario,
    [AllowNull()][string]$UsuarioSlug,
    [AllowNull()][string]$GitHubUsuario,
    [AllowNull()][string]$GitNombre,
    [AllowNull()][string]$GitCorreo
)

$ErrorActionPreference = "Stop"

$SetupDir = Split-Path -Parent $PSCommandPath
$RepoRoot = Split-Path -Parent $SetupDir
$LogDir = Join-Path $RepoRoot "logs\setup"

. (Join-Path $SetupDir "utilidades.ps1")
. (Join-Path $SetupDir "herramientas.ps1")
. (Join-Path $SetupDir "gcc_msys2.ps1")
. (Join-Path $SetupDir "vscode.ps1")
. (Join-Path $SetupDir "proyecto.ps1")

Set-Location $RepoRoot
if ($SoloVerificar) {
    New-SetupDirectory -Path $LogDir -SoloVerificar:$true
} else {
    New-SetupDirectory -Path $LogDir
    Start-SetupLog -Path (Join-Path $LogDir ("instalacion_{0}.log" -f (Get-Date -Format "yyyyMMdd_HHmmss")))
}

try {
    Write-SetupTitle "Estudio Socratico - Setup Windows"

    if ($SoloVerificar) {
        Write-SetupWarning "Modo SoloVerificar activo: no se instalaran paquetes ni se modificara el sistema."
    }

    Assert-ProjectRoot -RepoRoot $RepoRoot

    $toolSpecs = Get-ToolSpecs
    Request-SetupElevationIfNeeded `
        -ToolSpecs $toolSpecs `
        -SetupScript $PSCommandPath `
        -RepoRoot $RepoRoot `
        -SoloVerificar:$SoloVerificar `
        -SinWinget:$SinWinget `
        -Elevado:$Elevado `
        -Actualizar:$Actualizar `
        -Reconfigurar:$Reconfigurar `
        -SinOnboarding:$SinOnboarding `
        -SinRamaUsuario:$SinRamaUsuario `
        -SinExtensiones:$SinExtensiones

    Write-SetupStep "Preparando carpetas del proyecto"
    Ensure-ProjectFolders -RepoRoot $RepoRoot -SoloVerificar:$SoloVerificar

    Write-SetupStep "Verificando herramientas base"
    $tools = Ensure-Tools -ToolSpecs $toolSpecs -SoloVerificar:$SoloVerificar -SinWinget:$SinWinget

    $setupIdentity = Resolve-ProjectOnboarding `
        -RepoRoot $RepoRoot `
        -GitPath $tools["Git"] `
        -GhPath $tools["GitHub CLI"] `
        -UsuarioSlug $UsuarioSlug `
        -GitHubUsuario $GitHubUsuario `
        -GitNombre $GitNombre `
        -GitCorreo $GitCorreo `
        -SoloVerificar:$SoloVerificar `
        -SinOnboarding:$SinOnboarding `
        -Actualizar:$Actualizar `
        -Reconfigurar:$Reconfigurar

    $UsuarioSlug = $setupIdentity["UsuarioSlug"]
    $GitHubUsuario = $setupIdentity["GitHubUsuario"]
    $GitNombre = $setupIdentity["GitNombre"]
    $GitCorreo = $setupIdentity["GitCorreo"]

    Test-ExercismCliConfiguration -ExercismPath $tools["Exercism CLI"] -SoloVerificar:$SoloVerificar -SinOnboarding:$SinOnboarding
    Test-GeminiConfiguration -RepoRoot $RepoRoot -SoloVerificar:$SoloVerificar -SinOnboarding:$SinOnboarding

    Write-SetupStep "Configurando Git local"
    Configure-ProjectGit `
        -RepoRoot $RepoRoot `
        -GitPath $tools["Git"] `
        -UsuarioSlug $UsuarioSlug `
        -GitHubUsuario $GitHubUsuario `
        -GitNombre $GitNombre `
        -GitCorreo $GitCorreo `
        -SoloVerificar:$SoloVerificar

    Write-SetupStep "Preparando carpeta y rama del estudiante"
    Initialize-ProjectUser `
        -RepoRoot $RepoRoot `
        -GitPath $tools["Git"] `
        -UsuarioSlug $UsuarioSlug `
        -SoloVerificar:$SoloVerificar `
        -SinRamaUsuario:$SinRamaUsuario `
        -Actualizar:$Actualizar `
        -Reconfigurar:$Reconfigurar

    Write-SetupStep "Instalando y validando GCC"
    Ensure-GccToolchain -RepoRoot $RepoRoot -SoloVerificar:$SoloVerificar -SinWinget:$SinWinget

    Write-SetupStep "Compilando herramientas locales del proyecto"
    Ensure-AgentRuntimeTools -RepoRoot $RepoRoot -SoloVerificar:$SoloVerificar

    Write-SetupStep "Configurando VS Code"
    Configure-VSCode `
        -RepoRoot $RepoRoot `
        -CodePath $tools["VS Code"] `
        -PowerShellPath $tools["PowerShell"] `
        -SinExtensiones:$SinExtensiones `
        -SoloVerificar:$SoloVerificar

    Write-SetupStep "Validando archivos del workspace"
    Test-WorkspaceJson -RepoRoot $RepoRoot

    Write-SetupStep "Resumen final"
    Write-SetupReport -Tools $tools
    Write-SetupSuccess "Setup completado. Abre un .c en Ejercicios y presiona F9."
    Stop-SetupLog
    if ($Elevado) {
        Read-Host "Proceso elevado completado. Presiona Enter para cerrar esta ventana"
    }
    exit 0
} catch {
    Write-SetupError $_.Exception.Message
    Write-SetupInfo "Log: $script:SetupLogPath"
    Stop-SetupLog
    if ($Elevado) {
        Read-Host "Proceso elevado detenido. Presiona Enter para cerrar esta ventana"
    }
    exit 1
}
