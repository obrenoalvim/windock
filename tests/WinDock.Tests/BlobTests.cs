using WinDock.Services;

namespace WinDock.Tests;

public class BlobTests
{
    static (byte[] Fav, byte[] Res) Chunk(string name, string appId) =>
        TaskbarBlob.BuildClassic(name, @"C:\Users\x\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\" + name,
            2440, appId, new HashSet<string>(), new Random(1));

    [Fact]
    public void Favorite_entry_for_a_short_name_matches_the_size_windows_writes()
    {
        var (fav, _) = Chunk("Brave.lnk", "Brave");
        Assert.Equal(5 + 314, fav.Length);
    }

    [Theory]
    [InlineData("Brave.lnk", "Brave")]
    [InlineData("Visual Studio Code.lnk", "Microsoft.VisualStudioCode")]
    [InlineData("qBittorrent.lnk", @"{6D809377-6AF0-444B-8957-A3773F02200E}\qBittorrent\qbittorrent.exe")]
    public void Built_pin_parses_back_to_the_same_name_and_app_id(string name, string appId)
    {
        var (fav, res) = Chunk(name, appId);
        var pin = Assert.Single(TaskbarBlob.Parse(TaskbarBlob.JoinFavorites([fav]), TaskbarBlob.JoinResolve([res])));
        Assert.Equal(name, pin.LinkName);
        Assert.Equal(appId, pin.AppId);
    }

    [Fact]
    public void Order_is_kept_when_joining_several_pins()
    {
        var a = Chunk("Alpha.lnk", "Alpha");
        var b = Chunk("Beta.lnk", "Beta");
        var pins = TaskbarBlob.Parse(TaskbarBlob.JoinFavorites([a.Fav, b.Fav]), TaskbarBlob.JoinResolve([a.Res, b.Res]));
        Assert.Equal(["Alpha.lnk", "Beta.lnk"], pins.Select(p => p.LinkName));
    }

    [Fact]
    public void Favorites_blob_ends_with_the_terminator()
    {
        var (fav, _) = Chunk("Brave.lnk", "Brave");
        Assert.Equal(0xFF, TaskbarBlob.JoinFavorites([fav])[^1]);
    }
}
