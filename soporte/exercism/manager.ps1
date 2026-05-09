param(
    [ValidateSet("status", "catalog", "import", "mark", "test", "submit", "detect")]
    [string]$Action = "catalog",

    [string]$RepoRoot,
    [string]$Provider = "exercism",
    [string]$Slug,
    [ValidateSet("completed", "in_progress")]
    [string]$NewStatus,
    [string]$File,
    [string]$ExercisePath,
    [string]$OutFile,
    [switch]$Force,
    [switch]$Json
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
    $fromScript = Resolve-Path (Join-Path $scriptRoot "..\..")
    return $fromScript.Path
}

function Write-Json {
    param([object]$Value)
    $text = $Value | ConvertTo-Json -Depth 20 -Compress
    if (-not [string]::IsNullOrWhiteSpace($script:JsonOutFile)) {
        $dir = Split-Path -Parent $script:JsonOutFile
        if ($dir -and -not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($script:JsonOutFile, $text, $utf8NoBom)
        return
    }
    Write-Output $text
}

function Set-ObjectProperty {
    param(
        [object]$Target,
        [string]$Name,
        [object]$Value
    )

    $Target | Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force
}

function ConvertTo-SafeFileName {
    param([string]$Value)
    $name = $Value -replace '[<>:"/\\|?*]', '-'
    $name = $name -replace '\s+', ' '
    $name = $name.Trim()
    if ([string]::IsNullOrWhiteSpace($name)) {
        return "Ejercicio"
    }
    return $name
}

function ConvertTo-Slug {
    param([string]$Value)
    $slug = $Value.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $slug = $slug.Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) {
        return "ejercicio"
    }
    return $slug
}

function Get-UserSlug {
    param([string]$Root)

    $config = Join-Path $Root ".estudio_usuario"
    if (Test-Path -LiteralPath $config) {
        $value = (Get-Content -LiteralPath $config -ErrorAction SilentlyContinue | Select-Object -First 1)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return (ConvertTo-Slug $value)
        }
    }

    try {
        $branch = (& git -C $Root branch --show-current 2>$null | Select-Object -First 1)
        if (-not [string]::IsNullOrWhiteSpace($branch)) {
            return (ConvertTo-Slug $branch)
        }
    } catch {
    }

    return (ConvertTo-Slug $env:USERNAME)
}

function Get-GitAuthor {
    param(
        [string]$Root,
        [string]$Slug
    )

    $name = $null
    $email = $null
    $githubUser = $null
    try { $name = (& git -C $Root config --local --get user.name 2>$null | Select-Object -First 1) } catch {}
    try { $email = (& git -C $Root config --local --get user.email 2>$null | Select-Object -First 1) } catch {}
    try { $githubUser = (& git -C $Root config --local --get github.user 2>$null | Select-Object -First 1) } catch {}

    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = if ($githubUser) { $githubUser } else { $Slug }
    }
    if ([string]::IsNullOrWhiteSpace($email)) {
        $mailUser = if ($githubUser) { $githubUser } else { $Slug }
        $email = "$mailUser@users.noreply.github.com"
    }

    return @{
        Name = $name.Trim()
        Email = $email.Trim()
    }
}

function Get-ExercismCli {
    $command = Get-Command exercism -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) {
        return $command.Source
    }

    $candidate = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\exercism.exe"
    if (Test-Path -LiteralPath $candidate) {
        return $candidate
    }

    return $null
}

function Get-ExercismWorkspace {
    param([string]$Cli)

    if (-not $Cli) {
        return $null
    }

    try {
        $workspace = (& $Cli workspace 2>$null | Select-Object -First 1)
        if (-not [string]::IsNullOrWhiteSpace($workspace)) {
            return $workspace.Trim()
        }
    } catch {
    }

    return (Join-Path $env:USERPROFILE "Exercism")
}

function Test-ExercismToken {
    param([string]$Cli)

    if (-not $Cli) {
        return $false
    }

    try {
        $output = & $Cli configure --show 2>&1
        $tokenLine = @($output) | Where-Object { $_ -match '^\s*Token:' } | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($tokenLine)) {
            if ($tokenLine -match '(?i)<not configured>|not configured') {
                return $false
            }
            if ($tokenLine -match '^\s*Token:\s*(?:\(-t,\s*--token\)\s*)?\S+') {
                return $true
            }
        }
    } catch {
    }

    $configPath = Join-Path $env:APPDATA "exercism\user.json"
    if (Test-Path -LiteralPath $configPath) {
        try {
            $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
            return (-not [string]::IsNullOrWhiteSpace($config.token))
        } catch {
        }
    }

    return $false
}

function Get-ExercismToken {
    if (-not [string]::IsNullOrWhiteSpace($env:EXERCISM_TOKEN)) {
        return $env:EXERCISM_TOKEN.Trim()
    }

    $configPath = Join-Path $env:APPDATA "exercism\user.json"
    if (Test-Path -LiteralPath $configPath) {
        try {
            $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace($config.token)) {
                return $config.token.Trim()
            }
        } catch {
        }
    }

    return $null
}

function Get-ExercismApiHeaders {
    $token = Get-ExercismToken
    if ([string]::IsNullOrWhiteSpace($token)) {
        return @{}
    }

    return @{
        Authorization = "Bearer $token"
    }
}

function Get-GeminiApiKey {
    $config = Get-ProjectGeminiConfig
    if ($config -and -not [string]::IsNullOrWhiteSpace($config.apiKey)) {
        return $config.apiKey
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GEMINI_API_KEY)) {
        return $env:GEMINI_API_KEY
    }

    $userValue = [Environment]::GetEnvironmentVariable("GEMINI_API_KEY", "User")
    if (-not [string]::IsNullOrWhiteSpace($userValue)) {
        return $userValue
    }

    $machineValue = [Environment]::GetEnvironmentVariable("GEMINI_API_KEY", "Machine")
    if (-not [string]::IsNullOrWhiteSpace($machineValue)) {
        return $machineValue
    }

    return $null
}

function Get-GeminiModel {
    $config = Get-ProjectGeminiConfig
    if ($config -and -not [string]::IsNullOrWhiteSpace($config.model)) {
        return $config.model
    }

    if ($env:GEMINI_MODEL) {
        return $env:GEMINI_MODEL
    }

    return "gemini-2.5-flash-lite"
}

function Get-ProjectGeminiConfig {
    if ([string]::IsNullOrWhiteSpace($script:ResolvedRepoRoot)) {
        return $null
    }

    $paths = @(
        (Join-Path $script:ResolvedRepoRoot "soporte\exercism\config.local.json"),
        (Join-Path $script:ResolvedRepoRoot "soporte\exercism\config.json"),
        (Join-Path $script:ResolvedRepoRoot ".estudio_exercism.local.json")
    )

    foreach ($path in $paths) {
        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        try {
            $config = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            $gemini = if ($config.gemini) { $config.gemini } else { $config }
            $apiKey = $null
            $model = $null
            if (-not [string]::IsNullOrWhiteSpace($gemini.apiKey)) { $apiKey = $gemini.apiKey }
            elseif (-not [string]::IsNullOrWhiteSpace($gemini.geminiApiKey)) { $apiKey = $gemini.geminiApiKey }
            elseif (-not [string]::IsNullOrWhiteSpace($gemini.GEMINI_API_KEY)) { $apiKey = $gemini.GEMINI_API_KEY }

            if (-not [string]::IsNullOrWhiteSpace($gemini.model)) { $model = $gemini.model }
            elseif (-not [string]::IsNullOrWhiteSpace($gemini.geminiModel)) { $model = $gemini.geminiModel }

            if ((-not [string]::IsNullOrWhiteSpace($apiKey)) -or (-not [string]::IsNullOrWhiteSpace($model))) {
                return [pscustomobject]@{
                    apiKey = $apiKey
                    model = $model
                    source = $path
                }
            }
        } catch {
        }
    }

    return $null
}

function Get-LocalExerciseMetas {
    param([string]$Root)

    $exercisesRoot = Join-Path $Root "Ejercicios"
    if (-not (Test-Path -LiteralPath $exercisesRoot)) {
        return @{}
    }

    $result = @{}
    Get-ChildItem -LiteralPath $exercisesRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $metaPath = Join-Path $_.FullName ".estudio-exercism.json"
        if (Test-Path -LiteralPath $metaPath) {
            try {
                $meta = Get-Content -LiteralPath $metaPath -Raw | ConvertFrom-Json
                $key = ("{0}:{1}" -f $meta.provider, $meta.slug)
                $result[$key] = @{
                    Folder = $_.FullName
                    Meta = $meta
                }
            } catch {
            }
        }
    }

    return $result
}

function Get-StaticCatalog {
    param(
        [string]$Root,
        [string]$CatalogName
    )

    $path = Join-Path $Root ("soporte\exercism\catalogs\" + $CatalogName + ".json")
    if (-not (Test-Path -LiteralPath $path)) {
        return @()
    }

    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $raw = $text | ConvertFrom-Json
    return @($raw.exercises)
}

function Get-ExercismCatalog {
    $fallback = @(
        [pscustomobject]@{ slug = "hello-world"; title = "Hello World"; difficulty = "easy"; blurb = "Exercism's classic introductory exercise."; icon_url = "" },
        [pscustomobject]@{ slug = "lasagna"; title = "Lasagna"; difficulty = "easy"; blurb = "Learn about basics by helping cook lasagna."; icon_url = "" },
        [pscustomobject]@{ slug = "grains"; title = "Grains"; difficulty = "easy"; blurb = "Calculate grains of wheat on a chessboard."; icon_url = "" },
        [pscustomobject]@{ slug = "collatz-conjecture"; title = "Collatz Conjecture"; difficulty = "easy"; blurb = "Calculate the number of steps to reach 1."; icon_url = "" }
    )

    try {
        $headers = Get-ExercismApiHeaders
        $response = if ($headers.Count -gt 0) {
            Invoke-RestMethod -Uri "https://api.exercism.org/v2/tracks/c/exercises" -Headers $headers -TimeoutSec 15
        } else {
            Invoke-RestMethod -Uri "https://api.exercism.org/v2/tracks/c/exercises" -TimeoutSec 15
        }
        if ($response.exercises) {
            return @($response.exercises)
        }
    } catch {
    }

    return $fallback
}

function Get-ExercismSolutions {
    $headers = Get-ExercismApiHeaders
    if ($headers.Count -eq 0) {
        return @{}
    }

    $solutions = @{}
    try {
        $response = Invoke-RestMethod -Uri "https://api.exercism.org/v2/solutions?track_slug=c" -Headers $headers -TimeoutSec 15
        foreach ($solution in @($response.results)) {
            if ($solution.exercise -and -not [string]::IsNullOrWhiteSpace($solution.exercise.slug)) {
                $solutions[$solution.exercise.slug] = $solution
            }
        }
    } catch {
    }

    return $solutions
}

function Get-Catalog {
    param([string]$Root)

    $local = Get-LocalExerciseMetas -Root $Root
    $remoteSolutions = Get-ExercismSolutions
    $items = @()
    $order = 0

    foreach ($exercise in (Get-ExercismCatalog)) {
        $order++
        $key = "exercism:$($exercise.slug)"
        $localEntry = $local[$key]
        $remoteSolution = $remoteSolutions[$exercise.slug]
        $status = if ($localEntry) { $localEntry.Meta.status } else { "available" }
        if ([string]::IsNullOrWhiteSpace($status)) { $status = "imported" }
        if ($remoteSolution) {
            if (-not [string]::IsNullOrWhiteSpace($remoteSolution.completed_at)) {
                $status = "completed"
            } elseif (-not [string]::IsNullOrWhiteSpace($remoteSolution.status)) {
                $status = "in_progress"
            }
        }

        $items += [pscustomobject]@{
            provider = "exercism"
            providerName = "Exercism C"
            slug = $exercise.slug
            title = $exercise.title
            folderName = (ConvertTo-SafeFileName $exercise.title)
            difficulty = $exercise.difficulty
            blurb = $exercise.blurb
            iconUrl = $exercise.icon_url
            status = $status
            imported = [bool]$localEntry
            folder = if ($localEntry) { $localEntry.Folder } else { $null }
            topics = @()
            order = $order
            recommended = [bool]$exercise.is_recommended
            unlocked = if ($null -ne $exercise.is_unlocked) { [bool]$exercise.is_unlocked } else { $true }
            supportsTests = $true
            supportsSubmit = $true
        }
    }

    foreach ($catalogName in @("learn-c", "alejandro")) {
        $providerName = if ($catalogName -eq "learn-c") { "learn-c.org" } else { "PDF Alejandro Liz" }
        $order = 0
        foreach ($exercise in (Get-StaticCatalog -Root $Root -CatalogName $catalogName)) {
            $order++
            $key = "${catalogName}:$($exercise.slug)"
            $localEntry = $local[$key]
            $items += [pscustomobject]@{
                provider = $catalogName
                providerName = $providerName
                slug = $exercise.slug
                title = $exercise.title
                folderName = (ConvertTo-SafeFileName $exercise.title)
                difficulty = $exercise.difficulty
                blurb = $exercise.blurb
                iconUrl = $exercise.iconUrl
                status = if ($localEntry) { $localEntry.Meta.status } else { "available" }
                imported = [bool]$localEntry
                folder = if ($localEntry) { $localEntry.Folder } else { $null }
                topics = @($exercise.topics)
                order = $order
                supportsTests = $false
                supportsSubmit = $false
            }
        }
    }

    $cli = Get-ExercismCli
    return [pscustomobject]@{
        generatedAt = (Get-Date).ToString("o")
        exercismCli = [pscustomobject]@{
            available = [bool]$cli
            path = $cli
            workspace = if ($cli) { Get-ExercismWorkspace -Cli $cli } else { $null }
            tokenConfigured = if ($cli) { Test-ExercismToken -Cli $cli } else { $false }
        }
        exercises = $items
    }
}

function Invoke-GeminiTranslation {
    param(
        [string]$Markdown,
        [string]$Title
    )

    $apiKey = Get-GeminiApiKey
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        return @"
# $Title

> Traduccion automatica pendiente.

Configura la variable de entorno `GEMINI_API_KEY` y vuelve a importar este ejercicio para generar las instrucciones en espanol.

Mientras tanto, usa los tests del ejercicio como guia de comportamiento esperado.
"@
    }

    $model = Get-GeminiModel
    $systemPrompt = @"
Eres un traductor tecnico para estudiantes de programacion en C.
Devuelve unicamente la traduccion solicitada.
No saludes, no expliques lo que hiciste, no anadas introducciones, no cierres con notas y no uses frases como "Aqui tienes".
Conserva Markdown, tablas, listas, nombres de funciones, nombres de archivos y bloques de codigo.
No resuelvas el ejercicio ni agregues pistas.
"@
    $prompt = @"
Traduce al espanol latinoamericano el siguiente README de un ejercicio de programacion.
Titulo del ejercicio: $Title

$Markdown
"@

    $body = @{
        systemInstruction = @{
            parts = @(
                @{ text = $systemPrompt }
            )
        }
        contents = @(
            @{
                role = "user"
                parts = @(
                    @{ text = $prompt }
                )
            }
        )
        generationConfig = @{
            temperature = 0.1
        }
    } | ConvertTo-Json -Depth 20

    $uri = "https://generativelanguage.googleapis.com/v1beta/models/$model`:generateContent?key=$apiKey"
    $response = Invoke-RestMethod -Uri $uri -Method Post -ContentType "application/json" -Body $body -TimeoutSec 90
    $text = $response.candidates[0].content.parts[0].text
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Gemini no devolvio texto traducido."
    }
    return (Clear-TranslationText -Text $text -Title $Title)
}

function Clear-TranslationText {
    param(
        [string]$Text,
        [string]$Title
    )

    $clean = $Text.Trim()
    if ($clean -match '(?s)^```(?:markdown|md)?\s*(.*?)\s*```$') {
        $clean = $Matches[1].Trim()
    }

    $lines = @($clean -split "`r?`n")
    $headingIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*#\s+') {
            $headingIndex = $i
            break
        }
    }

    if ($headingIndex -gt 0) {
        $prefix = ($lines[0..($headingIndex - 1)] -join " ").Trim()
        if ($prefix -match '(?i)aqui tienes|traducci[oó]n|conservando|solicitad') {
            $clean = ($lines[$headingIndex..($lines.Count - 1)] -join "`n").Trim()
        }
    }

    return $clean
}

function ConvertTo-CCommentBlock {
    param(
        [string]$Markdown,
        [string]$Title
    )

    $safe = $Markdown -replace '\*/', '* /'
    return @"
/*
Estudio Socratico - instrucciones traducidas
Ejercicio: $Title

$safe
*/

"@
}

function Get-SolutionFiles {
    param([string]$ExerciseRoot)

    $configCandidates = @(
        (Join-Path $ExerciseRoot ".exercism\config.json"),
        (Join-Path $ExerciseRoot ".estudio-exercism\support\.exercism\config.json")
    )
    foreach ($configPath in $configCandidates) {
        if (-not (Test-Path -LiteralPath $configPath)) {
            continue
        }

        try {
            $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
            if ($config.files.solution) {
                return @($config.files.solution)
            }
        } catch {
        }
    }

    $metadataPath = Join-Path $ExerciseRoot ".exercism\metadata.json"
    if (Test-Path -LiteralPath $metadataPath) {
        try {
            $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
            if ($metadata.files.solution) {
                return @($metadata.files.solution)
            }
        } catch {
        }
    }

    return @(Get-ChildItem -LiteralPath $ExerciseRoot -Filter "*.c" -File | Where-Object {
        $_.Name -notmatch 'test|vendor'
    } | Select-Object -ExpandProperty Name)
}

function Get-ExercismSupportRoot {
    param(
        [string]$ExerciseRoot,
        [AllowNull()][object]$Meta
    )

    if ($Meta -and -not [string]::IsNullOrWhiteSpace($Meta.supportRoot)) {
        $candidate = Join-Path $ExerciseRoot $Meta.supportRoot
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $compact = Join-Path $ExerciseRoot ".estudio-exercism\support"
    if (Test-Path -LiteralPath $compact) {
        return $compact
    }

    return $ExerciseRoot
}

function Sync-SolutionFilesToSupport {
    param(
        [string]$ExerciseRoot,
        [string]$SupportRoot,
        [string[]]$SolutionFiles
    )

    foreach ($relative in $SolutionFiles) {
        $source = Join-Path $ExerciseRoot $relative
        $destination = Join-Path $SupportRoot $relative
        if (-not (Test-Path -LiteralPath $source)) {
            continue
        }
        if ([string]::Equals($source, $destination, [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $destinationDir = Split-Path -Parent $destination
        if ($destinationDir -and -not (Test-Path -LiteralPath $destinationDir)) {
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
        }
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

function Ensure-ExercismMakeShim {
    param([string]$SupportRoot)

    $shim = Join-Path $SupportRoot "make.cmd"
    $msysMake = "C:\msys64\usr\bin\make.exe"
    if (Test-Path -LiteralPath $msysMake) {
        if (Test-Path -LiteralPath $shim) {
            Remove-Item -LiteralPath $shim -Force
        }
        return
    }

    if (Get-Command make -ErrorAction SilentlyContinue) {
        return
    }

    $makeCandidates = @(
        "C:\msys64\mingw64\bin\mingw32-make.exe"
    )
    $makePath = $makeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($makePath)) {
        return
    }

    $content = @"
@echo off
"$makePath" %*
"@
    Set-Content -LiteralPath $shim -Value $content -Encoding ascii
}

function Add-TranslatedHeaderToSolution {
    param(
        [string]$ExerciseRoot,
        [string[]]$SolutionFiles,
        [string]$TranslatedReadme,
        [string]$Title
    )

    $comment = ConvertTo-CCommentBlock -Markdown $TranslatedReadme -Title $Title
    foreach ($relative in $SolutionFiles) {
        if ($relative -notlike "*.c") {
            continue
        }

        $path = Join-Path $ExerciseRoot $relative
        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        $current = Get-Content -LiteralPath $path -Raw
        if ($current -match 'Estudio Socratico - instrucciones traducidas') {
            continue
        }

        Set-Content -LiteralPath $path -Value ($comment + $current) -Encoding utf8
    }
}

function Save-Progress {
    param(
        [string]$Root,
        [string]$Provider,
        [string]$Slug,
        [string]$Title,
        [string]$Status,
        [string]$Folder
    )

    $userSlug = Get-UserSlug -Root $Root
    $dir = Join-Path $Root ("usuarios\" + $userSlug + "\exercism")
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $path = Join-Path $dir "progreso.json"
    $data = @{}
    if (Test-Path -LiteralPath $path) {
        try {
            $existing = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            foreach ($p in $existing.PSObject.Properties) {
                $data[$p.Name] = $p.Value
            }
        } catch {
        }
    }

    $key = "$Provider`:$Slug"
    $data[$key] = [pscustomobject]@{
        provider = $Provider
        slug = $Slug
        title = $Title
        status = $Status
        folder = $Folder
        updatedAt = (Get-Date).ToString("o")
    }

    $data | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding utf8
}

function Import-ExercismExercise {
    param(
        [string]$Root,
        [string]$ExerciseSlug,
        [switch]$Overwrite
    )

    $cli = Get-ExercismCli
    if (-not $cli) {
        throw "No se encontro Exercism CLI. Ejecuta setup\instalar.cmd o instala Exercism.CLI con winget."
    }

    $catalog = Get-ExercismCatalog
    $exercise = $catalog | Where-Object { $_.slug -eq $ExerciseSlug } | Select-Object -First 1
    if (-not $exercise) {
        throw "No se encontro el ejercicio '$ExerciseSlug' en el track C de Exercism."
    }

    $workspace = Get-ExercismWorkspace -Cli $cli
    $source = Join-Path $workspace ("c\" + $ExerciseSlug)
    if ((-not (Test-Path -LiteralPath $source)) -or $Overwrite) {
        $downloadArgs = @("download", "--track", "c", "--exercise", $ExerciseSlug)
        if ($Overwrite) {
            $downloadArgs += "--force"
        }

        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $downloadOutput = & $cli @downloadArgs 2>&1
            $downloadExitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        if ($downloadExitCode -ne 0) {
            throw "exercism download fallo para '$ExerciseSlug'. $($downloadOutput -join ' ')"
        }
    }

    if (-not (Test-Path -LiteralPath $source)) {
        throw "No encontre el ejercicio descargado en $source."
    }

    $folderName = ConvertTo-SafeFileName $exercise.title
    $target = Join-Path $Root ("Ejercicios\" + $folderName)
    if ((Test-Path -LiteralPath $target) -and (-not $Overwrite)) {
        throw "Ya existe $target. Usa Force si quieres reemplazarlo."
    }

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $supportRelative = ".estudio-exercism\support"
    $supportRoot = Join-Path $target $supportRelative
    New-Item -ItemType Directory -Path $supportRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $supportRoot -Recurse -Force
    }

    $readmePath = Join-Path $supportRoot "README.md"
    $originalReadme = if (Test-Path -LiteralPath $readmePath) { Get-Content -LiteralPath $readmePath -Raw } else { "# $($exercise.title)`n`n$($exercise.blurb)" }
    $translated = Invoke-GeminiTranslation -Markdown $originalReadme -Title $exercise.title
    Set-Content -LiteralPath $readmePath -Value $translated -Encoding utf8

    $solutionFiles = Get-SolutionFiles -ExerciseRoot $supportRoot
    foreach ($solution in $solutionFiles) {
        $sourceFile = Join-Path $supportRoot $solution
        $targetFile = Join-Path $target $solution
        if (-not (Test-Path -LiteralPath $sourceFile)) {
            continue
        }

        $targetDir = Split-Path -Parent $targetFile
        if ($targetDir -and -not (Test-Path -LiteralPath $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
        Copy-Item -LiteralPath $sourceFile -Destination $targetFile -Force
    }
    Add-TranslatedHeaderToSolution -ExerciseRoot $target -SolutionFiles $solutionFiles -TranslatedReadme $translated -Title $exercise.title

    $meta = [pscustomobject]@{
        provider = "exercism"
        track = "c"
        slug = $ExerciseSlug
        title = $exercise.title
        folderName = $folderName
        status = "imported"
        difficulty = $exercise.difficulty
        blurb = $exercise.blurb
        iconUrl = $exercise.icon_url
        solutionFiles = $solutionFiles
        supportRoot = $supportRelative
        importedAt = (Get-Date).ToString("o")
        sourceWorkspace = $source
    }
    $meta | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $target ".estudio-exercism.json") -Encoding utf8
    Save-Progress -Root $Root -Provider "exercism" -Slug $ExerciseSlug -Title $exercise.title -Status "imported" -Folder $target

    $openFile = $null
    foreach ($solution in $solutionFiles) {
        if ($solution -like "*.c") {
            $candidate = Join-Path $target $solution
            if (Test-Path -LiteralPath $candidate) {
                $openFile = $candidate
                break
            }
        }
    }

    return [pscustomobject]@{
        ok = $true
        provider = "exercism"
        slug = $ExerciseSlug
        title = $exercise.title
        folder = $target
        openFile = $openFile
    }
}

function Set-ExerciseStatus {
    param(
        [string]$Root,
        [string]$ProviderName,
        [string]$ExerciseSlug,
        [string]$Status
    )

    $local = Get-LocalExerciseMetas -Root $Root
    $key = "${ProviderName}:$ExerciseSlug"
    $entry = $local[$key]
    if (-not $entry) {
        throw "Importa el ejercicio antes de marcarlo como completado o en progreso."
    }

    $metaPath = Join-Path $entry.Folder ".estudio-exercism.json"
    if (-not (Test-Path -LiteralPath $metaPath)) {
        throw "Falta .estudio-exercism.json en $($entry.Folder)."
    }

    $meta = Get-Content -LiteralPath $metaPath -Raw | ConvertFrom-Json
    Set-ObjectProperty -Target $meta -Name "status" -Value $Status
    if ($Status -eq "completed") {
        Set-ObjectProperty -Target $meta -Name "completedAt" -Value (Get-Date).ToString("o")
    } else {
        Set-ObjectProperty -Target $meta -Name "reopenedAt" -Value (Get-Date).ToString("o")
    }
    $meta | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $metaPath -Encoding utf8
    Save-Progress -Root $Root -Provider $ProviderName -Slug $ExerciseSlug -Title $meta.title -Status $Status -Folder $entry.Folder

    return [pscustomobject]@{
        ok = $true
        provider = $ProviderName
        slug = $ExerciseSlug
        title = $meta.title
        status = $Status
        folder = $entry.Folder
    }
}

function Import-TemplateExercise {
    param(
        [string]$Root,
        [string]$ProviderName,
        [string]$ExerciseSlug,
        [switch]$Overwrite
    )

    $catalog = Get-StaticCatalog -Root $Root -CatalogName $ProviderName
    $exercise = $catalog | Where-Object { $_.slug -eq $ExerciseSlug } | Select-Object -First 1
    if (-not $exercise) {
        throw "No se encontro '$ExerciseSlug' en $ProviderName."
    }

    $folderName = ConvertTo-SafeFileName $exercise.title
    $target = Join-Path $Root ("Ejercicios\" + $folderName)
    if ((Test-Path -LiteralPath $target) -and (-not $Overwrite)) {
        throw "Ya existe $target. Usa Force si quieres reemplazarlo."
    }
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null

    $readme = @"
# $($exercise.title)

$($exercise.blurb)

## Objetivo

Resuelve el ejercicio en C usando el flujo de Estudio Socratico. Este proveedor aun no incluye tests automaticos, pero comparte la misma interfaz y metadata para poder filtrarse por temas.

## Temas

$(@($exercise.topics) -join ", ")
"@

    $fileName = (ConvertTo-Slug $exercise.title) + ".c"
    $sourcePath = Join-Path $target $fileName
    Set-Content -LiteralPath (Join-Path $target "README.md") -Value $readme -Encoding utf8
    Set-Content -LiteralPath $sourcePath -Value ((ConvertTo-CCommentBlock -Markdown $readme -Title $exercise.title) + "#include <stdio.h>`n`nint main(void)`n{`n    return 0;`n}`n") -Encoding utf8

    $meta = [pscustomobject]@{
        provider = $ProviderName
        slug = $ExerciseSlug
        title = $exercise.title
        folderName = $folderName
        status = "imported"
        difficulty = $exercise.difficulty
        blurb = $exercise.blurb
        topics = @($exercise.topics)
        solutionFiles = @($fileName)
        importedAt = (Get-Date).ToString("o")
    }
    $meta | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $target ".estudio-exercism.json") -Encoding utf8
    Save-Progress -Root $Root -Provider $ProviderName -Slug $ExerciseSlug -Title $exercise.title -Status "imported" -Folder $target

    return [pscustomobject]@{
        ok = $true
        provider = $ProviderName
        slug = $ExerciseSlug
        title = $exercise.title
        folder = $target
        openFile = $sourcePath
    }
}

function Resolve-ExerciseRoot {
    param(
        [string]$Root,
        [AllowNull()][string]$Path
    )

    $candidate = $Path
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = $ExercisePath
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = $File
    }
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        return $null
    }

    $item = Get-Item -LiteralPath $candidate -ErrorAction SilentlyContinue
    if (-not $item) {
        return $null
    }

    $dir = if ($item.PSIsContainer) { $item.FullName } else { $item.DirectoryName }
    while ($dir -and $dir.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path (Join-Path $dir ".estudio-exercism.json")) {
            return $dir
        }
        if (Test-Path (Join-Path $dir ".exercism\metadata.json")) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }

    return $null
}

function Invoke-LoggedGitCommit {
    param(
        [string]$Root,
        [string]$ExerciseRoot,
        [string]$Log,
        [string]$ErroresFile,
        [string]$Message
    )

    $slug = Get-UserSlug -Root $Root
    $author = Get-GitAuthor -Root $Root -Slug $slug
    $relExercise = $ExerciseRoot.Substring($Root.Length).TrimStart('\', '/')
    $relLog = $Log.Substring($Root.Length).TrimStart('\', '/')
    $relErrores = $ErroresFile.Substring($Root.Length).TrimStart('\', '/')

    try {
        & git -C $Root add -- $relExercise $relLog $relErrores | Out-Null
        & git -C $Root diff --cached --quiet
        if ($LASTEXITCODE -ne 0) {
            & git -C $Root -c "user.name=$($author.Name)" -c "user.email=$($author.Email)" commit -m $Message | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[LOG] Sesion grabada: $Message"
            } else {
                Write-Host "[LOG] No se pudo crear el commit automatico."
            }
        } else {
            Write-Host "[LOG] No habia cambios rastreables para grabar en git."
        }
    } catch {
        Write-Host "[LOG] No se pudo crear el commit automatico: $($_.Exception.Message)"
    }
}

function Invoke-ExercismTest {
    param(
        [string]$Root,
        [string]$Path
    )

    $exerciseRoot = Resolve-ExerciseRoot -Root $Root -Path $Path
    if (-not $exerciseRoot) {
        throw "No se pudo detectar un ejercicio importado desde la ruta indicada."
    }

    $metaPath = Join-Path $exerciseRoot ".estudio-exercism.json"
    $meta = if (Test-Path $metaPath) { Get-Content -LiteralPath $metaPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{ provider = "exercism"; slug = (Split-Path $exerciseRoot -Leaf); title = (Split-Path $exerciseRoot -Leaf) } }
    if ($meta.provider -ne "exercism") {
        Write-Host "[INFO] Este proveedor aun no tiene tests automaticos. Se usara el compilador normal con F9 para el .c."
        return 0
    }

    $cli = Get-ExercismCli
    if (-not $cli) {
        throw "No se encontro Exercism CLI."
    }

    $userSlug = Get-UserSlug -Root $Root
    $supportRoot = Get-ExercismSupportRoot -ExerciseRoot $exerciseRoot -Meta $meta
    $solutionFiles = if ($meta.solutionFiles) { @($meta.solutionFiles) } else { Get-SolutionFiles -ExerciseRoot $supportRoot }
    Sync-SolutionFilesToSupport -ExerciseRoot $exerciseRoot -SupportRoot $supportRoot -SolutionFiles $solutionFiles
    Ensure-ExercismMakeShim -SupportRoot $supportRoot

    $logsDir = Join-Path $Root ("usuarios\" + $userSlug + "\logs\" + (ConvertTo-Slug $meta.title))
    $erroresFile = Join-Path $Root ("usuarios\" + $userSlug + "\errores.md")
    if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir -Force | Out-Null }
    if (-not (Test-Path $erroresFile)) { New-Item -ItemType File -Path $erroresFile -Force | Out-Null }

    $timestamp = Get-Date -Format "yyyy-MM-ddTHH-mm-ss"
    $log = Join-Path $logsDir ("bloque_" + $timestamp + ".log")

    "============================================================" | Add-Content -LiteralPath $log
    "INTENTO EXERCISM: $timestamp" | Add-Content -LiteralPath $log
    "EJERCICIO: $($meta.title)" | Add-Content -LiteralPath $log
    "RUTA: $exerciseRoot" | Add-Content -LiteralPath $log
    "============================================================" | Add-Content -LiteralPath $log
    "[ARCHIVOS C]" | Add-Content -LiteralPath $log
    Get-ChildItem -LiteralPath $exerciseRoot -Filter "*.c" -File | ForEach-Object {
        "---- $($_.Name) ----" | Add-Content -LiteralPath $log
        Get-Content -LiteralPath $_.FullName | Add-Content -LiteralPath $log
    }
    "[EXERCISM TEST]" | Add-Content -LiteralPath $log

    Push-Location $supportRoot
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $previousPath = $env:PATH
        $ErrorActionPreference = "Continue"
        $msysPath = "C:\msys64\usr\bin"
        $mingwPath = "C:\msys64\mingw64\bin"
        $env:PATH = "$supportRoot;$msysPath;$mingwPath;$env:PATH"
        try {
            $output = & $cli test 2>&1
            $exitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
            $env:PATH = $previousPath
        }
    } finally {
        Pop-Location
    }

    $output | ForEach-Object {
        Write-Host $_
        $_ | Add-Content -LiteralPath $log
    }
    "[EXIT CODE: $exitCode]" | Add-Content -LiteralPath $log

    $newStatus = if ($exitCode -eq 0) { "tests_passed" } else { "tests_failed" }
    if (Test-Path -LiteralPath $metaPath) {
        Set-ObjectProperty -Target $meta -Name "status" -Value $newStatus
        Set-ObjectProperty -Target $meta -Name "lastTestAt" -Value (Get-Date).ToString("o")
        Set-ObjectProperty -Target $meta -Name "lastTestExitCode" -Value $exitCode
        $meta | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $metaPath -Encoding utf8
    }
    Save-Progress -Root $Root -Provider "exercism" -Slug $meta.slug -Title $meta.title -Status $newStatus -Folder $exerciseRoot

    $message = "intento_${userSlug}_${timestamp}_exercism_exit$exitCode"
    Invoke-LoggedGitCommit -Root $Root -ExerciseRoot $exerciseRoot -Log $log -ErroresFile $erroresFile -Message $message

    return $exitCode
}

function Invoke-ExercismSubmit {
    param(
        [string]$Root,
        [string]$Path
    )

    $exerciseRoot = Resolve-ExerciseRoot -Root $Root -Path $Path
    if (-not $exerciseRoot) {
        throw "No se pudo detectar el ejercicio a enviar."
    }

    $metaPath = Join-Path $exerciseRoot ".estudio-exercism.json"
    if (-not (Test-Path -LiteralPath $metaPath)) {
        throw "Falta .estudio-exercism.json."
    }
    $meta = Get-Content -LiteralPath $metaPath -Raw | ConvertFrom-Json
    if ($meta.provider -ne "exercism") {
        throw "Solo los ejercicios de Exercism se pueden enviar con exercism submit."
    }

    $cli = Get-ExercismCli
    if (-not $cli) {
        throw "No se encontro Exercism CLI."
    }

    $supportRoot = Get-ExercismSupportRoot -ExerciseRoot $exerciseRoot -Meta $meta
    $solutionFiles = if ($meta.solutionFiles) { @($meta.solutionFiles) } else { Get-SolutionFiles -ExerciseRoot $supportRoot }
    Sync-SolutionFilesToSupport -ExerciseRoot $exerciseRoot -SupportRoot $supportRoot -SolutionFiles $solutionFiles

    Push-Location $supportRoot
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            & $cli submit
            $exitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
    } finally {
        Pop-Location
    }

    $newStatus = if ($exitCode -eq 0) { "submitted" } else { "submit_failed" }
    Set-ObjectProperty -Target $meta -Name "status" -Value $newStatus
    Set-ObjectProperty -Target $meta -Name "lastSubmitAt" -Value (Get-Date).ToString("o")
    Set-ObjectProperty -Target $meta -Name "lastSubmitExitCode" -Value $exitCode
    $meta | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $metaPath -Encoding utf8
    Save-Progress -Root $Root -Provider "exercism" -Slug $meta.slug -Title $meta.title -Status $newStatus -Folder $exerciseRoot

    return $exitCode
}

$RepoRoot = Resolve-RepoRoot -Root $RepoRoot
$script:ResolvedRepoRoot = $RepoRoot
$script:JsonOutFile = $OutFile

try {
    switch ($Action) {
        "status" {
            $cli = Get-ExercismCli
            Write-Json ([pscustomobject]@{
                ok = $true
                repoRoot = $RepoRoot
                exercismCli = [pscustomobject]@{
                    available = [bool]$cli
                    path = $cli
                    workspace = if ($cli) { Get-ExercismWorkspace -Cli $cli } else { $null }
                    tokenConfigured = if ($cli) { Test-ExercismToken -Cli $cli } else { $false }
                }
                geminiConfigured = -not [string]::IsNullOrWhiteSpace((Get-GeminiApiKey))
            })
            exit 0
        }
        "catalog" {
            Write-Json (Get-Catalog -Root $RepoRoot)
            exit 0
        }
        "import" {
            if ([string]::IsNullOrWhiteSpace($Slug)) { throw "Debes indicar -Slug." }
            if ($Provider -eq "exercism") {
                $result = Import-ExercismExercise -Root $RepoRoot -ExerciseSlug $Slug -Overwrite:$Force
            } else {
                $result = Import-TemplateExercise -Root $RepoRoot -ProviderName $Provider -ExerciseSlug $Slug -Overwrite:$Force
            }
            Write-Json $result
            exit 0
        }
        "mark" {
            if ([string]::IsNullOrWhiteSpace($Slug)) { throw "Debes indicar -Slug." }
            if ([string]::IsNullOrWhiteSpace($NewStatus)) { throw "Debes indicar -NewStatus." }
            $result = Set-ExerciseStatus -Root $RepoRoot -ProviderName $Provider -ExerciseSlug $Slug -Status $NewStatus
            Write-Json $result
            exit 0
        }
        "test" {
            $path = if ($ExercisePath) { $ExercisePath } else { $File }
            $exitCode = Invoke-ExercismTest -Root $RepoRoot -Path $path
            exit $exitCode
        }
        "submit" {
            $path = if ($ExercisePath) { $ExercisePath } else { $File }
            $exitCode = Invoke-ExercismSubmit -Root $RepoRoot -Path $path
            exit $exitCode
        }
        "detect" {
            $path = if ($ExercisePath) { $ExercisePath } else { $File }
            $exerciseRoot = Resolve-ExerciseRoot -Root $RepoRoot -Path $path
            if (-not $exerciseRoot) {
                Write-Output "IS_EXERCISM=0"
                exit 0
            }
            $metaPath = Join-Path $exerciseRoot ".estudio-exercism.json"
            $provider = "exercism"
            if (Test-Path -LiteralPath $metaPath) {
                try {
                    $meta = Get-Content -LiteralPath $metaPath -Raw | ConvertFrom-Json
                    $provider = $meta.provider
                } catch {
                }
            }
            if ($provider -eq "exercism") {
                Write-Output "IS_EXERCISM=1"
                Write-Output ("EXERCISM_ROOT=" + $exerciseRoot)
            } else {
                Write-Output "IS_EXERCISM=0"
            }
        }
    }
} catch {
    $message = $_.Exception.Message
    if ([string]::IsNullOrWhiteSpace($message)) {
        $message = ($_ | Out-String).Trim()
    }
    if ($Json -or $Action -in @("catalog", "status", "import", "mark")) {
        Write-Json ([pscustomobject]@{
            ok = $false
            error = $message
        })
    } else {
        Write-Error $message
    }
    exit 1
}
