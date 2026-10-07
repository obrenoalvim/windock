using System.Drawing;
using System.Windows;
using Microsoft.Win32;
using WinDock.ViewModels;
using WinDock.Views;
using Forms = System.Windows.Forms;

namespace WinDock.Services;

public sealed class TrayService : IDisposable
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    readonly MainViewModel vm;
    readonly ConfigWindow window;
    readonly Forms.NotifyIcon icon;

    public TrayService(MainViewModel vm, ConfigWindow window)
    {
        this.vm = vm;
        this.window = window;

        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream;
        icon = new Forms.NotifyIcon
        {
            Icon = new Icon(stream),
            Text = "WinDock",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) window.Toggle(); };
        icon.ContextMenuStrip.Opening += (_, _) => Rebuild();

        vm.Notice += text => icon.ShowBalloonTip(2500, "WinDock", text, Forms.ToolTipIcon.None);
    }

    void Rebuild()
    {
        var menu = icon.ContextMenuStrip!.Items;
        menu.Clear();
        menu.Add("Abrir configuração", null, (_, _) => window.Open());
        menu.Add(new Forms.ToolStripSeparator());

        foreach (var p in vm.Presets)
        {
            var preset = p;
            menu.Add(new Forms.ToolStripMenuItem($"{p.Glyph}  {p.Model.Name}", null, async (_, _) => await vm.ApplyAsync(preset, auto: false))
            {
                Checked = p.IsActive
            });
        }

        menu.Add(new Forms.ToolStripSeparator());
        menu.Add(new Forms.ToolStripMenuItem("Troca automática por horário", null, (_, _) => vm.AutoSwitch = !vm.AutoSwitch) { Checked = vm.AutoSwitch });
        menu.Add(new Forms.ToolStripMenuItem("Avisar trocas", null, (_, _) => vm.NotifySwitch = !vm.NotifySwitch) { Checked = vm.NotifySwitch });
        menu.Add(new Forms.ToolStripMenuItem("Iniciar com o Windows", null, (_, _) => SetAutostart(!IsAutostart())) { Checked = IsAutostart() });
        menu.Add(new Forms.ToolStripSeparator());
        menu.Add("Sair", null, (_, _) => Application.Current.Shutdown());
    }

    static bool IsAutostart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("WinDock") is not null;
    }

    static void SetAutostart(bool on)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;
        if (on) key.SetValue("WinDock", $"\"{Environment.ProcessPath}\" --tray");
        else key.DeleteValue("WinDock", throwOnMissingValue: false);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
    }
}
