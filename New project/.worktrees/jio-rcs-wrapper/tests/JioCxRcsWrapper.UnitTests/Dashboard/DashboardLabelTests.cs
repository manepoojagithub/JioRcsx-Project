namespace JioCxRcsWrapper.UnitTests.Dashboard;

public sealed class DashboardLabelTests
{
    [Fact]
    public void MessagePerformanceLegend_UsesSameStatusesAsChart()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "JioCxRcsWrapper.Web", "Views", "Dashboard", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "src", "JioCxRcsWrapper.Web", "wwwroot", "js", "dashboard.js"));

        string[] statuses = ["Pending", "Sent", "Delivered", "Failed", "Opened", "Clicked"];

        Assert.Contains("Message Status Distribution", view);
        Assert.Contains("Current contact status breakdown", view);

        foreach (var status in statuses)
        {
            Assert.Contains($">{status}</span>", view);
            Assert.Contains($"label: \"{status}\"", script);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JioCxRcsWrapper.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
