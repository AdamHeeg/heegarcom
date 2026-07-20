namespace heegarcom.Client.Projects;

public sealed class MonolithToNTier : IProjectSource
{
    public int Order => 20;

    public ProjectItem Card => new(
        "Monolith to N-Tier",
        "Broke a 20,000-line monolith into clean layers and unified 200+ data-access files.",
        new[] { "Architecture", "Refactoring", "C# / .NET", "SQL Server" },
        "Case Study",
        Problem: "One 20k-line project; data-access logic copy-pasted across 200+ files.",
        Built: "Extracted UI / business / data tiers; consolidated connections into one base class.",
        Outcome: "Changes land in one place; far lower risk of drift and duplication bugs.",
        Metrics: new[]
        {
            new MetricItem("20k", "lines untangled"),
            new MetricItem("200+→1", "data files unified"),
            new MetricItem("3", "clean layers"),
        },
        Visual: "monolith-split");
}
