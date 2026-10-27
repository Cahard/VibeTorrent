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

    [Fact]
    public void Format_UsesBinaryRussianUnits()
    {
        Assert.Equal("1,0 КБ", DisplayFormat.Bytes(1024));
        Assert.Equal("1,5 МБ", DisplayFormat.Bytes(1024 * 1024 * 3 / 2));
        Assert.Equal("2,00", DisplayFormat.Ratio(2));
        Assert.Equal("—", DisplayFormat.Eta(null));
        Assert.Contains("мин", DisplayFormat.Eta(125));
    }
}
