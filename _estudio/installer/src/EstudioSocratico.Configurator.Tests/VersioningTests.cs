using System;
using System.IO;
using System.Xml;
using EstudioSocratico.Configurator.Core;
using EstudioSocratico.Configurator.Engine;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class VersioningTests
{
    [Fact]
    public void Versioning_PublicDisplayVersionIsStable20()
    {
        Assert.Equal("2.0", ProductInfo.PublicDisplayVersion);
    }

    [Fact]
    public void Versioning_ProductInfoMatchesDirectoryBuildProps()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var propsPath = Path.Combine(repoRoot, "_estudio", "installer", "Directory.Build.props");
        Assert.True(File.Exists(propsPath), $"Directory.Build.props not found at: {propsPath}");

        var doc = new XmlDocument();
        doc.Load(propsPath);
        var productVersionNode = doc.SelectSingleNode("//ProductVersion");
        Assert.NotNull(productVersionNode);

        var xmlVersion = productVersionNode.InnerText?.Trim();
        Assert.Equal(ProductInfo.Version, xmlVersion);
    }

    [Fact]
    public void Versioning_CheckExpectedFilesExist()
    {
        var repoRoot = AppPaths.TryResolveRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        // Verify key files that set-version.ps1 relies on exist
        var paths = new[]
        {
            Path.Combine(repoRoot, "_estudio", "installer", "Directory.Build.props"),
            Path.Combine(repoRoot, "_estudio", "installer", "src", "EstudioSocratico.Configurator.Core", "ProductInfo.cs"),
            Path.Combine(repoRoot, "_estudio", "installer", "packaging", "wix-burn", "EstudioSocratico.Configurator.Package.wixproj"),
            Path.Combine(repoRoot, "_estudio", "installer", "packaging", "wix-burn", "EstudioSocratico.Setup.Bundle.wixproj"),
            Path.Combine(repoRoot, "package.json"),
            Path.Combine(repoRoot, "README.md"),
            Path.Combine(repoRoot, "_estudio", "installer", "README.md"),
            Path.Combine(repoRoot, "_estudio", "installer", "docs", "release-v2.md")
        };

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"Expected file for version management does not exist: {path}");
        }
    }
}
