using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using EstudioSocratico.Configurator.Core;

namespace EstudioSocratico.Configurator.Engine;

public sealed class DownloadManager(AppPaths paths, LogManager logManager)
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(20)
    };

    public async Task<string> DownloadAsync(Uri uri, string fileName, CancellationToken cancellationToken)
    {
        paths.EnsureBaseDirectories();
        var safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        var target = Path.Combine(paths.DownloadCache, safeFileName);
        var temp = target + ".tmp";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("EstudioSocraticoConfigurator/2.0");
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var output = File.Create(temp);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                output.Close();
                File.Move(temp, target, overwrite: true);
                await logManager.WriteAsync("info", "download", $"Descarga completada desde {uri.Host}: {safeFileName}", cancellationToken)
                    .ConfigureAwait(false);
                return target;
            }
            catch (Exception ex) when (attempt < 3)
            {
                await logManager.WriteAsync("warn", "download", $"Reintento {attempt} para {uri.Host}: {ex.Message}", cancellationToken)
                    .ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"No se pudo descargar {uri}.");
    }
}

public sealed class ChecksumVerifier
{
    public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task VerifySha256Async(string path, string? expectedSha256, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            return;
        }

        var actual = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SHA256 invalido para {Path.GetFileName(path)}.");
        }
    }
}

public sealed class OfficialInstallerFallback(LogManager logManager)
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    public async Task<OfficialInstallerSource> ResolveAsync(DependencyId dependency, CancellationToken cancellationToken)
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("EstudioSocraticoConfigurator/2.0");
        return dependency switch
        {
            DependencyId.NodeJs => await ResolveNodeAsync(cancellationToken).ConfigureAwait(false),
            DependencyId.Python => await ResolvePythonAsync(cancellationToken).ConfigureAwait(false),
            DependencyId.Git => await ResolveGitHubReleaseAsync(
                dependency,
                "git-for-windows",
                "git",
                @"Git-.*-64-bit\.exe$",
                ["/VERYSILENT", "/NORESTART", "/NOCANCEL"],
                requiresElevation: true,
                cancellationToken).ConfigureAwait(false),
            DependencyId.GitHubCli => await ResolveGitHubReleaseAsync(
                dependency,
                "cli",
                "cli",
                @"gh_.*_windows_amd64\.msi$",
                ["/qn", "/norestart"],
                requiresElevation: true,
                cancellationToken).ConfigureAwait(false),
            DependencyId.ExercismCli => await ResolveGitHubReleaseAsync(
                dependency,
                "exercism",
                "cli",
                RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? @"exercism-.*-windows-arm64\.zip$"
                    : @"exercism-.*-windows-x86_64\.zip$",
                [],
                requiresElevation: false,
                cancellationToken).ConfigureAwait(false),
            DependencyId.VSCode => new OfficialInstallerSource
            {
                Dependency = dependency,
                Uri = new Uri(RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? "https://update.code.visualstudio.com/latest/win32-arm64-user/stable"
                    : "https://update.code.visualstudio.com/latest/win32-x64-user/stable"),
                SilentArguments = ["/VERYSILENT", "/MERGETASKS=!runcode,addcontextmenufiles,addcontextmenufolders,associatewithfiles,addtopath"],
                RequiresElevation = false,
                SourceName = "visualstudio.com"
            },
            DependencyId.Msys2 => await ResolveGitHubReleaseAsync(
                dependency,
                "msys2",
                "msys2-installer",
                @"msys2-x86_64-.*\.exe$",
                ["in", "--confirm-command", "--accept-messages", "--root", ProductInfo.DefaultMsys2Root],
                requiresElevation: true,
                cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"No hay fallback oficial para {dependency}.")
        };
    }

    private async Task<OfficialInstallerSource> ResolveNodeAsync(CancellationToken cancellationToken)
    {
        var json = await _http.GetStringAsync("https://nodejs.org/dist/index.json", cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var version = doc.RootElement.EnumerateArray()
            .Where(x => x.TryGetProperty("lts", out var lts) && lts.ValueKind != JsonValueKind.False)
            .Select(x => x.GetProperty("version").GetString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (version is null)
        {
            throw new InvalidOperationException("No se pudo resolver la version LTS de Node.js.");
        }

        return new OfficialInstallerSource
        {
            Dependency = DependencyId.NodeJs,
            Version = version,
            Uri = new Uri($"https://nodejs.org/dist/{version}/node-{version}-{(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64")}.msi"),
            SilentArguments = ["/qn", "/norestart"],
            RequiresElevation = true,
            SourceName = "nodejs.org"
        };
    }

    private async Task<OfficialInstallerSource> ResolvePythonAsync(CancellationToken cancellationToken)
    {
        var html = await _http.GetStringAsync("https://www.python.org/downloads/windows/", cancellationToken)
            .ConfigureAwait(false);
        var releaseMatch = Regex.Match(html, "href=\"(?<href>[^\"]+)\"[^>]*>\\s*Latest Python 3 Release - Python (?<version>\\d+\\.\\d+\\.\\d+)", RegexOptions.Singleline);
        if (!releaseMatch.Success)
        {
            releaseMatch = Regex.Match(html, "Latest Python 3 Release - Python (?<version>\\d+\\.\\d+\\.\\d+).*?href=\"(?<href>[^\"]+)\"", RegexOptions.Singleline);
        }
        if (!releaseMatch.Success)
        {
            throw new InvalidOperationException("No se pudo resolver la version estable de Python para Windows.");
        }

        var version = releaseMatch.Groups["version"].Value;
        var href = releaseMatch.Groups["href"].Value;
        var releaseUri = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(href)
            : new Uri(new Uri("https://www.python.org"), href);
        var page = await _http.GetStringAsync(releaseUri, cancellationToken).ConfigureAwait(false);
        var pythonArch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "amd64";
        var label = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "ARM64" : "64-bit";
        var assetMatch = Regex.Match(page, $"href=\"(?<href>[^\"]*python-[^\"]*-{pythonArch}\\.exe)\"[^>]*>Windows installer \\({label}\\)", RegexOptions.IgnoreCase);
        if (!assetMatch.Success)
        {
            throw new InvalidOperationException($"No se encontro el instalador oficial {label} de Python.");
        }

        var assetHref = assetMatch.Groups["href"].Value;
        var assetUri = assetHref.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(assetHref)
            : new Uri(new Uri("https://www.python.org"), assetHref);

        return new OfficialInstallerSource
        {
            Dependency = DependencyId.Python,
            Version = version,
            Uri = assetUri,
            SilentArguments = ["/quiet", "InstallAllUsers=0", "PrependPath=1", "Include_test=0"],
            RequiresElevation = false,
            SourceName = "python.org"
        };
    }

    private async Task<OfficialInstallerSource> ResolveGitHubReleaseAsync(
        DependencyId dependency,
        string owner,
        string repo,
        string assetPattern,
        IReadOnlyList<string> silentArguments,
        bool requiresElevation,
        CancellationToken cancellationToken)
    {
        var api = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var json = await _http.GetStringAsync(api, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var version = doc.RootElement.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
        var regex = new Regex(assetPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (regex.IsMatch(name))
            {
                var url = asset.GetProperty("browser_download_url").GetString();
                if (url is not null)
                {
                    await logManager.WriteAsync("info", "fallback", $"Fallback oficial {dependency}: GitHub release {owner}/{repo} {version}", cancellationToken)
                        .ConfigureAwait(false);
                    return new OfficialInstallerSource
                    {
                        Dependency = dependency,
                        Version = version,
                        Uri = new Uri(url),
                        SilentArguments = silentArguments,
                        RequiresElevation = requiresElevation,
                        SourceName = $"github.com/{owner}/{repo}"
                    };
                }
            }
        }

        throw new InvalidOperationException($"No se encontro asset oficial para {dependency} en {owner}/{repo}.");
    }
}
