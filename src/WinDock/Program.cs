using WinDock.Services;

namespace WinDock;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--watchdog", var pid] && int.TryParse(pid, out var id))
        {
            ShellGuard.Watch(id);
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
