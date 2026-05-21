param(
    [ValidateSet("status", "catalog", "import", "mark", "test", "submit", "validate", "test-window", "validate-window", "reveal-tests", "detect")]
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

function Get-TemplateExerciseFileName {
    param(
        [object]$Exercise,
        [string]$ProviderName,
        [string]$ExerciseSlug
    )

    if ($ProviderName -eq "alejandro") {
        $slug = if (-not [string]::IsNullOrWhiteSpace($Exercise.slug)) { [string]$Exercise.slug } else { $ExerciseSlug }
        $baseName = $slug -replace '^alejandro-', ''
        return ((ConvertTo-Slug $baseName) + ".c")
    }

    if ($Exercise.fileName) {
        return (ConvertTo-SafeFileName $Exercise.fileName)
    }

    return "main.c"
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

function Resolve-UserDataRoot {
    param(
        [string]$Root,
        [string]$Slug,
        [switch]$Create
    )

    $canonical = Join-Path $Root "usuario"
    if (Test-Path -LiteralPath $canonical) {
        return $canonical
    }

    $legacy = Join-Path $Root ("_estudio\usuarios\" + $Slug)
    if (Test-Path -LiteralPath $legacy) {
        if ($Create) {
            Move-Item -LiteralPath $legacy -Destination $canonical
            return $canonical
        }
        return $legacy
    }

    if ($Create) {
        New-Item -ItemType Directory -Path $canonical -Force | Out-Null
    }
    return $canonical
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
        return $null
    }

    return [pscustomobject]@{
        ProtectedData = $protectedDataType
        CurrentUserScope = $scopeType::CurrentUser
    }
}

function ConvertFrom-ProtectedGeminiValue {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    $scheme = "windows-dpapi-current-user-base64"
    $cipherText = $null

    if ($Value -is [string]) {
        $cipherText = $Value
    } else {
        if (-not [string]::IsNullOrWhiteSpace($Value.scheme)) {
            $scheme = $Value.scheme.Trim()
        }

        if (-not [string]::IsNullOrWhiteSpace($Value.value)) {
            $cipherText = $Value.value
        } elseif (-not [string]::IsNullOrWhiteSpace($Value.ciphertext)) {
            $cipherText = $Value.ciphertext
        }
    }

    if ([string]::IsNullOrWhiteSpace($cipherText)) {
        return $null
    }

    if ($scheme -ne "windows-dpapi-current-user-base64") {
        return $null
    }

    try {
        $dpapi = Get-ProtectedDataSupport
        if ($null -eq $dpapi) {
            return $null
        }

        $protectedBytes = [Convert]::FromBase64String($cipherText)
        $clearBytes = $dpapi.ProtectedData::Unprotect(
            $protectedBytes,
            $null,
            $dpapi.CurrentUserScope
        )
        $plainText = [System.Text.Encoding]::UTF8.GetString($clearBytes).Trim()
        if (-not [string]::IsNullOrWhiteSpace($plainText)) {
            return $plainText
        }
    } catch {
    }

    return $null
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

    $paths = @()
    if (-not [string]::IsNullOrWhiteSpace($env:APPDATA)) {
        $paths += (Join-Path $env:APPDATA "EstudioSocratico\config.json")
    }

    $paths += @(
        (Join-Path $script:ResolvedRepoRoot "_estudio\soporte\exercism\config.local.json"),
        (Join-Path $script:ResolvedRepoRoot "_estudio\soporte\exercism\config.json"),
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
            if ($null -ne $gemini.apiKeyProtected) { $apiKey = ConvertFrom-ProtectedGeminiValue -Value $gemini.apiKeyProtected }
            elseif ($null -ne $gemini.protectedApiKey) { $apiKey = ConvertFrom-ProtectedGeminiValue -Value $gemini.protectedApiKey }
            elseif ($null -ne $gemini.protectedGeminiApiKey) { $apiKey = ConvertFrom-ProtectedGeminiValue -Value $gemini.protectedGeminiApiKey }
            elseif (-not [string]::IsNullOrWhiteSpace($gemini.apiKey)) { $apiKey = $gemini.apiKey }
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

    $path = Join-Path $Root ("_estudio\soporte\exercism\catalogs\" + $CatalogName + ".json")
    if (-not (Test-Path -LiteralPath $path)) {
        return @()
    }

    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $raw = $text | ConvertFrom-Json
    return @($raw.exercises)
}

function Get-CatalogStatus {
    param([string]$Root)

    $alejandro = @(Get-StaticCatalog -Root $Root -CatalogName "alejandro")
    $withInstructionSource = @($alejandro | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_.gistInstructionsUrl) -or
        -not [string]::IsNullOrWhiteSpace($_.instructionMarkdown) -or
        -not [string]::IsNullOrWhiteSpace($_.driveFileId)
    })

    return [pscustomobject]@{
        alejandro = [pscustomobject]@{
            available = ($alejandro.Count -gt 0)
            count = $alejandro.Count
            withInstructionSource = $withInstructionSource.Count
        }
    }
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

function Get-ExercismSolutionForSlug {
    param([string]$Slug)

    $solutions = Get-ExercismSolutions
    if ($solutions.ContainsKey($Slug)) {
        return $solutions[$Slug]
    }

    return $null
}

function Get-ExercismSolutionTestsStatus {
    param([AllowNull()][object]$Solution)

    if ($null -eq $Solution) {
        return $null
    }

    foreach ($name in @(
        "published_iteration_head_tests_status",
        "latest_iteration_head_tests_status",
        "head_tests_status",
        "tests_status"
    )) {
        if ($Solution.PSObject.Properties.Name -contains $name) {
            $value = $Solution.$name
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                return "$value"
            }
        }
    }

    return $null
}

function Get-ExercismSolutionUrl {
    param(
        [AllowNull()][object]$Solution,
        [string]$Slug,
        [AllowNull()][string[]]$Output
    )

    foreach ($line in @($Output)) {
        $match = [regex]::Match("$line", "https?://\S+")
        if ($match.Success) {
            return $match.Value.TrimEnd(".", ",", ";", ")")
        }
    }

    if ($Solution) {
        if (-not [string]::IsNullOrWhiteSpace($Solution.private_url)) {
            return "$($Solution.private_url)"
        }
        if (-not [string]::IsNullOrWhiteSpace($Solution.public_url)) {
            return "$($Solution.public_url)"
        }
    }

    return "https://exercism.org/tracks/c/exercises/$Slug"
}

function Wait-ExercismSolutionAfterSubmit {
    param(
        [string]$Slug,
        [AllowNull()][object]$PreviousSolution,
        [int]$Attempts = 8,
        [int]$DelaySeconds = 2
    )

    $previousLastIteratedAt = if ($PreviousSolution) { "$($PreviousSolution.last_iterated_at)" } else { "" }
    $previousUpdatedAt = if ($PreviousSolution) { "$($PreviousSolution.updated_at)" } else { "" }
    $previousIterations = if ($PreviousSolution -and $null -ne $PreviousSolution.num_iterations) { [int]$PreviousSolution.num_iterations } else { -1 }
    $latest = $null
    $sawChanged = (-not $PreviousSolution)
    for ($i = 0; $i -lt $Attempts; $i++) {
        $latest = Get-ExercismSolutionForSlug -Slug $Slug
        if ($latest) {
            $testsStatus = Get-ExercismSolutionTestsStatus -Solution $latest
            $changed = $true
            if ($PreviousSolution) {
                $changed = (($latest.num_iterations -as [int]) -gt $previousIterations) -or
                    ("$($latest.last_iterated_at)" -ne $previousLastIteratedAt) -or
                    ("$($latest.updated_at)" -ne $previousUpdatedAt)
            }
            if ($changed) {
                $sawChanged = $true
            }

            if ($changed -and (
                (-not [string]::IsNullOrWhiteSpace($latest.completed_at)) -or
                ($testsStatus -match '(?i)passed|failed|errored|error')
            )) {
                return $latest
            }
        }

        Start-Sleep -Seconds $DelaySeconds
    }

    if (-not $sawChanged) {
        return $null
    }

    return $latest
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
            } elseif ($status -notin @("completed", "submitted")) {
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

    foreach ($catalogName in @("alejandro")) {
        $providerName = "PDF Alejandro Liz"
        $order = 0
        foreach ($exercise in (Get-StaticCatalog -Root $Root -CatalogName $catalogName)) {
            $order++
            $key = "${catalogName}:$($exercise.slug)"
            $localEntry = $local[$key]
            $testsRoot = if ($localEntry) { Join-Path $localEntry.Folder ".estudio-tests" } else { $null }
            $hasLocalTests = $testsRoot -and (Test-Path -LiteralPath (Join-Path $testsRoot "manifest.json"))
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
                sourceUrl = $exercise.sourceUrl
                driveFileId = $exercise.driveFileId
                order = $order
                unlocked = $true
                supportsTests = [bool]$hasLocalTests
                supportsValidate = [bool]$hasLocalTests
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

    $Markdown = Select-InstructionMarkdown -Markdown $Markdown

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
Traduce al espanol latinoamericano el siguiente enunciado de un ejercicio de programacion.
Devuelve solo el Markdown traducido, sin introducciones ni explicaciones.
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

function Select-InstructionMarkdown {
    param([string]$Markdown)

    if ([string]::IsNullOrWhiteSpace($Markdown)) {
        return $Markdown
    }

    $clean = $Markdown -replace "`r`n", "`n"
    $patterns = @(
        '(?im)^##\s+Fuente\b',
        '(?im)^##\s+Source\b',
        '(?im)^##\s+Credits?\b',
        '(?im)^##\s+External\s+source\b'
    )

    $cutAt = $clean.Length
    foreach ($pattern in $patterns) {
        $match = [regex]::Match($clean, $pattern)
        if ($match.Success -and $match.Index -lt $cutAt) {
            $cutAt = $match.Index
        }
    }

    return $clean.Substring(0, $cutAt).Trim()
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
        [string]$Title,
        [string]$Source
    )

    $safe = $Markdown -replace '\*/', '* /'
    # Strip markdown headings to get clean instruction text
    $lines = @($safe -split "`r?`n")
    $cleanLines = @()
    foreach ($line in $lines) {
        if ($line -match '^\s*#+\s') { continue }
        $cleanLines += $line
    }
    $body = ($cleanLines -join "`n").Trim()

    $sourceText = if (-not [string]::IsNullOrWhiteSpace($Source)) { $Source } else { "Estudio Socratico" }

    return @"
/*
$Title

Instrucciones:
$body

Fuente: $sourceText
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

function Disable-ExercismTestIgnoreLines {
    param([string]$SupportRoot)

    Get-ChildItem -LiteralPath $SupportRoot -Filter "test_*.c" -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        $current = Get-Content -LiteralPath $_.FullName -Raw
        $updated = [regex]::Replace($current, '(?m)^(\s*)TEST_IGNORE\(\);', '$1// TEST_IGNORE();')
        if (-not [string]::Equals($current, $updated, [StringComparison]::Ordinal)) {
            Set-Content -LiteralPath $_.FullName -Value $updated -Encoding utf8
        }
    }
}

function New-ExercismTestWorkspace {
    param(
        [string]$Root,
        [string]$ExerciseRoot,
        [string]$SupportRoot,
        [string[]]$SolutionFiles,
        [string]$Slug
    )

    $runtimeRoot = Join-Path $Root "_estudio\soporte\runtime\exercism-tests"
    if (-not (Test-Path -LiteralPath $runtimeRoot)) {
        New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
    }

    $stamp = Get-Date -Format "yyyyMMdd_HHmmss_fff"
    $folderName = "{0}_{1}" -f (ConvertTo-Slug $Slug), $stamp
    $workspaceRoot = Join-Path $runtimeRoot $folderName
    New-Item -ItemType Directory -Path $workspaceRoot -Force | Out-Null

    Get-ChildItem -LiteralPath $SupportRoot -Force -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $workspaceRoot -Recurse -Force
    }

    Sync-SolutionFilesToSupport -ExerciseRoot $ExerciseRoot -SupportRoot $workspaceRoot -SolutionFiles $SolutionFiles
    Ensure-ExercismMakeShim -SupportRoot $workspaceRoot
    Disable-ExercismTestIgnoreLines -SupportRoot $workspaceRoot

    return $workspaceRoot
}

function Get-ExercismMakeCommand {
    param([string]$SupportRoot)

    $localShim = Join-Path $SupportRoot "make.cmd"
    if (Test-Path -LiteralPath $localShim) {
        return $localShim
    }

    $command = Get-Command make -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) {
        return $command.Source
    }

    foreach ($candidate in @(
        "C:\msys64\usr\bin\make.exe",
        "C:\msys64\mingw64\bin\mingw32-make.exe"
    )) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "No se encontro make para ejecutar los tests oficiales de Exercism."
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
    $userRoot = Resolve-UserDataRoot -Root $Root -Slug $userSlug -Create
    $dir = Join-Path $userRoot "exercism"
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
        throw "No se encontro Exercism CLI. Ejecuta Estudio Socratico Configurador en modo Reparar o instala Exercism.CLI con winget."
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
    $supportRelative = ".estudio-exercism\support"
    $supportRoot = Join-Path $target $supportRelative
    New-Item -ItemType Directory -Path $supportRoot -Force | Out-Null

    $readme = Get-TemplateExerciseMarkdown -Root $Root -Exercise $exercise -ProviderName $ProviderName

    # Alejandro exercises from Gists are already in Spanish; skip Gemini translation
    if ($ProviderName -eq "alejandro" -and -not [string]::IsNullOrWhiteSpace($exercise.gistInstructionsUrl)) {
        $translated = Select-InstructionMarkdown -Markdown $readme
    } else {
        try {
            $translated = Invoke-GeminiTranslation -Markdown $readme -Title $exercise.title
        } catch {
            $translated = Select-InstructionMarkdown -Markdown $readme
        }
    }

    $fileName = Get-TemplateExerciseFileName -Exercise $exercise -ProviderName $ProviderName -ExerciseSlug $ExerciseSlug
    $sourcePath = Join-Path $target $fileName

    # Alejandro exercises: only the comment block, no C skeleton
    $sourceLabel = $exercise.sourceUrl
    if ($ProviderName -eq "alejandro") {
        $sourceLabel = "Problemas de Programacion - Rolando J. Batista & Alejandro J. Liz"
    }
    $commentBlock = ConvertTo-CCommentBlock -Markdown $translated -Title $exercise.title -Source $sourceLabel

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    if ($ProviderName -eq "alejandro") {
        # No starter code; student writes everything from scratch
        [System.IO.File]::WriteAllText($sourcePath, $commentBlock, $utf8NoBom)
    } else {
        $starter = if ($exercise.starterCode) { [string]$exercise.starterCode } else { "#include <stdio.h>`n`nint main(void)`n{`n    return 0;`n}`n" }
        [System.IO.File]::WriteAllText($sourcePath, ($commentBlock + $starter), $utf8NoBom)
    }
    [System.IO.File]::WriteAllText((Join-Path $supportRoot "README.md"), $translated, $utf8NoBom)

    $meta = [pscustomobject]@{
        provider = $ProviderName
        slug = $ExerciseSlug
        title = $exercise.title
        folderName = $folderName
        status = "in_progress"
        difficulty = $exercise.difficulty
        blurb = $exercise.blurb
        topics = @($exercise.topics)
        sourceUrl = $exercise.sourceUrl
        driveFileId = $exercise.driveFileId
        solutionFiles = @($fileName)
        supportRoot = $supportRelative
        importedAt = (Get-Date).ToString("o")
    }
    $meta | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $target ".estudio-exercism.json") -Encoding utf8
    Save-Progress -Root $Root -Provider $ProviderName -Slug $ExerciseSlug -Title $exercise.title -Status "in_progress" -Folder $target

    return [pscustomobject]@{
        ok = $true
        provider = $ProviderName
        slug = $ExerciseSlug
        title = $exercise.title
        folder = $target
        openFile = $sourcePath
    }
}

function Get-TemplateExerciseMarkdown {
    param(
        [string]$Root,
        [object]$Exercise,
        [string]$ProviderName
    )

    # Priority 1: Download from Gist raw URL (Alejandro curated exercises)
    if (-not [string]::IsNullOrWhiteSpace($Exercise.gistInstructionsUrl)) {
        $cacheRoot = Join-Path $Root "_estudio\soporte\runtime\gist-cache"
        if (-not (Test-Path -LiteralPath $cacheRoot)) {
            New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
        }
        $cachePath = Join-Path $cacheRoot ((ConvertTo-Slug $Exercise.title) + ".md")
        if (-not (Test-Path -LiteralPath $cachePath)) {
            try {
                Invoke-WebRequest -Uri $Exercise.gistInstructionsUrl -OutFile $cachePath -TimeoutSec 30 | Out-Null
            } catch {
                # If download fails but we have inline markdown, fall through
            }
        }
        if (Test-Path -LiteralPath $cachePath) {
            $utf8 = New-Object System.Text.UTF8Encoding($false)
            $raw = [System.IO.File]::ReadAllText($cachePath, $utf8)
            return (Select-InstructionMarkdown -Markdown $raw)
        }
    }

    # Priority 2: Google Drive download (legacy Alejandro exercises)
    if ($Exercise.driveFileId) {
        $cacheRoot = Join-Path $Root "_estudio\soporte\runtime\drive-cache"
        if (-not (Test-Path -LiteralPath $cacheRoot)) {
            New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
        }
        $cachePath = Join-Path $cacheRoot ((ConvertTo-Slug $Exercise.title) + ".md")
        if (-not (Test-Path -LiteralPath $cachePath)) {
            $uri = "https://drive.google.com/uc?export=download&id=$($Exercise.driveFileId)"
            Invoke-WebRequest -Uri $uri -OutFile $cachePath -TimeoutSec 60 | Out-Null
        }
        if (Test-Path -LiteralPath $cachePath) {
            return (Select-InstructionMarkdown -Markdown (Get-Content -LiteralPath $cachePath -Raw))
        }
    }

    # Priority 3: Inline instructionMarkdown from catalog JSON
    if ($Exercise.instructionMarkdown) {
        return (Select-InstructionMarkdown -Markdown ([string]$Exercise.instructionMarkdown))
    }

    throw "El ejercicio '$($Exercise.title)' no tiene instrucciones disponibles. Ejecuta npm run alejandro:gists:manifest para regenerar el catalogo."
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

    function Test-ImportedExerciseMarkers {
        param([string]$Dir)

        $markers = @(
            (Join-Path $Dir ".estudio-exercism.json"),
            (Join-Path $Dir ".exercism\metadata.json"),
            (Join-Path $Dir ".estudio-exercism\support"),
            (Join-Path $Dir ".estudio-exercism\support\.exercism\metadata.json"),
            (Join-Path $Dir ".estudio-exercism\support\.exercism\config.json")
        )

        foreach ($marker in $markers) {
            if (Test-Path -LiteralPath $marker) {
                return $true
            }
        }

        return $false
    }

    $dir = if ($item.PSIsContainer) { $item.FullName } else { $item.DirectoryName }
    while ($dir -and $dir.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) {
        if (Test-ImportedExerciseMarkers -Dir $dir) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }

    return $null
}

function Get-ExerciseLogContext {
    param(
        [string]$Root,
        [object]$Meta,
        [string]$ExerciseRoot
    )

    $userSlug = Get-UserSlug -Root $Root
    $userRoot = Resolve-UserDataRoot -Root $Root -Slug $userSlug -Create
    $title = if ($Meta.title) { $Meta.title } else { Split-Path $ExerciseRoot -Leaf }
    $logsDir = Join-Path $userRoot ("logs\" + (ConvertTo-Slug $title))
    $erroresFile = Join-Path $userRoot "errores.md"
    if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir -Force | Out-Null }
    if (-not (Test-Path $erroresFile)) { New-Item -ItemType File -Path $erroresFile -Force | Out-Null }
    $timestamp = Get-Date -Format "yyyy-MM-ddTHH-mm-ss"
    return [pscustomobject]@{
        userSlug = $userSlug
        title = $title
        timestamp = $timestamp
        log = (Join-Path $logsDir ("bloque_" + $timestamp + ".log"))
        erroresFile = $erroresFile
    }
}

function Set-LocalExerciseStatus {
    param(
        [string]$Root,
        [string]$ExerciseRoot,
        [object]$Meta,
        [string]$Status,
        [int]$ExitCode
    )

    $metaPath = Join-Path $ExerciseRoot ".estudio-exercism.json"
    if (Test-Path -LiteralPath $metaPath) {
        Set-ObjectProperty -Target $Meta -Name "status" -Value $Status
        Set-ObjectProperty -Target $Meta -Name "lastValidateAt" -Value (Get-Date).ToString("o")
        Set-ObjectProperty -Target $Meta -Name "lastValidateExitCode" -Value $ExitCode
        if ($Status -eq "completed") {
            Set-ObjectProperty -Target $Meta -Name "completedAt" -Value (Get-Date).ToString("o")
        }
        $Meta | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $metaPath -Encoding utf8
        Save-Progress -Root $Root -Provider $Meta.provider -Slug $Meta.slug -Title $Meta.title -Status $Status -Folder $ExerciseRoot
    }
}

function Invoke-ExerciseValidate {
    param(
        [string]$Root,
        [string]$Path
    )

    $exerciseRoot = Resolve-ExerciseRoot -Root $Root -Path $Path
    if (-not $exerciseRoot) {
        throw "No se pudo detectar un ejercicio importado desde la ruta indicada."
    }

    $metaPath = Join-Path $exerciseRoot ".estudio-exercism.json"
    if (-not (Test-Path -LiteralPath $metaPath)) {
        throw "Falta .estudio-exercism.json."
    }
    $meta = Get-Content -LiteralPath $metaPath -Raw | ConvertFrom-Json
    if ($meta.provider -eq "exercism") {
        return (Invoke-ExercismTest -Root $Root -Path $Path)
    }

    $testsRoot = Join-Path $exerciseRoot ".estudio-tests"
    $manifestPath = Join-Path $testsRoot "manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Este ejercicio aun no tiene tests generados. Usa @test o @validar para crearlos primero."
    }

    $logContext = Get-ExerciseLogContext -Root $Root -Meta $meta -ExerciseRoot $exerciseRoot
    "============================================================" | Add-Content -LiteralPath $logContext.log
    "VALIDACION LOCAL: $($logContext.timestamp)" | Add-Content -LiteralPath $logContext.log
    "EJERCICIO: $($meta.title)" | Add-Content -LiteralPath $logContext.log
    "RUTA: $exerciseRoot" | Add-Content -LiteralPath $logContext.log
    "============================================================" | Add-Content -LiteralPath $logContext.log

    $psRunner = Join-Path $testsRoot "validar.ps1"
    $cmdRunner = Join-Path $testsRoot "validar.cmd"
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        if (Test-Path -LiteralPath $psRunner) {
            $output = powershell -NoProfile -ExecutionPolicy Bypass -File $psRunner -RepoRoot $Root -ExerciseRoot $exerciseRoot 2>&1
            $exitCode = $LASTEXITCODE
        } elseif (Test-Path -LiteralPath $cmdRunner) {
            $output = cmd /c "`"$cmdRunner`" `"$Root`" `"$exerciseRoot`"" 2>&1
            $exitCode = $LASTEXITCODE
        } else {
            throw "No encontre .estudio-tests\validar.ps1 ni .estudio-tests\validar.cmd."
        }
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $output | ForEach-Object {
        Write-Host $_
        $_ | Add-Content -LiteralPath $logContext.log
    }
    "[EXIT CODE: $exitCode]" | Add-Content -LiteralPath $logContext.log

    $newStatus = if ($exitCode -eq 0) { "completed" } else { "tests_failed" }
    Set-LocalExerciseStatus -Root $Root -ExerciseRoot $exerciseRoot -Meta $meta -Status $newStatus -ExitCode $exitCode
    return $exitCode
}

function Invoke-ExerciseWindow {
    param(
        [string]$Root,
        [string]$Path,
        [ValidateSet("test", "validate")]
        [string]$InnerAction
    )

    $runtime = Join-Path $Root "_estudio\soporte\runtime"
    if (-not (Test-Path -LiteralPath $runtime)) {
        New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    }

    $stamp = Get-Date -Format "yyyyMMdd_HHmmss_fff"
    $runner = Join-Path $runtime ("exercise_window_" + $stamp + ".ps1")
    $manager = $PSCommandPath
    $content = @"
`$Host.UI.RawUI.WindowTitle = 'Estudio Socratico - pruebas'
try {
    Set-Location -LiteralPath '$Root'
    powershell -NoProfile -ExecutionPolicy Bypass -File '$manager' -RepoRoot '$Root' -Action $InnerAction -ExercisePath '$Path'
    `$code = `$LASTEXITCODE
    Write-Host ''
    Write-Host ('Process returned {0} (0x{0:X})' -f `$code)
} catch {
    `$code = 1
    Write-Host ('[ERROR] ' + `$_.Exception.Message)
}
Write-Host ''
Read-Host 'Press Enter to continue'
Remove-Item -LiteralPath `$PSCommandPath -Force -ErrorAction SilentlyContinue
exit `$code
"@
    Set-Content -LiteralPath $runner -Value $content -Encoding ascii
    Start-Process -FilePath "powershell.exe" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $runner) -WindowStyle Normal | Out-Null
    return [pscustomobject]@{
        ok = $true
        action = $InnerAction
        runner = $runner
    }
}

function Reveal-ExerciseTests {
    param(
        [string]$Root,
        [string]$Path
    )

    $exerciseRoot = Resolve-ExerciseRoot -Root $Root -Path $Path
    if (-not $exerciseRoot) {
        throw "No se pudo detectar un ejercicio importado desde la ruta indicada."
    }

    $testsRoot = Join-Path $exerciseRoot ".estudio-tests"
    if (-not (Test-Path -LiteralPath $testsRoot)) {
        throw "Este ejercicio aun no tiene tests generados."
    }

    return [pscustomobject]@{
        ok = $true
        folder = $testsRoot
    }
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
    $testWorkspace = New-ExercismTestWorkspace `
        -Root $Root `
        -ExerciseRoot $exerciseRoot `
        -SupportRoot $supportRoot `
        -SolutionFiles $solutionFiles `
        -Slug $meta.slug
    $makeCommand = Get-ExercismMakeCommand -SupportRoot $testWorkspace

    $userRoot = Resolve-UserDataRoot -Root $Root -Slug $userSlug -Create
    $logsDir = Join-Path $userRoot ("logs\" + (ConvertTo-Slug $meta.title))
    $erroresFile = Join-Path $userRoot "errores.md"
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
    "Running tests via make" | Add-Content -LiteralPath $log

    Push-Location $testWorkspace
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $previousPath = $env:PATH
        $ErrorActionPreference = "Continue"
        $msysPath = "C:\msys64\usr\bin"
        $mingwPath = "C:\msys64\mingw64\bin"
        $env:PATH = "$testWorkspace;$msysPath;$mingwPath;$env:PATH"
        try {
            Write-Host "Running tests via make"
            $output = & $makeCommand test 2>&1
            $exitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
            $env:PATH = $previousPath
        }
    } finally {
        Pop-Location
        if ($testWorkspace -and (Test-Path -LiteralPath $testWorkspace)) {
            Remove-Item -LiteralPath $testWorkspace -Recurse -Force -ErrorAction SilentlyContinue
        }
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

    $workspaceRoot = if ($meta.sourceWorkspace) { [string]$meta.sourceWorkspace } else { $null }
    if ([string]::IsNullOrWhiteSpace($workspaceRoot)) {
        $workspace = Get-ExercismWorkspace -Cli $cli
        $workspaceRoot = Join-Path $workspace ("c\" + $meta.slug)
    }
    if (-not (Test-Path -LiteralPath $workspaceRoot)) {
        throw "No encontre el workspace real de Exercism para '$($meta.slug)'. Reimporta el ejercicio o ejecuta exercism download --track c --exercise $($meta.slug)."
    }
    Sync-SolutionFilesToSupport -ExerciseRoot $exerciseRoot -SupportRoot $workspaceRoot -SolutionFiles $solutionFiles

    $output = @()
    Push-Location $workspaceRoot
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $output = & $cli submit 2>&1
            $exitCode = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
    } finally {
        Pop-Location
    }

    $previousRemoteSolution = Get-ExercismSolutionForSlug -Slug $meta.slug
    $remoteSolution = $null
    $remoteTestsStatus = $null
    if ($exitCode -eq 0) {
        $remoteSolution = Wait-ExercismSolutionAfterSubmit -Slug $meta.slug -PreviousSolution $previousRemoteSolution
        $remoteTestsStatus = Get-ExercismSolutionTestsStatus -Solution $remoteSolution
    }

    $newStatus = "submit_failed"
    if ($exitCode -eq 0) {
        if ($remoteTestsStatus -match '(?i)failed|errored|error') {
            $newStatus = "submit_failed"
        } elseif ($remoteSolution -and -not [string]::IsNullOrWhiteSpace($remoteSolution.completed_at)) {
            $newStatus = "completed"
        } else {
            $newStatus = "submitted"
        }
    }
    $viewUrl = Get-ExercismSolutionUrl -Solution $remoteSolution -Slug $meta.slug -Output $output

    Set-ObjectProperty -Target $meta -Name "status" -Value $newStatus
    Set-ObjectProperty -Target $meta -Name "lastSubmitAt" -Value (Get-Date).ToString("o")
    Set-ObjectProperty -Target $meta -Name "lastSubmitExitCode" -Value $exitCode
    Set-ObjectProperty -Target $meta -Name "lastSubmitRemoteTestsStatus" -Value $remoteTestsStatus
    Set-ObjectProperty -Target $meta -Name "lastSubmitUrl" -Value $viewUrl
    if ($newStatus -eq "completed") {
        Set-ObjectProperty -Target $meta -Name "completedAt" -Value (Get-Date).ToString("o")
    }
    $meta | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $metaPath -Encoding utf8
    Save-Progress -Root $Root -Provider "exercism" -Slug $meta.slug -Title $meta.title -Status $newStatus -Folder $exerciseRoot

    return [pscustomobject]@{
        ok = ($exitCode -eq 0)
        completed = ($newStatus -eq "completed")
        provider = "exercism"
        slug = $meta.slug
        title = $meta.title
        status = $newStatus
        exitCode = $exitCode
        remoteTestsStatus = $remoteTestsStatus
        viewUrl = $viewUrl
        folder = $exerciseRoot
        output = @($output | ForEach-Object { "$_" })
    }
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
                catalog = Get-CatalogStatus -Root $RepoRoot
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
            } elseif ($Provider -in @("w3", "w3schools", "w3resource")) {
                throw "El proveedor W3 fue eliminado de Estudio Socrático 1.2. Será reimplementado desde cero en una versión futura."
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
        "validate" {
            $path = if ($ExercisePath) { $ExercisePath } else { $File }
            $exitCode = Invoke-ExerciseValidate -Root $RepoRoot -Path $path
            exit $exitCode
        }
        "test-window" {
            $path = if ($ExercisePath) { $ExercisePath } else { $File }
            Write-Json (Invoke-ExerciseWindow -Root $RepoRoot -Path $path -InnerAction "test")
            exit 0
        }
        "validate-window" {
            $path = if ($ExercisePath) { $ExercisePath } else { $File }
            Write-Json (Invoke-ExerciseWindow -Root $RepoRoot -Path $path -InnerAction "validate")
            exit 0
        }
        "reveal-tests" {
            $path = if ($ExercisePath) { $ExercisePath } else { $File }
            Write-Json (Reveal-ExerciseTests -Root $RepoRoot -Path $path)
            exit 0
        }
        "submit" {
            $path = if ($ExercisePath) { $ExercisePath } else { $File }
            $result = Invoke-ExercismSubmit -Root $RepoRoot -Path $path
            if ($Json -or $OutFile) {
                Write-Json $result
                exit 0
            }
            foreach ($line in @($result.output)) {
                Write-Output $line
            }
            exit $result.exitCode
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
                if (Test-Path -LiteralPath (Join-Path $exerciseRoot ".estudio-tests\manifest.json")) {
                    Write-Output "IS_ESTUDIO_VALIDATE=1"
                    Write-Output ("ESTUDIO_VALIDATE_ROOT=" + $exerciseRoot)
                }
            }
        }
    }
} catch {
    $message = $_.Exception.Message
    if ([string]::IsNullOrWhiteSpace($message)) {
        $message = ($_ | Out-String).Trim()
    }
    if ($Json -or $Action -in @("catalog", "status", "import", "mark", "submit", "test-window", "validate-window", "reveal-tests")) {
        Write-Json ([pscustomobject]@{
            ok = $false
            error = $message
        })
    } else {
        Write-Error $message
    }
    exit 1
}
