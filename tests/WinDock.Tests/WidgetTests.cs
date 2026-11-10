using System.Text.Json;
using WinDock.Models;
using WinDock.Services;
using WinDock.ViewModels;

namespace WinDock.Tests;

public class WidgetTests
{
    [Fact]
    public void Widgets_wake_when_the_shown_number_turns()
    {
        var now = new DateTime(2026, 10, 14, 12, 34, 56, 700);
        Assert.Equal(new DateTime(2026, 10, 14, 12, 35, 0), WidgetTile.NextMinute(now));
        Assert.Equal(now.AddMilliseconds(300), WidgetTile.NextSecond(now, TimeSpan.FromMilliseconds(2300)));
        Assert.Equal(now.AddSeconds(1), WidgetTile.NextSecond(now, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Rigdeck_scenes_survive_a_round_trip()
    {
        var scene = new RigAction { Name = "Game night", Parallel = true, Icon = @"C:\icons\s.png", Steps = [
            new RigStepSpec { Target = @"C:\Games\game.exe", Monitor = 1, Fullscreen = true },
            new RigStepSpec { Type = RigStep.Keys, Target = "ctrl+shift+m" }] };
        var back = RigStore.Parse(RigStore.Format([scene]));
        Assert.Single(back);
        Assert.Equal(scene.Id, back[0].Id);
        Assert.Equal("Game night", back[0].Name);
        Assert.True(back[0].Parallel);
        Assert.Equal(2, back[0].Steps.Count);
        Assert.Equal(1, back[0].Steps[0].Monitor);
        Assert.True(back[0].Steps[0].Fullscreen);
        Assert.Null(back[0].Steps[1].Monitor);
        Assert.Null(RigStore.Format([]));
    }

    [Fact]
    public void Rigdeck_reads_the_first_line_format_and_drops_bad_lines()
    {
        var old = RigStore.Parse("""
            Steam|C:\icons\s.png|C:\Games\steam.exe
            Old|C:\old.exe
            no separator
            |no name
            no target|
            """);
        Assert.Equal(2, old.Count);
        Assert.Equal(@"C:\Games\steam.exe", old[0].Steps[0].Target);
        Assert.Equal(@"C:\icons\s.png", old[0].Icon);
        Assert.Equal(@"C:\old.exe", old[1].Steps[0].Target);
        Assert.Empty(RigStore.Parse("[not json"));
    }

    [Fact]
    public void Rigdeck_pins_launch_by_link_when_they_have_one_and_by_path_otherwise()
    {
        var icon = Path.GetTempFileName();
        var steam = RigStore.FromPin(new PinItem { Name = "Game", Target = "explorer.exe", Args = "steam://rungameid/730", AppId = "WinDock.Steam.730", Icon = icon });
        Assert.Equal("steam://rungameid/730", steam.Steps[0].Target);
        Assert.Equal(icon, steam.Icon);
        File.Delete(icon);

        var program = RigStore.FromPin(new PinItem { Name = "Tool", Target = @"C:\Tools\tool.exe" });
        Assert.Equal(@"C:\Tools\tool.exe", program.Steps[0].Target);
        Assert.Equal(@"C:\Tools\tool.exe", program.Icon);
    }

    [Fact]
    public void Rigdeck_drops_nameless_scenes_and_survives_broken_json()
    {
        Assert.Empty(RigStore.Parse("""[{"name":"","steps":[{"target":"x"}]},{"name":"No steps","steps":[]}]"""));
        Assert.Empty(RigStore.Parse("""[{"name":"cut off"""));
    }

    [Fact]
    public async Task A_combo_set_to_run_together_does_not_add_up_its_waits()
    {
        RigAction Combo(bool together) => new()
        {
            Parallel = together,
            Steps = [new RigStepSpec { Type = RigStep.Wait, Target = "0.3" }, new RigStepSpec { Type = RigStep.Wait, Target = "0.3" }]
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await RigRunner.Run(Combo(false));
        var inOrder = watch.Elapsed;
        watch.Restart();
        await RigRunner.Run(Combo(true));
        var together = watch.Elapsed;

        Assert.True(inOrder >= TimeSpan.FromMilliseconds(580), $"in order took {inOrder}");
        Assert.True(together < TimeSpan.FromMilliseconds(500), $"together took {together}");
    }

    [Fact]
    public void Steam_library_and_login_files_are_read()
    {
        var library = Games.ParseSteamLibrary("<gamesList><games><game><appID>730</appID><name>CS2</name></game><game><appID>1</appID></game></games></gamesList>");
        Assert.Equal([("730", "CS2")], library);
        Assert.Empty(Games.ParseSteamLibrary("<response><error>private</error></response>"));
        Assert.Empty(Games.ParseSteamLibrary("not xml"));

        const string users = """
            "users"
            {
                "76561198000000001" { "AccountName" "old" "MostRecent" "0" }
                "76561198000000002" { "AccountName" "now" "MostRecent" "1" }
            }
            """;
        Assert.Equal("76561198000000002", Games.ParseRecentSteamUser(users));
        Assert.Null(Games.ParseRecentSteamUser("\"users\" { }"));
    }

    [Theory]
    [InlineData("ctrl+shift+m", new byte[] { 0x11, 0x10, 0x4D })]
    [InlineData("Win + D", new byte[] { 0x5B, 0x44 })]
    [InlineData("f13", new byte[] { 0x7C })]
    [InlineData("volumemute", new byte[] { 0xAD })]
    public void Key_combos_become_virtual_keys(string text, byte[] expected) => Assert.Equal(expected, RigStore.Chord(text));

    [Theory]
    [InlineData("")]
    [InlineData("ctrl+banana")]
    [InlineData("f99")]
    public void Unknown_key_combos_are_ignored(string text) => Assert.Null(RigStore.Chord(text));

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(512, "512 B/s")]
    [InlineData(2048, "2 KB/s")]
    [InlineData(1.5 * 1024 * 1024, "1.5 MB/s")]
    [InlineData(250.0 * 1024 * 1024, "250 MB/s")]
    public void Network_rates_read_like_the_task_manager(double bytes, string expected) =>
        Assert.Equal(expected, NetworkMeter.Rate(bytes));

    [Fact]
    public void Network_meter_needs_two_samples_for_a_rate()
    {
        var meter = new NetworkMeter();
        var t0 = DateTime.Now;
        Assert.Equal((0.0, 0.0), meter.Sample(t0));
        var (down, up) = meter.Sample(t0.AddSeconds(1));
        Assert.True(down >= 0 && up >= 0);
    }

    [Fact]
    public void Pomodoro_counts_the_cycles_finished_today_and_starts_over_tomorrow()
    {
        var p = new PomodoroWidget("p", new WidgetSpec { Kind = WidgetSpec.Pomodoro, Minutes = 1 });
        var t = new DateTime(2026, 10, 12, 22, 0, 0);
        for (var i = 0; i < 2; i++)
        {
            p.Click(t);
            t = t.AddMinutes(1);
            p.Tick(t);
            t = t.AddMinutes(PomodoroWidget.ShortBreak);
            p.Tick(t);
        }
        Assert.Equal(2, p.Today);
        Assert.Equal("2", p.Done);

        p.Tick(new DateTime(2026, 10, 13, 8, 0, 0));
        Assert.Equal("", p.Done);
        p.Click(new DateTime(2026, 10, 13, 8, 0, 0));
        p.Tick(new DateTime(2026, 10, 13, 8, 1, 0));
        Assert.Equal(1, p.Today);
    }

    [Fact]
    public void Widgets_that_change_their_settings_ask_to_be_saved()
    {
        var saved = new List<string>();
        var timer = new TimerWidget("t", new WidgetSpec { Kind = WidgetSpec.Timer, Minutes = 10 });
        var progress = new ProgressWidget("p", new WidgetSpec { Kind = WidgetSpec.Progress });
        var note = new NoteWidget("n", new WidgetSpec { Kind = WidgetSpec.Note });
        foreach (var w in new WidgetTile[] { timer, progress, note }) w.Edited += x => saved.Add(x.Kind);

        timer.Wheel(2);
        progress.Click(DateTime.Now);
        note.Note = "  comprar pão  ";
        note.Note = "comprar pão";

        Assert.Equal([WidgetSpec.Timer, WidgetSpec.Progress, WidgetSpec.Note], saved);
        Assert.Equal(12, timer.Spec.Minutes);
        Assert.Equal("week", progress.Spec.Label);
        Assert.Equal("comprar pão", note.Spec.Text);
        Assert.Equal("comprar pão", note.Text);
    }

    [Fact]
    public void Empty_note_shows_a_hint_and_stores_nothing()
    {
        var note = new NoteWidget("n", new WidgetSpec { Kind = WidgetSpec.Note, Text = "old" });
        note.Note = "   ";
        Assert.Null(note.Spec.Text);
        Assert.True(note.Empty);
        Assert.Equal(Loc.T("noteEmpty"), note.Text);
    }

    [Fact]
    public void Every_widget_kind_has_a_name_and_a_tile()
    {
        foreach (var kind in WidgetSpec.Kinds)
        {
            Assert.NotEqual("widget_" + kind, Loc.T("widget_" + kind));
            var tile = WidgetTile.Create("k", new WidgetSpec { Kind = kind });
            Assert.Equal(kind, tile.Kind);
        }
    }

    [Fact]
    public void Weather_reads_the_open_meteo_forecast()
    {
        const string json = """
        {
          "current": { "time": "2026-10-12T14:15", "temperature_2m": 24.6, "apparent_temperature": 26.1, "weather_code": 2, "is_day": 1 },
          "hourly": {
            "time": ["2026-10-12T14:00", "2026-10-12T15:00", "2026-10-12T16:00", "2026-10-12T19:00"],
            "temperature_2m": [24.0, 25.2, null, 19.4],
            "weather_code": [2, 3, 61, 0],
            "is_day": [1, 1, 1, 0]
          },
          "daily": { "temperature_2m_max": [27.3, 28.0], "temperature_2m_min": [16.8, 17.1] }
        }
        """;
        var report = Weather.Parse(json, "Belo Horizonte", DateTime.Now)!;
        Assert.Equal("25°", Weather.Degrees(report.Temp));
        Assert.Equal("0°", Weather.Degrees(-0.3));
        Assert.Equal((27.3, 16.8), (report.High, report.Low));
        Assert.Equal([15, 19], report.Hours.Select(h => h.Time.Hour));
        Assert.False(report.Hours[^1].Day);
        Assert.Equal("weatherPartly", Weather.Describe(report.Code, report.Day).Key);
        Assert.Equal("weatherStorm", Weather.Describe(96, true).Key);
        Assert.Null(Weather.Parse("{}", "x", DateTime.Now));
    }

    [Fact]
    public void Small_icons_lose_the_shell_frame()
    {
        const int size = 128;
        var px = new byte[size * size * 4];
        void Paint(int x, int y, byte a) { var o = (y * size + x) * 4; px[o] = px[o + 1] = px[o + 2] = 0xFF; px[o + 3] = a; }
        for (var i = 0; i < size; i++) { Paint(i, 0, 0x40); Paint(0, i, 0x40); Paint(i, size - 1, 0x40); Paint(size - 1, i, 0x40); }
        for (var y = 40; y < 88; y++) for (var x = 40; x < 88; x++) Paint(x, y, 0xFF);
        var small = System.Windows.Media.Imaging.BitmapSource.Create(size, size, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, px, size * 4);
        Assert.InRange(ShellIcons.Trim(small).PixelWidth, 48, 56);

        for (var y = 8; y < 120; y++) for (var x = 8; x < 120; x++) Paint(x, y, 0xFF);
        var full = System.Windows.Media.Imaging.BitmapSource.Create(size, size, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, px, size * 4);
        Assert.Same(full, ShellIcons.Trim(full));
    }

    [Fact]
    public void Note_text_survives_the_preset_file()
    {
        var preset = new Preset { Widgets = [new WidgetSpec { Kind = WidgetSpec.Note, Text = "linha 1\nlinha 2" }] };
        var back = JsonSerializer.Deserialize<Preset>(JsonSerializer.Serialize(preset, Json.Options), Json.Options)!;
        Assert.Equal("linha 1\nlinha 2", Assert.Single(back.Widgets!).Text);
    }

    [Fact]
    public void Uninstaller_finds_the_program_and_its_command()
    {
        var code = new InstalledApp("Visual Studio Code", "Microsoft", @"D:\Apps\Microsoft VS Code", @"""D:\Apps\Microsoft VS Code\unins000.exe""");
        var nested = new InstalledApp("Code Helper", null, @"D:\Apps\Microsoft VS Code\helper", "helper.exe /uninstall");
        var msi = new InstalledApp("7-Zip 24.08 (x64)", "Igor Pavlov", null, "MsiExec.exe /I{23170F69-40C1-2702-2408-000001000000}");
        InstalledApp[] apps = [code, nested, msi];

        Assert.Equal(code, Uninstaller.Match(apps, @"D:\Apps\Microsoft VS Code\Code.exe", "Code"));
        Assert.Equal(nested, Uninstaller.Match(apps, @"D:\Apps\Microsoft VS Code\helper\h.exe", null));
        Assert.Equal(msi, Uninstaller.Match(apps, null, "7-Zip"));
        Assert.Null(Uninstaller.Match(apps, @"D:\Other\tool.exe", "zz"));

        Assert.Equal(("msiexec.exe", "/x {23170F69-40C1-2702-2408-000001000000} /passive /norestart", true), Uninstaller.Command(msi));
        Assert.Equal((@"D:\Apps\Microsoft VS Code\unins000.exe", "/SILENT /SUPPRESSMSGBOXES /NORESTART", true), Uninstaller.Command(code));
        Assert.Equal((@"C:\Program Files\App\uninstall.exe", "--force"), Uninstaller.Split(@"C:\Program Files\App\uninstall.exe --force"));

        Assert.True(Uninstaller.Broken(@"C:\WinDockNoSuchFolder\unins000.exe"));
        Assert.False(Uninstaller.Broken("msiexec.exe"));
        Assert.False(Uninstaller.Broken(Path.Combine(Environment.SystemDirectory, "cmd.exe")));

        Assert.True(Uninstaller.Related("Microsoft VS Code", code));
        Assert.False(Uninstaller.Related("Games", code));
        Assert.Null(Uninstaller.Specific(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)));
        Assert.Null(Uninstaller.Specific(@"C:\"));
        Assert.Null(Uninstaller.Specific(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\Installer\x"));
    }

    [Fact]
    public async Task Uninstaller_treats_a_vanished_folder_as_removed_and_drops_the_orphan_entry()
    {
        var id = $"WinDockOrphan{Guid.NewGuid():N}";
        var regPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + id;
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), id)).FullName.TrimEnd('\\');
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regPath)) key.SetValue("DisplayName", id);
            var app = new InstalledApp(id, null, folder, "steam.exe steam://uninstall/1", Hive: Microsoft.Win32.RegistryHive.CurrentUser, Id: id);
            Assert.False(Uninstaller.Gone(app with { Command = "x.exe" }, hadFolder: true));
            Directory.Delete(folder);
            Assert.True(Uninstaller.Gone(app with { Command = "x.exe" }, hadFolder: true));
            Assert.False(Uninstaller.Gone(app with { Command = "x.exe" }, hadFolder: false));
            Assert.Equal(0, await Uninstaller.Clean([], app));
            Assert.False(Uninstaller.StillInstalled(app));
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regPath)) key.SetValue("Other", "x");
            Assert.False(Uninstaller.StillInstalled(app));
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(regPath, false);
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
    }

    [Fact]
    public void Uninstaller_finds_the_folder_of_a_program_without_an_uninstaller()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"WinDockGames{Guid.NewGuid():N}")).FullName;
        try
        {
            var game = Path.Combine(root, "Days Gone");
            var exe = Path.Combine(game, "BendGame", "Win64", "DaysGone.exe");
            var hidden = Path.Combine(root, ".claude", "claude.exe");
            foreach (var file in new[] { exe, hidden })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, "x");
            }
            Assert.Equal(game, Uninstaller.PortableFolder(exe, []));
            Assert.Null(Uninstaller.PortableFolder(hidden, []));
            Assert.Null(Uninstaller.PortableFolder(exe, [new InstalledApp("Other", null, Path.Combine(game, "BendGame"))]));
            Assert.Equal([game], Uninstaller.Remnants(game, exe));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Converter_tells_images_animations_videos_and_audio_apart()
    {
        Assert.Equal(MediaKind.Image, Converter.Classify("""{"streams":[{"codec_name":"png","codec_type":"video","disposition":{"attached_pic":0}}],"format":{"format_name":"png_pipe"}}""").Kind);
        Assert.Equal(MediaKind.Animation, Converter.Classify("""{"streams":[{"codec_name":"gif","codec_type":"video","nb_frames":"20","disposition":{"attached_pic":0}}],"format":{"format_name":"gif","duration":"2.000000"}}""").Kind);
        var video = Converter.Classify("""{"streams":[{"codec_name":"h264","codec_type":"video","disposition":{"attached_pic":0}},{"codec_name":"aac","codec_type":"audio","disposition":{"attached_pic":0}}],"format":{"format_name":"mov,mp4","duration":"3.000000"}}""");
        Assert.Equal((MediaKind.Video, 3.0), (video.Kind, video.Duration));
        Assert.Equal(MediaKind.Audio, Converter.Classify("""{"streams":[{"codec_name":"mp3","codec_type":"audio"},{"codec_name":"mjpeg","codec_type":"video","disposition":{"attached_pic":1}}],"format":{"format_name":"mp3","duration":"2.0"}}""").Kind);
        Assert.Equal(MediaKind.Unknown, Converter.Classify("not json").Kind);

        var forPng = Converter.TargetsFor(MediaKind.Image, @"C:\a\photo.png").Select(t => t.Id).ToList();
        Assert.DoesNotContain("png", forPng);
        Assert.Contains("jpg", forPng);
        Assert.DoesNotContain("mp3", forPng);
        Assert.Contains("mp4small", Converter.TargetsFor(MediaKind.Video, @"C:\a\clip.mp4").Select(t => t.Id));

        Assert.Equal(MediaKind.Document, Converter.KindByExtension(@"C:\a\notes.TXT"));
        Assert.Equal(MediaKind.Pdf, Converter.KindByExtension(@"C:\a\cv.pdf"));
        Assert.Equal(MediaKind.Unknown, Converter.KindByExtension(@"C:\a\setup.exe"));
        var forTxt = Converter.TargetsFor(MediaKind.Document, @"C:\a\notes.txt").Select(t => t.Id).ToList();
        Assert.Equal(["pdf", "docx", "odt", "rtf"], forTxt);
        Assert.Contains("txt", Converter.TargetsFor(MediaKind.Document, @"C:\a\cv.docx").Select(t => t.Id));
        Assert.Equal(["pdftxt", "pdfdocx", "pdfpng"], Converter.TargetsFor(MediaKind.Pdf, @"C:\a\cv.pdf").Select(t => t.Id));
    }

    [Fact]
    public void Shelf_keeps_each_existing_file_once_and_forgets_the_ones_that_vanish()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"WinDockShelf{Guid.NewGuid():N}")).FullName;
        try
        {
            var a = Path.Combine(dir, "a.jpg");
            var b = Path.Combine(dir, "b.jpg");
            File.WriteAllText(a, "a");
            File.WriteAllText(b, "b");
            var shelf = new ShelfWidget("s", new WidgetSpec { Kind = WidgetSpec.Shelf });
            Assert.Equal(2, shelf.Add([a, b, a.ToUpperInvariant(), Path.Combine(dir, "missing.jpg")]));
            Assert.Equal(0, shelf.Add([b]));
            Assert.Equal([a, b], shelf.Files);
            Assert.Equal(2, shelf.Count);
            File.Delete(a);
            shelf.Prune();
            Assert.Equal([b], ShelfWidget.Parse(shelf.Spec.Text));
            shelf.Clear();
            Assert.Null(shelf.Spec.Text);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Cleanup_only_calls_a_shortcut_dead_when_its_drive_is_here()
    {
        Assert.True(Cleanup.Dead(Path.Combine(Path.GetTempPath(), $"gone{Guid.NewGuid():N}", "game.exe")));
        Assert.False(Cleanup.Dead(Path.Combine(Environment.SystemDirectory, "cmd.exe")));
        Assert.False(Cleanup.Dead(null));
        Assert.False(Cleanup.Dead(@"\\server\share\app.exe"));
        var missingDrive = Enumerable.Range('D', 23).Select(c => $"{(char)c}:\\").First(d => !Directory.Exists(d));
        Assert.False(Cleanup.Dead(missingDrive + @"Games\game.exe"));
        Assert.NotEmpty(Cleanup.Bytes(1536));
    }

    [Fact]
    public void Converter_never_offers_video_formats_for_text_files()
    {
        var file = Path.Combine(Path.GetTempPath(), $"WinDock{Guid.NewGuid():N}.txt");
        File.WriteAllText(file, string.Concat(Enumerable.Repeat("linha de texto comum\n", 400)));
        try { Assert.Equal(MediaKind.Document, Converter.Probe(file).Kind); }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Converter_creates_the_copy_next_to_the_source_without_overwriting()
    {
        if (Converter.Ffmpeg() is null) return;
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"WinDockConvert{Guid.NewGuid():N}")).FullName;
        try
        {
            var source = Path.Combine(dir, "clip.wav");
            var mp3 = Converter.Targets.Single(t => t.Id == "mp3");
            File.WriteAllText(Path.Combine(dir, "clip.mp3"), "taken");
            var output = Converter.OutputFor(source, mp3);
            Assert.Equal(Path.Combine(dir, "clip (1).mp3"), output);

            var make = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Converter.Ffmpeg()!, $"-v error -f lavfi -i sine=d=2 \"{source}\"") { CreateNoWindow = true })!;
            await make.WaitForExitAsync();
            var info = Converter.Probe(source);
            Assert.Equal(MediaKind.Audio, info.Kind);
            var reports = new List<double>();
            var error = await Converter.Run(source, output, mp3, info.Duration, new SyncProgress(reports.Add), CancellationToken.None);
            Assert.Null(error);
            Assert.True(new FileInfo(output).Length > 0);
            Assert.Equal(1, reports[^1]);
            Assert.Equal("taken", File.ReadAllText(Path.Combine(dir, "clip.mp3")));
        }
        finally { Directory.Delete(dir, true); }
    }

    sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    [Fact]
    public void Uninstaller_sends_plain_files_to_the_bin_instead_of_matching_a_program()
    {
        var file = Path.Combine(Path.GetTempPath(), $"Steam-{Guid.NewGuid():N}.txt");
        File.WriteAllText(file, "x");
        try
        {
            InstalledApp[] apps = [new("Steam", "Valve", Path.GetTempPath().TrimEnd('\\'))];
            Assert.Null(Uninstaller.Find(file, null, null, apps));
            Assert.True(Uninstaller.Trashable(file));
            Assert.False(Uninstaller.Trashable(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)));
            Assert.False(Uninstaller.Trashable(file + ".missing"));
        }
        finally { File.Delete(file); }
    }
}
