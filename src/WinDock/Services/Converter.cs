using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

namespace WinDock.Services;

public enum MediaKind { Unknown, Image, Animation, Video, Audio, Document, Pdf }

public enum ConvertEngine { Ffmpeg, Office, PdfText }

public sealed record MediaProbe(MediaKind Kind, double Duration);

public sealed record ConvertTarget(string Id, string Extension, MediaKind[] From, string[] Args, ConvertEngine Engine = ConvertEngine.Ffmpeg)
{
    public string Label => Loc.T("convert_" + Id);
}

public static class Converter
{
    const string EvenSize = "scale=trunc(iw/2)*2:trunc(ih/2)*2";
    const string OnWhite = "color=white,format=rgb24[bg];[bg][0:v]scale2ref[bg][img];[bg][img]overlay=shortest=1,format=yuvj444p";
    const string GifPalette = "fps=15,scale='min(720,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse";
    const string IconSize = "scale='min(256,iw)':'min(256,ih)':force_original_aspect_ratio=decrease";
    static readonly TimeSpan OfficeLimit = TimeSpan.FromMinutes(5);

    static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".jfif", ".webp", ".bmp", ".tif", ".tiff", ".gif", ".ico", ".tga", ".qoi"];
    static readonly string[] VideoExtensions = [".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".flv", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".3gp", ".ogv"];
    static readonly string[] AudioExtensions = [".mp3", ".wav", ".flac", ".ogg", ".oga", ".opus", ".m4a", ".aac", ".wma", ".aiff", ".aif", ".amr"];
    static readonly string[] DocumentExtensions = [".doc", ".docx", ".odt", ".rtf", ".txt", ".html", ".htm"];
    static readonly string[] ImageCodecs = ["png", "mjpeg", "webp", "bmp", "tiff", "gif", "jpegls", "jpeg2000", "targa", "ico", "qoi"];

    public static readonly ConvertTarget[] Targets =
    [
        new("png", ".png", [MediaKind.Image], ["-frames:v", "1"]),
        new("jpg", ".jpg", [MediaKind.Image], ["-filter_complex", OnWhite, "-frames:v", "1", "-q:v", "2"]),
        new("webp", ".webp", [MediaKind.Image], ["-c:v", "libwebp", "-quality", "90", "-frames:v", "1"]),
        new("bmp", ".bmp", [MediaKind.Image], ["-frames:v", "1"]),
        new("ico", ".ico", [MediaKind.Image], ["-vf", IconSize, "-frames:v", "1"]),
        new("mp4", ".mp4", [MediaKind.Video, MediaKind.Animation], ["-vf", EvenSize, "-c:v", "libx264", "-preset", "fast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"]),
        new("mp4small", ".mp4", [MediaKind.Video], ["-vf", EvenSize, "-c:v", "libx264", "-preset", "medium", "-crf", "30", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart"]),
        new("webm", ".webm", [MediaKind.Video, MediaKind.Animation], ["-c:v", "libvpx-vp9", "-crf", "33", "-b:v", "0", "-row-mt", "1", "-deadline", "good", "-cpu-used", "4", "-c:a", "libopus", "-b:a", "128k"]),
        new("mkv", ".mkv", [MediaKind.Video], ["-map", "0", "-c", "copy"]),
        new("gif", ".gif", [MediaKind.Video], ["-filter_complex", GifPalette, "-loop", "0"]),
        new("webpanim", ".webp", [MediaKind.Animation], ["-c:v", "libwebp_anim", "-quality", "80", "-loop", "0"]),
        new("mp3", ".mp3", [MediaKind.Video, MediaKind.Audio], ["-vn", "-c:a", "libmp3lame", "-q:a", "2"]),
        new("m4a", ".m4a", [MediaKind.Audio], ["-vn", "-c:a", "aac", "-b:a", "192k"]),
        new("wav", ".wav", [MediaKind.Video, MediaKind.Audio], ["-vn", "-c:a", "pcm_s16le"]),
        new("flac", ".flac", [MediaKind.Audio], ["-vn", "-c:a", "flac"]),
        new("ogg", ".ogg", [MediaKind.Audio], ["-vn", "-c:a", "libvorbis", "-q:a", "5"]),
        new("opus", ".opus", [MediaKind.Audio], ["-vn", "-c:a", "libopus", "-b:a", "128k"]),
        new("pdf", ".pdf", [MediaKind.Document], ["pdf"], ConvertEngine.Office),
        new("docx", ".docx", [MediaKind.Document], ["docx:MS Word 2007 XML"], ConvertEngine.Office),
        new("odt", ".odt", [MediaKind.Document], ["odt"], ConvertEngine.Office),
        new("rtf", ".rtf", [MediaKind.Document], ["rtf"], ConvertEngine.Office),
        new("txt", ".txt", [MediaKind.Document], ["txt:Text (encoded):UTF8"], ConvertEngine.Office),
        new("pdftxt", ".txt", [MediaKind.Pdf], ["-enc", "UTF-8", "-layout"], ConvertEngine.PdfText),
        new("pdfdocx", ".docx", [MediaKind.Pdf], ["docx:MS Word 2007 XML"], ConvertEngine.Office),
        new("pdfpng", ".png", [MediaKind.Pdf], ["png"], ConvertEngine.Office)
    ];

    public static IEnumerable<ConvertTarget> TargetsFor(MediaKind kind, string source) =>
        Targets.Where(t => t.From.Contains(kind) && (t.Id == "mp4small" || !t.Extension.Equals(Path.GetExtension(source), StringComparison.OrdinalIgnoreCase)));

    public static MediaKind KindByExtension(string file)
    {
        var extension = Path.GetExtension(file).ToLowerInvariant();
        if (extension == ".pdf") return MediaKind.Pdf;
        if (DocumentExtensions.Contains(extension)) return MediaKind.Document;
        if (extension == ".gif") return MediaKind.Animation;
        if (ImageExtensions.Contains(extension)) return MediaKind.Image;
        if (VideoExtensions.Contains(extension)) return MediaKind.Video;
        return AudioExtensions.Contains(extension) ? MediaKind.Audio : MediaKind.Unknown;
    }

    public static bool Has(ConvertEngine engine) => Engine(engine) is not null;

    static string? Engine(ConvertEngine engine) => engine switch
    {
        ConvertEngine.Ffmpeg => Ffmpeg(),
        ConvertEngine.Office => Office(),
        _ => Find("pdftotext.exe")
    };

    public static string? Ffmpeg() => Find("ffmpeg.exe");

    static string? Ffprobe() => Find("ffprobe.exe");

    static string? Office()
    {
        var registered = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe", "", null) as string;
        var exe = new[]
        {
            registered,
            Find("soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"LibreOffice\program\soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"LibreOffice\program\soffice.exe")
        }.FirstOrDefault(File.Exists);
        if (exe is null) return null;
        var console = Path.ChangeExtension(exe, ".com");
        return File.Exists(console) ? console : exe;
    }

    static string? Find(string exe)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Append(Path.Combine(local, @"Microsoft\WinGet\Links"))
            .Append(@"C:\ffmpeg\bin")
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"ffmpeg\bin"));
        foreach (var dir in path)
        {
            try
            {
                var candidate = Path.Combine(Environment.ExpandEnvironmentVariables(dir.Trim().Trim('"')), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        var packages = Path.Combine(local, @"Microsoft\WinGet\Packages");
        if (!Directory.Exists(packages)) return null;
        return Directory.EnumerateDirectories(packages, "Gyan.FFmpeg*")
            .SelectMany(d => Directory.EnumerateFiles(d, exe, new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3 }))
            .FirstOrDefault();
    }

    public static bool Install(ConvertEngine engine)
    {
        var id = engine == ConvertEngine.Office ? "TheDocumentFoundation.LibreOffice" : "Gyan.FFmpeg";
        try
        {
            using var process = Process.Start(new ProcessStartInfo("winget", $"install -e --id {id} --accept-source-agreements --accept-package-agreements") { UseShellExecute = true });
            return process is not null;
        }
        catch (Win32Exception) { return false; }
    }

    public static MediaProbe Probe(string file)
    {
        var kind = KindByExtension(file);
        if (kind is MediaKind.Unknown or MediaKind.Document or MediaKind.Pdf || !File.Exists(file)) return new(File.Exists(file) ? kind : MediaKind.Unknown, 0);
        if (Ffprobe() is not { } ffprobe) return new(kind, 0);
        var info = new ProcessStartInfo(ffprobe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] { "-v", "error", "-show_entries", "format=duration,format_name:stream=codec_type,codec_name,nb_frames:stream_disposition=attached_pic", "-of", "json", file })
            info.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(info)!;
            var json = process.StandardOutput.ReadToEnd();
            process.WaitForExit(15000);
            return Classify(json);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException) { return new(MediaKind.Unknown, 0); }
    }

    public static MediaProbe Classify(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var format = root.TryGetProperty("format", out var f) ? f : default;
            var formatName = format.ValueKind == JsonValueKind.Object && format.TryGetProperty("format_name", out var fn) ? fn.GetString() ?? "" : "";
            var duration = format.ValueKind == JsonValueKind.Object && format.TryGetProperty("duration", out var d)
                           && double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : 0;
            if (!root.TryGetProperty("streams", out var streams)) return new(MediaKind.Unknown, 0);
            var video = streams.EnumerateArray().Where(s => Text(s, "codec_type") == "video" && !Cover(s)).ToList();
            var audio = streams.EnumerateArray().Any(s => Text(s, "codec_type") == "audio");
            if (video.Count == 0) return new(audio ? MediaKind.Audio : MediaKind.Unknown, duration);
            var codec = Text(video[0], "codec_name");
            var frames = int.TryParse(Text(video[0], "nb_frames"), out var n) ? n : 0;
            if (codec == "gif" || codec == "webp" && formatName.Contains("webp"))
                return new(frames > 1 || duration > 0.2 ? MediaKind.Animation : MediaKind.Image, duration);
            if (ImageCodecs.Contains(codec) && (formatName.Contains("pipe") || formatName.Contains("image2") || duration == 0))
                return new(MediaKind.Image, 0);
            return new(MediaKind.Video, duration);
        }
        catch (JsonException) { return new(MediaKind.Unknown, 0); }
    }

    static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString() : "";

    static bool Cover(JsonElement stream) =>
        stream.TryGetProperty("disposition", out var disposition)
        && disposition.TryGetProperty("attached_pic", out var pic)
        && pic.ValueKind == JsonValueKind.Number && pic.GetInt32() == 1;

    public static string OutputFor(string source, ConvertTarget target)
    {
        var dir = Path.GetDirectoryName(source)!;
        var name = Path.GetFileNameWithoutExtension(source) + (target.Id == "mp4small" ? " (small)" : "");
        var candidate = Path.Combine(dir, name + target.Extension);
        for (var i = 1; File.Exists(candidate); i++) candidate = Path.Combine(dir, $"{name} ({i}){target.Extension}");
        return candidate;
    }

    public static Task<string?> Run(string source, string output, ConvertTarget target, double duration, IProgress<double> progress, CancellationToken token) => target.Engine switch
    {
        ConvertEngine.Office => RunOffice(source, output, target, progress, token),
        ConvertEngine.PdfText => RunTool(Find("pdftotext.exe"), [.. target.Args, source, output], output, progress, token),
        _ => RunFfmpeg(source, output, target, duration, progress, token)
    };

    static async Task<string?> RunFfmpeg(string source, string output, ConvertTarget target, double duration, IProgress<double> progress, CancellationToken token)
    {
        if (Ffmpeg() is not { } ffmpeg) return Loc.T("convertNoFfmpeg");
        var args = new[] { "-hide_banner", "-nostdin", "-n", "-i", source }.Concat(target.Args).Concat(["-progress", "pipe:1", "-nostats", output]).ToList();
        return await Execute(ffmpeg, args, output, token, line =>
        {
            if (duration <= 0 || !line.StartsWith("out_time_us=", StringComparison.Ordinal)) return;
            if (long.TryParse(line["out_time_us=".Length..], out var micros)) progress.Report(Math.Clamp(micros / 1_000_000.0 / duration, 0, 0.99));
        }) ?? Done(progress);
    }

    static async Task<string?> RunTool(string? tool, IReadOnlyList<string> args, string output, IProgress<double> progress, CancellationToken token)
    {
        if (tool is null) return Loc.T("convertNoTool");
        return await Execute(tool, args, output, token, null) ?? Done(progress);
    }

    static async Task<string?> RunOffice(string source, string output, ConvertTarget target, IProgress<double> progress, CancellationToken token)
    {
        if (Office() is not { } office) return Loc.T("convertNoOffice");
        var work = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "windock-convert-" + Guid.NewGuid().ToString("N"))).FullName;
        var outDir = Path.Combine(work, "out");
        try
        {
            var args = new List<string> { "-env:UserInstallation=" + new Uri(Path.Combine(work, "profile")).AbsoluteUri, "--headless", "--norestore", "--nologo" };
            if (InFilter(source, target) is { } filter) args.Add("--infilter=" + filter);
            args.AddRange(["--convert-to", target.Args[0], "--outdir", outDir, source]);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
            limit.CancelAfter(OfficeLimit);
            var error = await Execute(office, args, null, limit.Token, null);
            if (error is not null) return error;
            var produced = Directory.Exists(outDir) ? Directory.EnumerateFiles(outDir).FirstOrDefault() : null;
            if (produced is null) return Loc.T("convertNoOutput");
            File.Move(produced, output);
            return Done(progress);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Loc.T("convertTimeout"); }
        finally
        {
            try { Directory.Delete(work, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    static string? InFilter(string source, ConvertTarget target)
    {
        if (target.Id == "pdfdocx") return "writer_pdf_import";
        return Path.GetExtension(source).ToLowerInvariant() switch
        {
            ".txt" => "Text (encoded):UTF8,LF,,,",
            ".html" or ".htm" => "HTML (StarWriter)",
            _ => null
        };
    }

    static string? Done(IProgress<double> progress)
    {
        progress.Report(1);
        return null;
    }

    static async Task<string?> Execute(string exe, IEnumerable<string> args, string? output, CancellationToken token, Action<string>? onLine)
    {
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        var errors = new Queue<string>();
        using var process = new Process { StartInfo = info };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not { Length: > 0 } line) return;
            lock (errors)
            {
                errors.Enqueue(line);
                if (errors.Count > 6) errors.Dequeue();
            }
        };
        try
        {
            process.Start();
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException) { return e.Message; }
        process.BeginErrorReadLine();
        using var kill = token.Register(() =>
        {
            try { process.Kill(true); }
            catch (InvalidOperationException) { }
        });
        while (await process.StandardOutput.ReadLineAsync(CancellationToken.None) is { } line) onLine?.Invoke(line);
        await process.WaitForExitAsync(CancellationToken.None);
        if (!token.IsCancellationRequested && process.ExitCode == 0) return null;
        if (output is not null) TryDelete(output);
        token.ThrowIfCancellationRequested();
        lock (errors) return errors.LastOrDefault() ?? $"{Path.GetFileNameWithoutExtension(exe)} {process.ExitCode}";
    }

    static void TryDelete(string file)
    {
        try { File.Delete(file); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
