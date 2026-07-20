namespace heegarcom.Client.Projects;

public sealed class MonolithToNTier : IProjectSource
{
    public int Order => 20;

    public ProjectItem Card => new()
    {
        Title = "Monolith to N-Tier",
        Summary = "Broke a 20,000-line monolith into clean layers and unified 200+ data-access files.",
        Tags = new[] { "Architecture", "Refactoring", "C# / .NET", "SQL Server" },
        Kind = "Case Study",
        Problem = "One 20k-line project; data-access logic copy-pasted across 200+ files.",
        Built = "Extracted UI / business / data tiers; consolidated connections into one base class.",
        Outcome = "Changes land in one place; far lower risk of drift and duplication bugs.",
        Metrics = new[]
        {
            new MetricItem { Value = "20k", Label = "lines untangled" },
            new MetricItem { Value = "200+→1", Label = "data files unified" },
            new MetricItem { Value = "3", Label = "clean layers" },
        },
        Visual = "monolith-split",
    };
}
