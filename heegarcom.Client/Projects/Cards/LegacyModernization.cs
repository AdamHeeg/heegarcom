namespace heegarcom.Client.Projects;

public sealed class LegacyModernization : IProjectSource
{
    public int Order => 10;

    public ProjectItem Card => new(
        "Legacy Modernization",
        "Drove a healthcare platform from .NET 2 to .NET 10 across a decade — without a rewrite.",
        new[] { "Modernization", "Architecture", "C# / .NET", "Healthcare" },
        "Case Study",
        Problem: "Core platform stuck on .NET 2 — mounting security and maintainability risk.",
        Built: "Incremental, in-place framework upgrades under production load — never a stop-the-world rewrite.",
        Outcome: "Current on .NET 10; a decade of uninterrupted delivery.",
        Metrics: new[]
        {
            new MetricItem("10+ yrs", "continuous ownership"),
            new MetricItem("6", "framework jumps"),
            new MetricItem("0", "big-bang rewrites"),
        },
        Visual: "version-trail");
}
