using WinDock.Models;

namespace WinDock.Tests;

public class WidgetGroupTests
{
    [Fact]
    public void Every_grouped_widget_is_a_real_kind_in_one_group_only()
    {
        var grouped = WidgetSpec.Groups.SelectMany(g => g.Kinds).ToList();
        Assert.Equal(grouped.Count, grouped.Distinct().Count());
        Assert.All(grouped, kind => Assert.Contains(kind, WidgetSpec.Kinds));
    }
}
