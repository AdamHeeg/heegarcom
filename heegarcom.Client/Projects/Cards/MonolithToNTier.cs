namespace heegarcom.Client.Projects;

public sealed class MonolithToNTier : IProjectSource
{
    public int Order => 20;

    public ProjectItem Card => new()
    {
        Title = "Monolith to N-Tier",
        Summary = "Broke a ~150,000-line monolith application with 2 super classes into clean layers and unified 200+ data-access files.",
        Tags = new[] { "Architecture", "Refactoring", "C# / .NET", "SQL Server" },
        Kind = "Case Study",
        Problem = "One 150k-line project; data-access, business logic, and custom code copy-pasted across 200+ files needed to be updated into a new .net framework and modernized into a SOLID code base.",
        Built = "Extracted UI / business / data tiers; consolidated connections into one base class.",
        Outcome = "Manageable, expandable code base following SOLID princples; far lower risk of drift and duplication bugs.",
        Metrics = new[]
        {
            new MetricItem { Value = "150k", Label = "lines untangled" },
            new MetricItem { Value = "200+→1", Label = "data files unified" },
            new MetricItem { Value = "3", Label = "clean layers" },
        },
        Visual = "monolith-split",
    };
}
