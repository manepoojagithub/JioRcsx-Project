namespace JioCxRcsWrapper.UnitTests.MessageBuilder;

public sealed class MessageBuilderPreviewTests
{
    [Fact]
    public void PreviewScript_FallsBackFromLocalMediaToJioCxUrl()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "JioCxRcsWrapper.Web", "wwwroot", "js", "message-builder.js"));

        Assert.Contains("fallbackMediaUrl", script);
        Assert.Contains("#MediaUrl", script);
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
