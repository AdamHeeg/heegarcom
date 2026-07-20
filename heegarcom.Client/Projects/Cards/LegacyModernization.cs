namespace heegarcom.Client.Projects;

public sealed class LegacyModernization : IProjectSource
{
    public int Order => 10;

    public ProjectItem Card => new()
    {
        Title = "Legacy Modernization",
        Summary = "Drove a healthcare platform from .NET 2 to .NET 10 across a decade — without a rewrite.",
        Tags = new[] { "Modernization", "Architecture", "C# / .NET", "Healthcare" },
        Kind = "Case Study",
        Problem = "Core platform stuck on .NET 2 — mounting security and maintainability risk.",
        Built = "Incremental, in-place framework upgrades under production load — never a stop-the-world rewrite.",
        Outcome = "Current on .NET 10; a decade of uninterrupted delivery.",
        Metrics = new[]
        {
            new MetricItem { Value = "10+ yrs", Label = "continuous ownership" },
            new MetricItem { Value = "6", Label = "framework jumps" },
            new MetricItem { Value = "0", Label = "big-bang rewrites" },
        },
        Visual = "version-trail",
    };
}
