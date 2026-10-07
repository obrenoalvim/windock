using System.Diagnostics;
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
    readonly DockWindow window;
    readonly Forms.NotifyIcon icon;

    public TrayService(MainViewModel vm, DockWindow window)
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
        menu.Add("Mostrar / ocultar", null, (_, _) => window.Toggle());
        menu.Add(new Forms.ToolStripSeparator());

        foreach (var p in vm.Presets)
        {
            var preset = p;
            var item = new Forms.ToolStripMenuItem($"{p.Glyph}  {p.Model.Name}", null, (_, _) => vm.SelectCommand.Execute(preset))
            {
                Checked = p.IsActive
            };
            menu.Add(item);
        }

        menu.Add(new Forms.ToolStripSeparator());
        menu.Add(Toggle("Troca automática por horário", vm.Settings.AutoSwitch, v => vm.Settings.AutoSwitch = v));
        menu.Add(Toggle("Ocultar ao perder o foco", vm.Settings.AutoHide, v => vm.Settings.AutoHide = v));
        menu.Add(Toggle("Notificar trocas", vm.Settings.Notify, v => vm.Settings.Notify = v));
        menu.Add(new Forms.ToolStripMenuItem("Iniciar com o Windows", null, (_, _) => SetAutostart(!IsAutostart())) { Checked = IsAutostart() });
        menu.Add(new Forms.ToolStripSeparator());
        menu.Add("Abrir pasta de presets", null, (_, _) => Process.Start("explorer.exe", vm.Store.Dir));
        menu.Add("Recarregar presets", null, (_, _) => vm.Reload());
        menu.Add(new Forms.ToolStripSeparator());
        menu.Add("Sair", null, (_, _) => Application.Current.Shutdown());
    }

    Forms.ToolStripMenuItem Toggle(string text, bool state, Action<bool> set) =>
        new(text, null, (_, _) => { set(!state); vm.Settings.Save(); }) { Checked = state };

    static bool IsAutostart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("WinDock") is not null;
    }

    static void SetAutostart(bool on)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;
        if (on) key.SetValue("WinDock", $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue("WinDock", throwOnMissingValue: false);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
    }
}
