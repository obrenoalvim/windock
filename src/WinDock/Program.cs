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

        if (args is [ClaudeSessions.HookArgument])
        {
            using var input = new StreamReader(Console.OpenStandardInput(), System.Text.Encoding.UTF8);
            ClaudeSessions.Record(input.ReadToEnd(), ClaudeSessions.Dir);
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
