using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class UpdateManager
{
    private readonly AppPaths _paths;
    private readonly LogManager _log;
    private readonly ChecksumVerifier _checksum = new();
    private readonly HttpClient _http;

    public Action<string> InstallerLauncher { get; set; } = exePath =>
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        };
        Process.Start(psi);
        Environment.Exit(0);
    };

    public UpdateManager(AppPaths paths, LogManager log, HttpClient? httpClient = null)
    {
        _paths = paths;
        _log = log;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("EstudioSocraticoConfigurator/2.0");
        }
    }

    /// <summary>
    /// Checks for a newer stable release on GitHub.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{ProductInfo.BaseRepositoryOwner}/{ProductInfo.RepositoryName}/releases";
            var json = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            foreach (var releaseElement in doc.RootElement.EnumerateArray())
            {
                var draft = releaseElement.TryGetProperty("draft", out var dProp) && dProp.GetBoolean();
                var prerelease = releaseElement.TryGetProperty("prerelease", out var pProp) && pProp.GetBoolean();

                // Ignore drafts and prereleases
                if (draft || prerelease)
                {
                    continue;
                }

                var tagName = releaseElement.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    continue;
                }

                // Strip 'v' prefix if present to compare
                var cleanTag = tagName.TrimStart('v');
                if (VersionParsing.CompareLoose(cleanTag, ProductInfo.Version) <= 0)
                {
                    // Releases are sorted latest first. If this one is not newer, none will be.
                    break;
                }

                // Find the assets
                string? exeUrl = null;
                string? shaUrl = null;
                string? exeName = null;
                string? shaName = null;

                if (releaseElement.TryGetProperty("assets", out var assetsProp))
                {
                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                        var downloadUrl = asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() : null;

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
                        {
                            continue;
                        }

                        if (name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase))
                        {
                            shaUrl = downloadUrl;
                            shaName = name;
                        }
                        else if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            exeUrl = downloadUrl;
                            exeName = name;
                        }
                    }
                }

                if (exeUrl != null && shaUrl != null && exeName != null && shaName != null)
                {
                    // Found a valid newer release with required assets
                    var body = releaseElement.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";

                    // Validate URLs and names right away to keep logic secure
                    ValidateUrl(exeUrl);
                    ValidateUrl(shaUrl);
                    ValidateAssetNames(exeName, shaName, cleanTag);

                    return new UpdateCheckResult
                    {
                        UpdateAvailable = true,
                        LocalVersion = ProductInfo.Version,
                        LatestVersion = cleanTag,
                        LatestDisplayVersion = ProductInfo.PublicDisplayVersion, // brand version stays stable
                        DownloadUrl = exeUrl,
                        Sha256Url = shaUrl,
                        ReleaseNotes = body
                    };
                }
            }

            return new UpdateCheckResult
            {
                UpdateAvailable = false,
                LocalVersion = ProductInfo.Version,
                LatestVersion = ProductInfo.Version,
                LatestDisplayVersion = ProductInfo.PublicDisplayVersion
            };
        }
        catch (Exception ex)
        {
            await _log.WriteAsync("error", "updater", $"Error al buscar actualizaciones: {ex.Message}", cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Downloads, verifies, and launches the update installer.
    /// </summary>
    public async Task TriggerUpdateAsync(
        string downloadUrl,
        string sha256Url,
        string expectedVersion,
        IProgressSink progressSink,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureBaseDirectories();

        // 1. Strict Security Checks
        ValidateUrl(downloadUrl);
        ValidateUrl(sha256Url);

        var cleanVersion = expectedVersion.TrimStart('v');
        if (VersionParsing.CompareLoose(cleanVersion, ProductInfo.Version) <= 0)
        {
            throw new InvalidOperationException("La version propuesta es menor o igual a la version actual.");
        }

        var exeName = $"Estudio-Socratico-Setup-v{cleanVersion}-x64.exe";
        var shaName = $"{exeName}.sha256";
        ValidateAssetNames(exeName, shaName, cleanVersion);

        var tempDir = _paths.UpdatesRoot;
        Directory.CreateDirectory(tempDir);

        var exePath = Path.Combine(tempDir, exeName);
        var shaPath = Path.Combine(tempDir, shaName);

        // 2. Download Checksum file (instant download)
        await progressSink.ReportAsync(new ProgressEvent
        {
            StepId = "update-download",
            Title = "Descargando firma",
            Message = "Obteniendo archivo checksum SHA256.",
            Percent = 5,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);

        using (var shaResponse = await _http.GetAsync(sha256Url, cancellationToken).ConfigureAwait(false))
        {
            shaResponse.EnsureSuccessStatusCode();
            var shaText = await shaResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(shaPath, shaText, cancellationToken).ConfigureAwait(false);
        }

        // Parse expected hash
        var shaMatch = Regex.Match(File.ReadAllText(shaPath), @"^[a-fA-F0-9]{64}");
        if (!shaMatch.Success)
        {
            throw new InvalidOperationException("El archivo de firma .sha256 no contiene un hash valido.");
        }
        var expectedSha = shaMatch.Value;

        // 3. Download Installer with progress
        await progressSink.ReportAsync(new ProgressEvent
        {
            StepId = "update-download",
            Title = "Descargando instalador",
            Message = "Descargando la nueva version de Estudio Socratico.",
            Percent = 10,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);

        using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength;

            using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            using (var fileStream = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
            {
                var buffer = new byte[81920];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    totalBytesRead += bytesRead;

                    if (contentLength.HasValue && contentLength.Value > 0)
                    {
                        var progressPercent = 10 + (int)((totalBytesRead * 80) / contentLength.Value);
                        await progressSink.ReportAsync(new ProgressEvent
                        {
                            StepId = "update-download",
                            Title = "Descargando instalador",
                            Message = $"Descargando ({totalBytesRead / (1024 * 1024)} MB / {contentLength.Value / (1024 * 1024)} MB).",
                            Percent = progressPercent,
                            Status = DependencyStatus.Installing
                        }, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        // 4. Verify Checksum
        await progressSink.ReportAsync(new ProgressEvent
        {
            StepId = "update-verify",
            Title = "Verificando integridad",
            Message = "Comprobando hash SHA256 de seguridad.",
            Percent = 95,
            Status = DependencyStatus.Installing
        }, cancellationToken).ConfigureAwait(false);

        var actualSha = await _checksum.ComputeSha256Async(exePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(exePath); } catch { /* ignore */ }
            try { File.Delete(shaPath); } catch { /* ignore */ }
            throw new InvalidOperationException("El hash SHA256 del instalador descargado no coincide con la firma oficial. Abortando actualizacion.");
        }

        await progressSink.ReportAsync(new ProgressEvent
        {
            StepId = "update-launch",
            Title = "Ejecutando instalador",
            Message = "Iniciando instalacion. El configurador se cerrara.",
            Percent = 100,
            Status = DependencyStatus.Ready
        }, cancellationToken).ConfigureAwait(false);

        await _log.WriteAsync("info", "updater", $"Ejecutando actualizador: {exePath}", cancellationToken).ConfigureAwait(false);

        // 5. Run the installer and Exit App
        InstallerLauncher(exePath);
    }

    private static void ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("La URL de descarga no puede estar vacia.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("URL de descarga invalida.");
        }

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se permiten descargas desde github.com.");
        }

        // Must belong to AxelBladelove/estudio-socratico releases
        if (!uri.AbsolutePath.StartsWith("/AxelBladelove/estudio-socratico/releases/download/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La ruta de descarga debe pertenecer al repositorio oficial AxelBladelove/estudio-socratico.");
        }
    }

    private static void ValidateAssetNames(string exeName, string sha256Name, string expectedVersion)
    {
        var escapedVersion = Regex.Escape(expectedVersion);
        var expectedExePattern = $"^Estudio-Socratico-Setup-v{escapedVersion}-x64\\.exe$";
        var expectedShaPattern = $"^Estudio-Socratico-Setup-v{escapedVersion}-x64\\.exe\\.sha256$";

        if (!Regex.IsMatch(exeName, expectedExePattern, RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException($"El nombre del instalador '{exeName}' no es valido o no coincide con la version esperada.");
        }

        if (!Regex.IsMatch(sha256Name, expectedShaPattern, RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException($"El nombre de la firma '{sha256Name}' no es valido o no coincide con la version esperada.");
        }
    }
}
