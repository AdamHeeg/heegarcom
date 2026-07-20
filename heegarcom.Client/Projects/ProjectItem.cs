namespace heegarcom.Client.Projects;

public record MetricItem(string Value, string Label);

public record ProjectItem(
    string Title,
    string Summary,
    string[] Tags,
    string Kind,
    string? Problem = null,
    string? Built = null,
    string? Outcome = null,
    string? Url = null,
    MetricItem[]? Metrics = null,
    string? Visual = null);

// To add a project card: create a class under Projects/Cards that implements this
// interface. Every implementation is discovered automatically (see ProjectCatalog),
// so there is no central list to update. Order controls display position (low first).
public interface IProjectSource
{
    int Order { get; }
    ProjectItem Card { get; }
}
