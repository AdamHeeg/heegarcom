namespace heegarcom.Client.Projects;

public sealed class DbUpdater : IProjectSource
{
    public int Order => 50;

    public ProjectItem Card => new(
        "DB Updater",
        "A tool that applies versioned database changes safely across environments.",
        new[] { "SQL Server", "C# / .NET", "Tooling", "DevOps" },
        "Case Study",
        Problem: "[Fill in: schema drift / risky manual deployments.]",
        Built: "[Fill in: how updates are versioned, validated, and applied.]",
        Outcome: "[Fill in: deployment safety, time saved, environments covered.]");
}
