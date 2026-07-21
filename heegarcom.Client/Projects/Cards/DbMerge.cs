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
        Problem = "Doctors move between locations and practices. They need access to only their patients and data. This solution provides an easy, in-product way to safely transfer a provider's patient data between systems.",
        Built = "A Win-Forms tool that safely connects to multiple proprietary databases, preserving hashing, auditing, and encryption while transferring the selected data to the new system.",
        Outcome = "Proved 90% faster at exporting and importing a provider's patient data between our systems — turning what used to cost long weekends and lost productivity into a routine task.",
        Metrics = new[]
        {
            new MetricItem { Value = "90%", Label = "faster than manual transfer" },
            new MetricItem { Value = "0", Label = "lost weekends" },
        },
    };
}
