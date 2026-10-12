using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace WinDock.Services;

public sealed record ParsedPin(string Name, string? LinkName, string? AppId, byte[] Favorites, byte[] Resolve);

public static partial class TaskbarBlob
{
    static readonly byte[] Root = Convert.FromHexString(
        "3a001f80c827341f105c1042aa032ee45287d668260001002600efbe1200000062ba7ce306edda01c0a5860e07edda01e1a7581309edda011400");

    static readonly byte[] Folder = Convert.FromHexString(
        "56003100000000000c5934b511005461736b42617200400009000400efbe0c59a7b30c5935b52e00000055d00600000005000000000000000000000000000000b25e78005400610073006b0042006100720000001600");

    static readonly byte[] LinkHeader = Convert.FromHexString(
        "4c0000000114020000000000c00000000000004683008000200000003a27581309edda0105f6581309edda01c8af64d608edda01880900000000000001000000000000000000000000000000");

    static readonly byte[] Tracker = Convert.FromHexString(
        "60000000030000a058000000000000006272656e6f00000000000000000000008c81301a822a5f4cada317e2cac823a6ed99749dfa58ef11ad5f8c882b25db3d8c81301a822a5f4cada317e2cac823a6ed99749dfa58ef11ad5f8c882b25db3d");

    static readonly byte[] Props = Convert.FromHexString(
        "45000000090000a03900000031535053b1166d44ad8d7048a748402ea43d788c1d0000006800000000480000005da8b58bff331e4ea2136b1b0afc141a0000000000000000");

    static readonly (Guid Id, string Alias)[] KnownFolders =
    [
        (new("6D809377-6AF0-444B-8957-A3773F02200E"), "ProgramFilesX64"),
        (new("7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E"), "ProgramFilesX86"),
        (new("F38BF404-1D43-42F2-9305-67DE0B28FC23"), "Windows"),
        (new("1AC14E77-02E7-4E5D-B744-2EB1AE5198B7"), "System"),
        (new("D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27"), "SystemX86")
    ];

    [DllImport("shell32.dll", PreserveSig = false)]
    static extern nint SHGetKnownFolderPath(ref Guid id, uint flags, nint token);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetVolumeInformationW(string root, StringBuilder label, int labelSize, out uint serial,
        out uint maxComponent, out uint flags, StringBuilder fsName, int fsSize);

    [GeneratedRegex(@"^[\w.\-]+_[a-z0-9]{13}![\w.]+$")]
    private static partial Regex AumidPattern();

    static TaskbarBlob() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static string AppIdForTarget(string exePath)
    {
        foreach (var (id, _) in KnownFolders.OrderByDescending(k => KnownFolderPath(k.Id).Length))
        {
            var root = KnownFolderPath(id);
            if (root.Length > 0 && exePath.StartsWith(root + @"\", StringComparison.OrdinalIgnoreCase))
                return "{" + id.ToString().ToUpperInvariant() + "}" + exePath[root.Length..];
        }
        return exePath;
    }

    public static string ExpandAppId(string appId)
    {
        foreach (var (id, _) in KnownFolders)
        {
            var prefix = "{" + id.ToString() + "}";
            if (appId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && KnownFolderPath(id) is { Length: > 0 } root)
                return root + appId[prefix.Length..];
        }
        return appId;
    }

    static string KnownFolderPath(Guid id)
    {
        try
        {
            var g = id;
            var ptr = SHGetKnownFolderPath(ref g, 0, 0);
            var path = Marshal.PtrToStringUni(ptr) ?? "";
            Marshal.FreeCoTaskMem(ptr);
            return path;
        }
        catch (COMException) { return ""; }
    }

    public static (byte[] Favorites, byte[] Resolve) BuildClassic(string linkName, string linkPath, int linkSize, string appId, HashSet<string> usedShort, Random rng)
    {
        var shortName = ShortName(linkName, usedShort);
        var fav = BuildIdList(linkName, linkSize, appId, shortName, rng, favoriteOrder: true);
        var res = BuildIdList(linkName, linkSize, appId, shortName, rng: new Random(0), favoriteOrder: false, reuse: fav);
        return (WrapFavorite(fav), WrapResolve(res, linkPath, linkSize));
    }

    static byte[] WrapFavorite(IdList list)
    {
        var ms = new MemoryStream();
        ms.WriteByte(0);
        ms.Write(BitConverter.GetBytes(list.Bytes.Length));
        ms.Write(list.Bytes);
        return ms.ToArray();
    }

    static byte[] WrapResolve(IdList list, string linkPath, int linkSize)
    {
        var header = (byte[])LinkHeader.Clone();
        var now = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
        for (var i = 0; i < 3; i++) now.CopyTo(header, 28 + i * 8);
        BitConverter.GetBytes(linkSize).CopyTo(header, 52);

        var body = new MemoryStream();
        body.Write(header);
        body.Write(BitConverter.GetBytes((ushort)list.Bytes.Length));
        body.Write(list.Bytes);
        body.Write(BuildLinkInfo(linkPath));
        body.Write(MachineTracker());
        body.Write(Props);
        body.Write(new byte[4]);

        var ms = new MemoryStream();
        ms.Write(BitConverter.GetBytes((int)body.Length));
        ms.Write(body.ToArray());
        return ms.ToArray();
    }

    static byte[] MachineTracker()
    {
        var tracker = (byte[])Tracker.Clone();
        Array.Clear(tracker, 16, 16);
        Encoding.ASCII.GetBytes(Environment.MachineName.ToLowerInvariant()[..Math.Min(15, Environment.MachineName.Length)]).CopyTo(tracker, 16);
        return tracker;
    }

    static byte[] BuildLinkInfo(string linkPath)
    {
        var label = new StringBuilder(261);
        uint serial = 0;
        var root = Path.GetPathRoot(linkPath) ?? @"C:\";
        if (!GetVolumeInformationW(root, label, label.Capacity, out serial, out _, out _, new StringBuilder(32), 32)) label.Clear();
        var ansi = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        var labelBytes = ansi.GetBytes(label.ToString() + "\0");
        var pathBytes = ansi.GetBytes(linkPath + "\0");

        var volume = new MemoryStream();
        volume.Write(BitConverter.GetBytes(16 + labelBytes.Length));
        volume.Write(BitConverter.GetBytes(3));
        volume.Write(BitConverter.GetBytes(serial));
        volume.Write(BitConverter.GetBytes(16));
        volume.Write(labelBytes);

        const int headerSize = 28;
        var pathOffset = headerSize + (int)volume.Length;
        var suffixOffset = pathOffset + pathBytes.Length;

        var ms = new MemoryStream();
        ms.Write(BitConverter.GetBytes(suffixOffset + 1));
        ms.Write(BitConverter.GetBytes(headerSize));
        ms.Write(BitConverter.GetBytes(1));
        ms.Write(BitConverter.GetBytes(headerSize));
        ms.Write(BitConverter.GetBytes(pathOffset));
        ms.Write(BitConverter.GetBytes(0));
        ms.Write(BitConverter.GetBytes(suffixOffset));
        ms.Write(volume.ToArray());
        ms.Write(pathBytes);
        ms.WriteByte(0);
        return ms.ToArray();
    }

    sealed record IdList(byte[] Bytes, byte[] FileItem);

    static IdList BuildIdList(string linkName, int linkSize, string appId, byte[] shortName, Random rng, bool favoriteOrder, IdList? reuse = null)
    {
        var fileItem = reuse is null
            ? BuildFileItem(linkName, linkSize, appId, shortName, rng, favoriteOrder)
            : ReorderTrail(reuse.FileItem, linkName, appId, shortName);

        var ms = new MemoryStream();
        ms.Write(Root);
        ms.Write(Folder);
        ms.Write(fileItem);
        ms.Write(new byte[2]);
        return new IdList(ms.ToArray(), fileItem);
    }

    static byte[] ReorderTrail(byte[] favoriteItem, string linkName, string appId, byte[] shortName)
    {
        var extStart = 14 + shortName.Length;
        var extSize = BitConverter.ToUInt16(favoriteItem, extStart);
        var headEnd = extStart + extSize;
        var head = favoriteItem[..headEnd];
        var rest = favoriteItem[headEnd..];

        var blocks = new List<byte[]>();
        for (var i = 0; i < rest.Length;)
        {
            var size = BitConverter.ToUInt16(rest, i);
            blocks.Add(rest[i..(i + size)]);
            i += size;
        }

        var ordered = new[] { blocks[2], blocks[0], blocks[1] };
        var ms = new MemoryStream();
        ms.Write(head);
        foreach (var b in ordered) ms.Write(b);
        var bytes = ms.ToArray();
        BitConverter.GetBytes((ushort)bytes.Length).CopyTo(bytes, 0);
        return bytes;
    }

    static byte[] BuildFileItem(string linkName, int linkSize, string appId, byte[] shortName, Random rng, bool favoriteOrder)
    {
        var (dosDate, dosTime) = DosNow();
        var name = Encoding.Unicode.GetBytes(linkName + "\0");
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);

        w.Write((ushort)0);
        w.Write((ushort)0x32);
        w.Write(linkSize);
        w.Write(dosDate);
        w.Write(dosTime);
        w.Write((ushort)0x20);
        w.Write(shortName);

        var extStart = (ushort)ms.Length;
        w.Write((ushort)(48 + name.Length));
        w.Write((ushort)9);
        w.Write(0xBEEF0004u);
        w.Write(dosDate);
        w.Write(dosTime);
        w.Write(dosDate);
        w.Write(dosTime);
        w.Write(0x2e);
        w.Write((uint)rng.Next(0x10000, 0xFFFFF));
        w.Write((ushort)0);
        w.Write((ushort)rng.Next(1, 0x40));
        w.Write(new byte[14]);
        w.Write((uint)rng.Next(0x100000, 0xFFFFFF));
        w.Write(name);
        w.Write(extStart);

        var time = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
        var b2b = Block(0xBEEF002Bu, time, extStart, null);
        var b1d = Block(0xBEEF001Du, Encoding.Unicode.GetBytes(appId + "\0"), extStart, 2);
        var b1e = Block(0xBEEF001Eu, Encoding.Unicode.GetBytes("UserPinned\0"), extStart, 2);
        foreach (var b in favoriteOrder ? new[] { b2b, b1d, b1e } : new[] { b1e, b2b, b1d }) w.Write(b);

        w.Flush();
        var bytes = ms.ToArray();
        BitConverter.GetBytes((ushort)bytes.Length).CopyTo(bytes, 0);
        return bytes;
    }

    static byte[] Block(uint signature, byte[] payload, ushort extStart, ushort? marker)
    {
        var size = 2 + 2 + 4 + (marker is null ? 0 : 2) + payload.Length + 2;
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write((ushort)size);
        w.Write((ushort)0);
        w.Write(signature);
        if (marker is { } m) w.Write(m);
        w.Write(payload);
        w.Write(extStart);
        return ms.ToArray();
    }

    static (ushort Date, ushort Time) DosNow()
    {
        var n = DateTime.Now;
        return ((ushort)(((n.Year - 1980) << 9) | (n.Month << 5) | n.Day), (ushort)((n.Hour << 11) | (n.Minute << 5) | (n.Second / 2)));
    }

    static byte[] ShortName(string linkName, HashSet<string> used)
    {
        var stem = Path.GetFileNameWithoutExtension(linkName);
        var ext = Path.GetExtension(linkName);
        string result;
        if (stem.Length <= 8 && stem.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && ext.Length == 4 && used.Add(linkName.ToUpperInvariant()))
        {
            result = linkName;
        }
        else
        {
            var clean = new string(stem.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
            if (clean.Length == 0) clean = "LINK";
            clean = clean[..Math.Min(6, clean.Length)];
            var n = 1;
            do { result = $"{clean}~{n++}.LNK"; } while (!used.Add(result));
        }
        var bytes = Encoding.ASCII.GetBytes(result + "\0");
        return bytes.Length % 2 == 0 ? bytes : [.. bytes, 0];
    }

    public static List<ParsedPin> Parse(byte[] favorites, byte[] resolve)
    {
        var favs = SplitFavorites(favorites);
        var ress = SplitResolve(resolve);
        var pins = new List<ParsedPin>();
        for (var i = 0; i < favs.Count && i < ress.Count; i++)
        {
            var (linkName, appId) = Inspect(favs[i]);
            appId ??= FindAumid(favs[i]);
            var display = linkName is not null
                ? Path.GetFileNameWithoutExtension(linkName)
                : appId is { Length: > 0 } ? ShellIcons.NameForAppId(appId) ?? appId.Split('_')[0].Split('.')[^1] : $"Item {i + 1}";
            pins.Add(new ParsedPin(display, linkName, appId, Frame(favs[i], true), Frame(ress[i], false)));
        }
        return pins;
    }

    static byte[] Frame(byte[] payload, bool favorite)
    {
        var ms = new MemoryStream();
        if (favorite) ms.WriteByte(0);
        ms.Write(BitConverter.GetBytes(payload.Length));
        ms.Write(payload);
        return ms.ToArray();
    }

    public static byte[] JoinFavorites(IEnumerable<byte[]> entries)
    {
        var ms = new MemoryStream();
        foreach (var e in entries) ms.Write(e);
        ms.WriteByte(0xFF);
        return ms.ToArray();
    }

    public static byte[] JoinResolve(IEnumerable<byte[]> entries)
    {
        var ms = new MemoryStream();
        foreach (var e in entries) ms.Write(e);
        return ms.ToArray();
    }

    static List<byte[]> SplitFavorites(byte[] b)
    {
        var list = new List<byte[]>();
        for (var i = 0; i < b.Length && b[i] == 0;)
        {
            var n = BitConverter.ToInt32(b, i + 1);
            list.Add(b[(i + 5)..(i + 5 + n)]);
            i += 5 + n;
        }
        return list;
    }

    static List<byte[]> SplitResolve(byte[] b)
    {
        var list = new List<byte[]>();
        for (var i = 0; i + 4 <= b.Length;)
        {
            var n = BitConverter.ToInt32(b, i);
            list.Add(b[(i + 4)..(i + 4 + n)]);
            i += 4 + n;
        }
        return list;
    }

    static (string? LinkName, string? AppId) Inspect(byte[] idList)
    {
        var items = new List<(int Offset, int Size)>();
        for (var i = 0; i + 2 <= idList.Length;)
        {
            var size = BitConverter.ToUInt16(idList, i);
            if (size == 0) break;
            items.Add((i, size));
            i += size;
        }
        if (items.Count != 3) return (null, null);

        var (offset, size2) = items[2];
        var item = idList[offset..(offset + size2)];
        var nameStart = 14;
        while (item[nameStart++] != 0) { }
        if ((nameStart - 14) % 2 == 1) nameStart++;
        var extStart = nameStart;
        var extSize = BitConverter.ToUInt16(item, extStart);
        var longName = ReadUtf16(item, extStart + 46);

        string? appId = null;
        for (var i = extStart + extSize; i + 10 <= item.Length;)
        {
            var size = BitConverter.ToUInt16(item, i);
            if (size < 10) break;
            if (BitConverter.ToUInt32(item, i + 4) == 0xBEEF001Du) appId = ReadUtf16(item, i + 10);
            i += size;
        }
        return (longName, appId);
    }

    static string FindAumid(byte[] data)
    {
        foreach (var text in new[] { Encoding.Unicode.GetString(data), Encoding.Unicode.GetString(data, 1, data.Length - 2) })
            foreach (Match m in Regex.Matches(text, @"[\w.\-]+_[a-z0-9]{13}![\w.]+"))
                if (AumidPattern().IsMatch(m.Value)) return m.Value;
        return "";
    }

    static string ReadUtf16(byte[] data, int offset)
    {
        var end = offset;
        while (end + 1 < data.Length && (data[end] != 0 || data[end + 1] != 0)) end += 2;
        return Encoding.Unicode.GetString(data, offset, end - offset);
    }
}
