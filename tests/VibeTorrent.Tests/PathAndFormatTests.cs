using VibeTorrent.Core.Utilities;

namespace VibeTorrent.Tests;

public sealed class PathAndFormatTests
{
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("folder/../../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/Windows/file.txt")]
    [InlineData("folder/segment.")]
    [InlineData("folder/CON.txt")]
    [InlineData("folder/file.txt:secret")]
    [InlineData(@"\\server\share\file.txt")]
    [InlineData("folder/\u0001bad.txt")]
    public void ValidateRelativePath_RejectsUnsafePaths(string path)
    {
        Assert.ThrowsAny<ArgumentException>(() => PathSafety.ValidateRelativePath(path));
    }

    [Theory]
    [InlineData("movie.mkv", "movie.mkv")]
    [InlineData("folder/season 1/episode 01.mkv", "folder/season 1/episode 01.mkv")]
    [InlineData("./folder/file.txt", "folder/file.txt")]
    public void ValidateRelativePath_AcceptsSafePaths(string input, string expected)
    {
        Assert.Equal(expected, PathSafety.ValidateRelativePath(input));
    }

    [Fact]
    public void IsUnderRoot_HandlesSiblingPrefixes()
    {
        var root = Path.Combine(Path.GetTempPath(), "vibetorrent-root");
        Assert.True(PathSafety.IsUnderRoot(root, Path.Combine(root, "inside.txt")));
        Assert.False(PathSafety.IsUnderRoot(root, root + "-evil/outside.txt"));
        Assert.False(PathSafety.IsUnderRoot(root, Path.Combine(root, "..", "outside.txt")));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12.5, 0.125)]
    [InlineData(100, 1)]
    [InlineData(150, 1)]
    [InlineData(-4, 0)]
    public void ProgressMath_ConvertsMonoTorrentPercentToFraction(double percent, double expected)
    {
        Assert.Equal(expected, ProgressMath.FromPercent(percent), 6);
    }

    [Fact]
    public void Format_UsesBinaryEnglishUnitsByDefault()
    {
        Assert.Equal("1.0 KB", DisplayFormat.Bytes(1024));
        Assert.Equal("1.5 MB", DisplayFormat.Bytes(1024 * 1024 * 3 / 2));
        Assert.Equal("2.00", DisplayFormat.Ratio(2));
        Assert.Equal("—", DisplayFormat.Eta(null));
        Assert.Contains("min", DisplayFormat.Eta(125));
    }
}
