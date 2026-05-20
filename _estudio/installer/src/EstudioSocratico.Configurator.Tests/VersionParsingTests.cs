using EstudioSocratico.Configurator.Core;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public sealed class VersionParsingTests
{
    [Theory]
    [InlineData("gcc.exe (Rev2, Built by MSYS2 project) 14.2.0", "14.2.0")]
    [InlineData("gh version 2.72.0 (2026-04-01)", "2.72.0")]
    [InlineData("Python 3.13.5", "3.13.5")]
    public void Extracts_First_Version(string input, string expected)
    {
        Assert.Equal(expected, VersionParsing.FirstVersionLikeValue(input));
    }

    [Fact]
    public void Compares_Loose_Versions()
    {
        Assert.True(VersionParsing.CompareLoose("14.2.0", "13.0") > 0);
        Assert.True(VersionParsing.CompareLoose("2.1", "2.1.0") == 0);
        Assert.True(VersionParsing.CompareLoose("1.9", "2.0") < 0);
    }
}
