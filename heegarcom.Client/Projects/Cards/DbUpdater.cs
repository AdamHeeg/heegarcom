namespace heegarcom.Client.Projects;

public sealed class DbUpdater : IProjectSource
{
    public int Order => 50;

    public ProjectItem Card => new()
    {
        Title = "DB Updater - No AI",
        Summary = "A fully self-contained DB migration tool built on DBUp with heavy customization.",
        Tags = new[] { "SQL Server", "C# / .NET", "Tooling", "DevOps" },
        Kind = "Case Study",
        Problem = "As we built new code and expanded the definition of the database, we needed an easy way to manage those migrations. We weren't allowed to use Entity Framework, and we had to support many customizations from VB6 and SQL Server Replication.",
        Built = "Starting with DBUp, I expanded their codebase with custom logic, SQL Server Replication–safe upgrades, and an optional split audit-trail database.",
        Outcome = "It migrates data, updates schema, and manages synchronization rules in a single library exposed by one Run method. My favorite kind of solution: simple, clean, easy to use.",
        Metrics = new[]
        {
            new MetricItem { Value = "141k", Label = "lines of T-SQL" },
            new MetricItem { Value = "29", Label = "schema versions" },
            new MetricItem { Value = "1", Label = "method to run it all" },
        },
    };
}
