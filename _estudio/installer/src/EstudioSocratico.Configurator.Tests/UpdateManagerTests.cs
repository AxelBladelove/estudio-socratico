using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class UpdateManagerTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "estudio-update-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private (UpdateManager updater, AppPaths paths, LogManager log) SetupUpdater(string tempDir, HttpMessageHandler handler)
    {
        var paths = new AppPaths(repoRoot: tempDir, localAppDataRoot: Path.Combine(tempDir, "LocalAppData"));
        paths.EnsureBaseDirectories();
        var log = new LogManager(paths);
        var httpClient = new HttpClient(handler);
        var updater = new UpdateManager(paths, log, httpClient);
        return (updater, paths, log);
    }

    [Fact]
    public async Task Updater_DetectsNewerStableRelease()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var jsonResponse = @"[
                {
                    ""draft"": false,
                    ""prerelease"": false,
                    ""tag_name"": ""v2.0.17"",
                    ""body"": ""Test release notes"",
                    ""assets"": [
                        {
                            ""name"": ""Estudio-Socratico-Setup-v2.0.17-x64.exe"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe""
                        },
                        {
                            ""name"": ""Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256""
                        }
                    ]
                }
            ]";

            var handler = new MockHttpMessageHandler(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                };
            });

            var (updater, _, _) = SetupUpdater(tempDir, handler);
            var result = await updater.CheckForUpdatesAsync();

            Assert.True(result.UpdateAvailable);
            Assert.Equal("2.0.17", result.LatestVersion);
            Assert.Equal("https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe", result.DownloadUrl);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UpdateManager_DetectsNewReleaseFromGitHubActionsRelease()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var jsonResponse = @"[
                {
                    ""draft"": false,
                    ""prerelease"": false,
                    ""tag_name"": ""v2.0.17"",
                    ""body"": ""Patch release from Actions"",
                    ""assets"": [
                        {
                            ""name"": ""Estudio-Socratico-Setup-v2.0.17-x64.exe"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe""
                        },
                        {
                            ""name"": ""Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256""
                        }
                    ]
                }
            ]";
            var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });
            var (updater, _, _) = SetupUpdater(tempDir, handler);

            var result = await updater.CheckForUpdatesAsync();

            Assert.True(result.UpdateAvailable);
            Assert.Equal("2.0.17", result.LatestVersion);
            Assert.Equal(ProductInfo.PublicDisplayVersion, result.LatestDisplayVersion);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Updater_IgnoresPrerelease()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var jsonResponse = @"[
                {
                    ""draft"": false,
                    ""prerelease"": true,
                    ""tag_name"": ""v2.0.11-beta"",
                    ""body"": ""Beta release"",
                    ""assets"": [
                        {
                            ""name"": ""Estudio-Socratico-Setup-v2.0.11-beta-x64.exe"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.11-beta/Estudio-Socratico-Setup-v2.0.11-beta-x64.exe""
                        },
                        {
                            ""name"": ""Estudio-Socratico-Setup-v2.0.11-beta-x64.exe.sha256"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.11-beta/Estudio-Socratico-Setup-v2.0.11-beta-x64.exe.sha256""
                        }
                    ]
                }
            ]";

            var handler = new MockHttpMessageHandler(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                };
            });

            var (updater, _, _) = SetupUpdater(tempDir, handler);
            var result = await updater.CheckForUpdatesAsync();

            Assert.False(result.UpdateAvailable);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Updater_RejectsUnexpectedAssetName()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var jsonResponse = @"[
                {
                    ""draft"": false,
                    ""prerelease"": false,
                    ""tag_name"": ""v2.0.17"",
                    ""body"": ""Test release notes"",
                    ""assets"": [
                        {
                            ""name"": ""MaliciousSetup.exe"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/MaliciousSetup.exe""
                        },
                        {
                            ""name"": ""MaliciousSetup.exe.sha256"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/MaliciousSetup.exe.sha256""
                        }
                    ]
                }
            ]";

            var handler = new MockHttpMessageHandler(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                };
            });

            var (updater, _, _) = SetupUpdater(tempDir, handler);
            
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => updater.CheckForUpdatesAsync());
            Assert.Contains("MaliciousSetup.exe", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Updater_DoesNotUpdateWhenVersionIsSame()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var jsonResponse = @"[
                {
                    ""draft"": false,
                    ""prerelease"": false,
                    ""tag_name"": ""v2.0.16"",
                    ""body"": ""Same version"",
                    ""assets"": [
                        {
                            ""name"": ""Estudio-Socratico-Setup-v2.0.16-x64.exe"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.16/Estudio-Socratico-Setup-v2.0.16-x64.exe""
                        },
                        {
                            ""name"": ""Estudio-Socratico-Setup-v2.0.16-x64.exe.sha256"",
                            ""browser_download_url"": ""https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.16/Estudio-Socratico-Setup-v2.0.16-x64.exe.sha256""
                        }
                    ]
                }
            ]";

            var handler = new MockHttpMessageHandler(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                };
            });

            var (updater, _, _) = SetupUpdater(tempDir, handler);
            var result = await updater.CheckForUpdatesAsync();

            Assert.False(result.UpdateAvailable);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Updater_DownloadsOnlyFromOfficialRepo()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK));
            var (updater, _, _) = SetupUpdater(tempDir, handler);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await updater.TriggerUpdateAsync(
                    "https://otherdomain.com/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe",
                    "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256",
                    "2.0.17",
                    NullProgressSink.Instance);
            });

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await updater.TriggerUpdateAsync(
                    "https://github.com/OtherUser/other-repo/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe",
                    "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256",
                    "2.0.17",
                    NullProgressSink.Instance);
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Updater_VerifiesSha256BeforeLaunch()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exePayload = Encoding.UTF8.GetBytes("fake setup payload");
            var shaString = Convert.ToHexString(SHA256.HashData(exePayload)).ToLowerInvariant();

            var handler = new MockHttpMessageHandler(req =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                if (uri.EndsWith(".sha256"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(shaString)
                    };
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(exePayload)
                    };
                }
            });

            var (updater, _, _) = SetupUpdater(tempDir, handler);

            string? launchedPath = null;
            updater.InstallerLauncher = (path) =>
            {
                launchedPath = path;
            };

            await updater.TriggerUpdateAsync(
                "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe",
                "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256",
                "2.0.17",
                NullProgressSink.Instance);

            Assert.NotNull(launchedPath);
            Assert.True(File.Exists(launchedPath));
            Assert.Equal("Estudio-Socratico-Setup-v2.0.17-x64.exe", Path.GetFileName(launchedPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UpdateManager_DownloadsAndVerifiesSha256()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exePayload = Encoding.UTF8.GetBytes("fake setup payload");
            var shaString = Convert.ToHexString(SHA256.HashData(exePayload)).ToLowerInvariant();
            var handler = new MockHttpMessageHandler(req =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = uri.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
                        ? new StringContent(shaString)
                        : new ByteArrayContent(exePayload)
                };
            });
            var (updater, paths, _) = SetupUpdater(tempDir, handler);
            string? launchedPath = null;
            updater.InstallerLauncher = path => launchedPath = path;

            await updater.TriggerUpdateAsync(
                "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe",
                "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256",
                "2.0.17",
                NullProgressSink.Instance);

            Assert.Equal(Path.Combine(paths.UpdatesRoot, "Estudio-Socratico-Setup-v2.0.17-x64.exe"), launchedPath);
            Assert.True(File.Exists(Path.Combine(paths.UpdatesRoot, "Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UpdaterButton_TriggersDownloadVerifyAndInstallerLaunch()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exePayload = Encoding.UTF8.GetBytes("fake setup payload from visible updater button");
            var shaString = Convert.ToHexString(SHA256.HashData(exePayload)).ToLowerInvariant();
            var handler = new MockHttpMessageHandler(req =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = uri.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
                        ? new StringContent(shaString)
                        : new ByteArrayContent(exePayload)
                };
            });
            var (updater, paths, _) = SetupUpdater(tempDir, handler);
            string? launchedPath = null;
            updater.InstallerLauncher = path => launchedPath = path;

            await updater.TriggerUpdateAsync(
                "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe",
                "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256",
                "2.0.17",
                NullProgressSink.Instance);

            Assert.Equal(Path.Combine(paths.UpdatesRoot, "Estudio-Socratico-Setup-v2.0.17-x64.exe"), launchedPath);
            Assert.True(File.Exists(Path.Combine(paths.UpdatesRoot, "Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Updater_RefusesLaunchWhenShaFails()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exePayload = Encoding.UTF8.GetBytes("fake setup payload");
            var wrongShaString = new string('a', 64); // mismatching hash

            var handler = new MockHttpMessageHandler(req =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                if (uri.EndsWith(".sha256"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(wrongShaString)
                    };
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(exePayload)
                    };
                }
            });

            var (updater, _, _) = SetupUpdater(tempDir, handler);

            bool launcherInvoked = false;
            updater.InstallerLauncher = (path) =>
            {
                launcherInvoked = true;
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await updater.TriggerUpdateAsync(
                    "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe",
                    "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256",
                    "2.0.17",
                    NullProgressSink.Instance);
            });

            Assert.False(launcherInvoked);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Updater_KeepsStudentDataOnUpgrade()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            // Set up a mock student data directory
            var studentDir = Path.Combine(tempDir, "usuario");
            Directory.CreateDirectory(studentDir);
            var errorLog = Path.Combine(studentDir, "errores.md");
            await File.WriteAllTextAsync(errorLog, "Some user errors recorded.");

            var exePayload = Encoding.UTF8.GetBytes("fake setup payload");
            var shaString = Convert.ToHexString(SHA256.HashData(exePayload)).ToLowerInvariant();

            var handler = new MockHttpMessageHandler(req =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                if (uri.EndsWith(".sha256"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(shaString)
                    };
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(exePayload)
                    };
                }
            });

            var (updater, _, _) = SetupUpdater(tempDir, handler);
            updater.InstallerLauncher = (path) => { };

            await updater.TriggerUpdateAsync(
                "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe",
                "https://github.com/AxelBladelove/estudio-socratico/releases/download/v2.0.17/Estudio-Socratico-Setup-v2.0.17-x64.exe.sha256",
                "2.0.17",
                NullProgressSink.Instance);

            // Assert that the student data is intact
            Assert.True(Directory.Exists(studentDir));
            Assert.True(File.Exists(errorLog));
            Assert.Equal("Some user errors recorded.", await File.ReadAllTextAsync(errorLog));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
