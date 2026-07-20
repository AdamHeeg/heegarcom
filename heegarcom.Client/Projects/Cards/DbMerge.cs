namespace heegarcom.Client.Projects;

public sealed class DbMerge : IProjectSource
{
    public int Order => 60;

    public ProjectItem Card => new(
        "DB Merge",
        "A process for merging data across databases while preserving integrity.",
        new[] { "SQL Server", "Data Migration", "C# / .NET", "Tooling" },
        "Case Study",
        Problem: "[Fill in: why databases needed merging — practices, tenants, acquisitions.]",
        Built: "[Fill in: matching, de-duplication, referential integrity, rollback.]",
        Outcome: "[Fill in: records merged, integrity results, downtime.]");
}
