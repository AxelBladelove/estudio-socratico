param(
    [string]$RepoRoot,
    [string]$ApiKey,
    [string]$Model,
    [switch]$RemoveLocalOverrides
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    param([AllowNull()][string]$Root)

    if ($Root -and (Test-Path (Join-Path $Root "AGENTS.md"))) {
        return (Resolve-Path $Root).Path
    }

    $candidate = (Get-Location).Path
    while ($candidate) {
        if (Test-Path (Join-Path $candidate "AGENTS.md")) {
            return $candidate
        }

        $parent = Split-Path -Parent $candidate
        if ($parent -eq $candidate) {
            break
        }

        $candidate = $parent
    }

    $scriptRoot = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptRoot "..\..")).Path
}

function Read-GeminiConfigFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    try {
        $config = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        if ($config.gemini) {
            return $config.gemini
        }

        return $config
    } catch {
        return $null
    }
}

function Get-GeminiPlainApiKey {
    param([AllowNull()][object]$GeminiConfig)

    if ($null -eq $GeminiConfig) {
        return $null
    }

    if (-not [string]::IsNullOrWhiteSpace($GeminiConfig.apiKey)) {
        return $GeminiConfig.apiKey.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($GeminiConfig.geminiApiKey)) {
        return $GeminiConfig.geminiApiKey.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($GeminiConfig.GEMINI_API_KEY)) {
        return $GeminiConfig.GEMINI_API_KEY.Trim()
    }

    return $null
}

function Get-GeminiModelValue {
    param([AllowNull()][object]$GeminiConfig)

    if ($null -eq $GeminiConfig) {
        return $null
    }

    if (-not [string]::IsNullOrWhiteSpace($GeminiConfig.model)) {
        return $GeminiConfig.model.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($GeminiConfig.geminiModel)) {
        return $GeminiConfig.geminiModel.Trim()
    }

    return $null
}

function Get-EnvironmentVariableValue {
    param([string]$Name)

    $processValue = [Environment]::GetEnvironmentVariable($Name, "Process")
    if (-not [string]::IsNullOrWhiteSpace($processValue)) {
        return $processValue.Trim()
    }

    $userValue = [Environment]::GetEnvironmentVariable($Name, "User")
    if (-not [string]::IsNullOrWhiteSpace($userValue)) {
        return $userValue.Trim()
    }

    $machineValue = [Environment]::GetEnvironmentVariable($Name, "Machine")
    if (-not [string]::IsNullOrWhiteSpace($machineValue)) {
        return $machineValue.Trim()
    }

    return $null
}

function Get-ProtectedDataSupport {
    $protectedDataType = "System.Security.Cryptography.ProtectedData" -as [type]
    $scopeType = "System.Security.Cryptography.DataProtectionScope" -as [type]

    if ($null -eq $protectedDataType -or $null -eq $scopeType) {
        foreach ($assemblyName in @("System.Security.Cryptography.ProtectedData", "System.Security")) {
            try {
                Add-Type -AssemblyName $assemblyName -ErrorAction Stop
            } catch {
            }
        }

        $protectedDataType = "System.Security.Cryptography.ProtectedData" -as [type]
        $scopeType = "System.Security.Cryptography.DataProtectionScope" -as [type]
    }

    if ($null -eq $protectedDataType -or $null -eq $scopeType) {
        throw "No se pudo cargar System.Security.Cryptography.ProtectedData en esta sesion de PowerShell."
    }

    return [pscustomobject]@{
        ProtectedData = $protectedDataType
        CurrentUserScope = $scopeType::CurrentUser
    }
}

function ConvertTo-ProtectedGeminiValue {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "No hay API key para proteger."
    }

    $dpapi = Get-ProtectedDataSupport
    $clearBytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    $protectedBytes = $dpapi.ProtectedData::Protect(
        $clearBytes,
        $null,
        $dpapi.CurrentUserScope
    )

    return [pscustomobject]@{
        scheme = "windows-dpapi-current-user-base64"
        value = [Convert]::ToBase64String($protectedBytes)
    }
}

$resolvedRoot = Resolve-RepoRoot -Root $RepoRoot
$trackedConfigPath = Join-Path $resolvedRoot "soporte\exercism\config.json"
$localConfigPath = Join-Path $resolvedRoot "soporte\exercism\config.local.json"
$legacyLocalConfigPath = Join-Path $resolvedRoot ".estudio_exercism.local.json"

$trackedGemini = Read-GeminiConfigFile -Path $trackedConfigPath
$localGemini = Read-GeminiConfigFile -Path $localConfigPath
$legacyLocalGemini = Read-GeminiConfigFile -Path $legacyLocalConfigPath

$resolvedApiKey = $null
if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
    $resolvedApiKey = $ApiKey.Trim()
}

if ([string]::IsNullOrWhiteSpace($resolvedApiKey)) {
    $resolvedApiKey = Get-GeminiPlainApiKey -GeminiConfig $localGemini
}

if ([string]::IsNullOrWhiteSpace($resolvedApiKey)) {
    $resolvedApiKey = Get-GeminiPlainApiKey -GeminiConfig $legacyLocalGemini
}

if ([string]::IsNullOrWhiteSpace($resolvedApiKey)) {
    $resolvedApiKey = Get-GeminiPlainApiKey -GeminiConfig $trackedGemini
}

if ([string]::IsNullOrWhiteSpace($resolvedApiKey)) {
    $resolvedApiKey = Get-EnvironmentVariableValue -Name "GEMINI_API_KEY"
}

if ([string]::IsNullOrWhiteSpace($resolvedApiKey)) {
    throw "No encontre una Gemini API key en config.local.json, .estudio_exercism.local.json, config.json ni en GEMINI_API_KEY."
}

$resolvedModel = $null
if (-not [string]::IsNullOrWhiteSpace($Model)) {
    $resolvedModel = $Model.Trim()
}

if ([string]::IsNullOrWhiteSpace($resolvedModel)) {
    $resolvedModel = Get-GeminiModelValue -GeminiConfig $localGemini
}

if ([string]::IsNullOrWhiteSpace($resolvedModel)) {
    $resolvedModel = Get-GeminiModelValue -GeminiConfig $legacyLocalGemini
}

if ([string]::IsNullOrWhiteSpace($resolvedModel)) {
    $resolvedModel = Get-GeminiModelValue -GeminiConfig $trackedGemini
}

if ([string]::IsNullOrWhiteSpace($resolvedModel)) {
    $resolvedModel = Get-EnvironmentVariableValue -Name "GEMINI_MODEL"
}

if ([string]::IsNullOrWhiteSpace($resolvedModel)) {
    $resolvedModel = "gemini-2.5-flash-lite"
}

$newConfig = [ordered]@{
    gemini = [ordered]@{
        model = $resolvedModel
        apiKeyProtected = ConvertTo-ProtectedGeminiValue -Value $resolvedApiKey
    }
}

$targetDir = Split-Path -Parent $trackedConfigPath
if (-not (Test-Path -LiteralPath $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$json = $newConfig | ConvertTo-Json -Depth 10
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($trackedConfigPath, $json + [Environment]::NewLine, $utf8NoBom)

if ($RemoveLocalOverrides) {
    foreach ($path in @($localConfigPath, $legacyLocalConfigPath)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

Write-Host "Gemini config protegida en soporte/exercism/config.json"
Write-Host "Formato: windows-dpapi-current-user-base64"
if ($RemoveLocalOverrides) {
    Write-Host "Overrides locales removidos."
} else {
    Write-Host "Los overrides locales siguen permitidos pero ahora estan ignorados por Git."
}