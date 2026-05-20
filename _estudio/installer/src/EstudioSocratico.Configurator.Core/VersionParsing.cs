using System.Text.RegularExpressions;

namespace EstudioSocratico.Configurator.Core;

public static partial class VersionParsing
{
    public static string? FirstVersionLikeValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = VersionRegex().Match(text);
        return match.Success ? match.Value : null;
    }

    public static int CompareLoose(string? left, string? right)
    {
        var leftVersion = ParseLoose(left);
        var rightVersion = ParseLoose(right);

        for (var i = 0; i < Math.Max(leftVersion.Length, rightVersion.Length); i++)
        {
            var l = i < leftVersion.Length ? leftVersion[i] : 0;
            var r = i < rightVersion.Length ? rightVersion[i] : 0;
            var cmp = l.CompareTo(r);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private static int[] ParseLoose(string? value)
    {
        var version = FirstVersionLikeValue(value);
        if (version is null)
        {
            return [0];
        }

        return version.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var number) ? number : 0)
            .ToArray();
    }

    [GeneratedRegex(@"\d+(?:\.\d+){1,3}", RegexOptions.Compiled)]
    private static partial Regex VersionRegex();
}
