param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,

    [Parameter(Mandatory = $true)]
    [string]$BaseName,

    [string]$UserSource,

    [Parameter(Mandatory = $true)]
    [string]$OutputLauncherSrc,

    [Parameter(Mandatory = $true)]
    [string]$OutputLauncherExe,

    [Parameter(Mandatory = $true)]
    [string]$ConioSrc,

    [Parameter(Mandatory = $true)]
    [string]$ConioHeader,

    [Parameter(Mandatory = $true)]
    [string]$ConsoleCp437Header,

    [Parameter(Mandatory = $true)]
    [string]$ConioObj
)

$ErrorActionPreference = 'Stop'

function Test-UsableIdentityValue {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }

    $trimmed = $Value.Trim()
    $badValues = @(
        'Estudiante',
        'estudiante',
        'estudiante@estudio.local',
        '2>',
        '2^>',
        'nul',
        'null'
    )

    if ($badValues -contains $trimmed) {
        return $false
    }

    if ($trimmed -match '\^?>|^2\^?>$') {
        return $false
    }

    return $true
}

function Get-GitConfigValue {
    param(
        [string]$Root,
        [string]$Name
    )

    try {
        $value = (& git -C $Root config --local --get $Name 2>$null | Select-Object -First 1)
        if (Test-UsableIdentityValue -Value $value) {
            return $value.Trim()
        }
    }
    catch {
    }

    return $null
}

function Get-UserSlug {
    param(
        [string]$RawName,
        [string]$Root
    )

    $value = $RawName
    if (-not (Test-UsableIdentityValue -Value $value)) {
        $value = Get-GitConfigValue -Root $Root -Name 'github.user'
    }
    if (-not (Test-UsableIdentityValue -Value $value)) {
        $value = Get-GitConfigValue -Root $Root -Name 'user.name'
    }
    if (-not (Test-UsableIdentityValue -Value $value)) {
        $value = $env:USERNAME
    }
    if (-not (Test-UsableIdentityValue -Value $value)) {
        $value = 'usuario'
    }

    $slug = $value.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $slug = $slug.Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) {
        $slug = 'usuario'
    }

    return $slug
}

function Get-GitAuthor {
    param(
        [string]$Root,
        [string]$Slug
    )

    $githubUser = Get-GitConfigValue -Root $Root -Name 'github.user'
    $name = Get-GitConfigValue -Root $Root -Name 'user.name'
    $email = Get-GitConfigValue -Root $Root -Name 'user.email'

    if (-not (Test-UsableIdentityValue -Value $name)) {
        $name = $githubUser
    }
    if (-not (Test-UsableIdentityValue -Value $name)) {
        $name = $Slug
    }

    if (-not (Test-UsableIdentityValue -Value $email)) {
        if (Test-UsableIdentityValue -Value $githubUser) {
            $email = ('{0}@users.noreply.github.com' -f $githubUser)
        }
        else {
            $email = ('{0}@users.noreply.github.com' -f $Slug)
        }
    }

    return @{
        Name = $name
        Email = $email
    }
}

function Get-BlockNumber {
    param(
        [string]$MarkerFile,
        [datetime]$Now
    )

    $markerDir = Split-Path -Parent $MarkerFile
    if ($markerDir -and -not (Test-Path -LiteralPath $markerDir)) {
        New-Item -ItemType Directory -Path $markerDir -Force | Out-Null
    }

    $blockNumber = 1
    $markerValue = '1 ' + $Now.ToString('s')

    if (Test-Path -LiteralPath $MarkerFile) {
        $raw = (Get-Content -LiteralPath $MarkerFile -Raw).Trim()
        if ($raw -match '^(\d+)\s+(.+)$') {
            $blockNumber = [int]$matches[1]
            try {
                $blockStart = [datetime]$matches[2]
                if ((New-TimeSpan -Start $blockStart -End $Now).TotalMinutes -gt 45) {
                    $blockNumber++
                    $markerValue = $blockNumber.ToString() + ' ' + $Now.ToString('s')
                    Set-Content -LiteralPath $MarkerFile -Value $markerValue -NoNewline
                }
            }
            catch {
                Set-Content -LiteralPath $MarkerFile -Value $markerValue -NoNewline
                $blockNumber = 1
            }
        }
        else {
            Set-Content -LiteralPath $MarkerFile -Value $markerValue -NoNewline
        }
    }
    else {
        Set-Content -LiteralPath $MarkerFile -Value $markerValue -NoNewline
    }

    return $blockNumber
}

function Get-ExerciseDuration {
    param(
        [string]$Root,
        [string]$Slug,
        [string]$ExerciseName,
        [datetime]$Now
    )

    $userDir = Join-Path $Root ('usuarios/' + $Slug + '/logs/' + $ExerciseName)
    $legacyDir = Join-Path $Root ('logs/' + $ExerciseName)
    $candidate = $null

    if (Test-Path -LiteralPath $userDir) {
        $candidate = $userDir
    }
    elseif (Test-Path -LiteralPath $legacyDir) {
        $candidate = $legacyDir
    }

    $start = $Now
    if ($candidate) {
        $firstLog = Get-ChildItem -LiteralPath $candidate -Filter 'bloque*.log' | Sort-Object Name | Select-Object -First 1
        if ($firstLog) {
            $start = $firstLog.CreationTime
        }
    }

    $span = New-TimeSpan -Start $start -End $Now
    if ($span.TotalHours -ge 1) {
        return ('{0:00}h{1:00}m' -f [int]$span.TotalHours, $span.Minutes)
    }

    return ('{0:00}m' -f [int][Math]::Max(1, [Math]::Round($span.TotalMinutes)))
}

function Test-RebuildNeeded {
    param(
        [string]$OutputPath,
        [string[]]$DependencyPaths
    )

    if (-not (Test-Path -LiteralPath $OutputPath)) {
        return 1
    }

    $outputTime = (Get-Item -LiteralPath $OutputPath).LastWriteTimeUtc
    foreach ($dependency in $DependencyPaths) {
        if ([string]::IsNullOrWhiteSpace($dependency)) {
            continue
        }
        if (-not (Test-Path -LiteralPath $dependency)) {
            return 1
        }
        if ((Get-Item -LiteralPath $dependency).LastWriteTimeUtc -gt $outputTime) {
            return 1
        }
    }

    return 0
}

$now = Get-Date
$slug = Get-UserSlug -RawName $UserSource -Root $RepoRoot
$gitAuthor = Get-GitAuthor -Root $RepoRoot -Slug $slug
$markerFile = Join-Path $RepoRoot ('usuarios/' + $slug + '/logs/' + $BaseName + '/bloque_actual.txt')
$blockNumber = Get-BlockNumber -MarkerFile $markerFile -Now $now
$duration = Get-ExerciseDuration -Root $RepoRoot -Slug $slug -ExerciseName $BaseName -Now $now

Write-Output ('USUARIO_SLUG=' + $slug)
Write-Output ('GIT_AUTHOR_NAME=' + $gitAuthor.Name)
Write-Output ('GIT_AUTHOR_EMAIL=' + $gitAuthor.Email)
Write-Output ('BLOQUE_NUM=' + $blockNumber)
Write-Output ('TIMESTAMP=' + $now.ToString('yyyy-MM-ddTHH-mm-ss'))
Write-Output ('DURACION_EJERCICIO=' + $duration)
Write-Output ('REBUILD_OUTPUT_LAUNCHER=' + (Test-RebuildNeeded -OutputPath $OutputLauncherExe -DependencyPaths @($OutputLauncherSrc)))
Write-Output ('REBUILD_CONIO_OBJ=' + (Test-RebuildNeeded -OutputPath $ConioObj -DependencyPaths @($ConioSrc, $ConioHeader, $ConsoleCp437Header)))
