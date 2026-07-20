namespace heegarcom.Client.Projects;

public sealed class DbUpdater : IProjectSource
{
    public int Order => 50;

    public ProjectItem Card => new()
    {
        Title = "DB Updater - No AI",
        Summary = "A fully self contained DB Migration tool leveraging DBUp and many customizations.",
        Tags = new[] { "SQL Server", "C# / .NET", "Tooling", "DevOps" },
        Kind = "Case Study",
        Problem = "As we built new code and expanded the defination of the database we needed an easy way to manage those migrations.  We were not allowed to use Entity Framework and we needed to support many customizations from VB6 and SQL Server Replication.",
        Built = @"Starting with DBUp I expanded their code base with custom logic and implemented SQL Server Replication safe upgrades.<br />
        <ul>
        <li>285 files</li>
        <li>16,051 lines of code</li>
        <li>781 SQL files with 141,103 lines of T-SQL </li>
        <li>29 major db versions</li>
        <li>Suppors SQL Replication and an optional split audit trail databases</li>
        </ul>",
        Outcome = "The code migrates data, updates schema, manages syncronization rules all in a single library exposed by a single Run method.  The solution is my favorite kind: simple, clean, easy to use.",
    };
}
