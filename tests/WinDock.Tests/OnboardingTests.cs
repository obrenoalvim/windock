using WinDock.Services;

namespace WinDock.Tests;

public class OnboardingTests
{
    [Fact]
    public void Steps_adapt_to_what_the_pc_has()
    {
        Assert.Equal([WelcomeStep.Hello, WelcomeStep.Look, WelcomeStep.Try, WelcomeStep.Done], Onboarding.Plan(0, 0));
        Assert.Equal([WelcomeStep.Hello, WelcomeStep.Apps, WelcomeStep.Look, WelcomeStep.Try, WelcomeStep.Done], Onboarding.Plan(6, 0));
        Assert.Equal([WelcomeStep.Hello, WelcomeStep.Games, WelcomeStep.Look, WelcomeStep.Try, WelcomeStep.Done], Onboarding.Plan(0, 2));
        Assert.Equal(6, Onboarding.Plan(6, 2).Count);
    }

    [Fact]
    public void Problem_report_carries_the_environment_and_escapes_it()
    {
        var body = ProblemReport.Body(new Version(0, 6, 0), new Version(10, 0, 26300), ["1920x1080 at 100%"], "pt", true);
        Assert.Contains("WinDock 0.6.0", body);
        Assert.Contains("Windows 10.0.26300", body);
        Assert.Contains("1920x1080 at 100%", body);
        var url = ProblemReport.Url(body);
        Assert.StartsWith(ProblemReport.NewIssue + "?body=", url);
        Assert.DoesNotContain(" ", url);
        Assert.DoesNotContain("\n", url);
    }
}
