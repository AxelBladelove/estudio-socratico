param(
    [switch]$SoloVerificar,
    [switch]$SinWinget,
    [switch]$SinExtensiones,
    [switch]$Elevado,
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

    $preGitPath = Resolve-SetupTool `
        -CommandName "git" `
        -Candidates @("$env:ProgramFiles\Git\cmd\git.exe", "${env:ProgramFiles(x86)}\Git\cmd\git.exe")

    $setupIdentity = Resolve-ProjectOnboarding `
        -RepoRoot $RepoRoot `
        -GitPath $preGitPath `
        -UsuarioSlug $UsuarioSlug `
        -GitHubUsuario $GitHubUsuario `
        -GitNombre $GitNombre `
        -GitCorreo $GitCorreo `
        -SoloVerificar:$SoloVerificar `
        -SinOnboarding:$SinOnboarding

    $UsuarioSlug = $setupIdentity["UsuarioSlug"]
    $GitHubUsuario = $setupIdentity["GitHubUsuario"]
    $GitNombre = $setupIdentity["GitNombre"]
    $GitCorreo = $setupIdentity["GitCorreo"]

    $toolSpecs = Get-ToolSpecs
    Request-SetupElevationIfNeeded `
        -ToolSpecs $toolSpecs `
        -SetupScript $PSCommandPath `
        -RepoRoot $RepoRoot `
        -SoloVerificar:$SoloVerificar `
        -SinWinget:$SinWinget `
        -Elevado:$Elevado `
        -UsuarioSlug $UsuarioSlug `
        -GitHubUsuario $GitHubUsuario `
        -GitNombre $GitNombre `
        -GitCorreo $GitCorreo `
        -SinOnboarding:$SinOnboarding `
        -SinRamaUsuario:$SinRamaUsuario `
        -SinExtensiones:$SinExtensiones

    Write-SetupStep "Preparando carpetas del proyecto"
    Ensure-ProjectFolders -RepoRoot $RepoRoot -SoloVerificar:$SoloVerificar

    Write-SetupStep "Verificando herramientas base"
    $tools = Ensure-Tools -ToolSpecs $toolSpecs -SoloVerificar:$SoloVerificar -SinWinget:$SinWinget

    Write-SetupStep "Configurando Git local"
    Configure-ProjectGit `
        -RepoRoot $RepoRoot `
        -GitPath $tools["Git"] `
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
        -SinRamaUsuario:$SinRamaUsuario

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
    exit 0
} catch {
    Write-SetupError $_.Exception.Message
    Write-SetupInfo "Log: $script:SetupLogPath"
    Stop-SetupLog
    exit 1
}
