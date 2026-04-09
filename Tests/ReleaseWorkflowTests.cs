namespace MouseTool.Tests;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void ReleaseWorkflow_UploadsUpdateManifestToGitHubRelease()
    {
        var repoRoot = TestPathHelper.FindRepositoryRoot();
        var workflowPath = Path.Combine(repoRoot, ".github", "workflows", "release.yml");
        var workflowContent = File.ReadAllText(workflowPath);

        Assert.Contains("release/update.json", workflowContent, StringComparison.Ordinal);
        Assert.Contains("release/MouseTool-CHANGELOG.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("release/MouseTool-Setup.exe", workflowContent, StringComparison.Ordinal);
        Assert.Contains("release/MouseTool-win-x64.zip", workflowContent, StringComparison.Ordinal);
    }
}
