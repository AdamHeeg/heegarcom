namespace heegarcom.Client.Projects;

public sealed class DbMerge : IProjectSource
{
    public int Order => 60;

    public ProjectItem Card => new()
    {
        Title = "DB Merge - AI With Oversight",
        Summary = "A process for merging data across databases while preserving integrity.",
        Tags = new[] { "SQL Server", "Data Migration", "C# / .NET", "Tooling" },
        Kind = "Case Study",
        Problem = "Doctors move between locations and practices.  They need access to only their patients and data.  This solution provides an easy in product way to safely transfer a provider's patient data between system.",
        Built = "Simple Win-Forms project which safely connects to multiple propritary databases preserving hashing, auditing, and encryption while transfering selected data to the new system.",
        Outcome = "This solution proved 90% faster in exporting and importing provider patient data between our systems solving the problem of either long weekends or missed days of productivity for the clients.",
    };
}
