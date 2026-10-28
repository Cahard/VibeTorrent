using Microsoft.Extensions.Logging.Abstractions;
using ZTorrent.Core.Models;
using ZTorrent.Core.Services;

namespace ZTorrent.Tests;

public sealed class TorrentPreviewTests
{
    [Fact]
    public async Task InspectTorrent_ReadsFilesAndTieredTrackers()
    {
        using var fixture = await SyntheticTorrentFactory.CreateAsync(
            announce: "udp://tracker.example.test:1337/announce");
        var previewService = new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance);

        var preview = await previewService.InspectTorrentFileAsync(fixture.TorrentPath);

        Assert.True(preview.HasMetadata);
        Assert.Equal(fixture.TorrentName, preview.Name);
        Assert.Equal(2, preview.Files.Count);
        Assert.Equal(98_304, preview.TotalSize);
        var tracker = Assert.Single(preview.Trackers);
        Assert.Equal("udp://tracker.example.test:1337/announce", tracker);
        Assert.DoesNotContain("System.Collections", tracker);
    }

    [Fact]
    public void InspectMagnet_ValidatesAndReadsHash()
    {
        var service = new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance);
        var preview = service.InspectMagnet(
            "magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567&dn=Test&xl=1024");

        Assert.False(preview.HasMetadata);
        Assert.Equal("0123456789ABCDEF0123456789ABCDEF01234567", preview.InfoHash);
        Assert.Equal(1024, preview.TotalSize);
        Assert.Equal("Test", preview.Name);
    }

    [Fact]
    public void InspectMagnet_RejectsGarbage()
    {
        var service = new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance);
        Assert.Throws<TorrentValidationException>(() => service.InspectMagnet("https://example.test"));
        Assert.Throws<TorrentValidationException>(() => service.InspectMagnet("magnet:?dn=missing"));
    }
}
