namespace WinDock.Services;

public enum WelcomeStep { Hello, Apps, Games, Look, Try, Done }

public static class Onboarding
{
    public static IReadOnlyList<WelcomeStep> Plan(int taskbarApps, int launchers)
    {
        var steps = new List<WelcomeStep> { WelcomeStep.Hello };
        if (taskbarApps > 0) steps.Add(WelcomeStep.Apps);
        if (launchers > 0) steps.Add(WelcomeStep.Games);
        steps.AddRange([WelcomeStep.Look, WelcomeStep.Try, WelcomeStep.Done]);
        return steps;
    }

    public static IReadOnlyList<string> Launchers() =>
        [.. Games.Platforms.Where(p => Games.Launcher(p) is not null || Games.Installed(p).Count > 0)];
}
