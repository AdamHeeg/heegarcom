namespace heegarcom.Client.Projects;

public record MetricItem
{
    public required string Value { get; init; }
    public required string Label { get; init; }
}

public record ProjectItem
{
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string[] Tags { get; init; }
    public required string Kind { get; init; }   // "Case Study" or "Demo"

    public string? Problem { get; init; }
    public string? Built { get; init; }
    public string? Outcome { get; init; }
    public string? Url { get; init; }            // demo cards: link to the live page
    public MetricItem[]? Metrics { get; init; }  // optional stat tiles
    public string? Visual { get; init; }         // "version-trail" | "monolith-split"
}

// To add a project card: create a class under Projects/Cards that implements this
// interface. Every implementation is discovered automatically (see ProjectCatalog),
// so there is no central list to update. Order controls display position (low first).
public interface IProjectSource
{
    int Order { get; }
    ProjectItem Card { get; }
}
