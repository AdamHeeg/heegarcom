namespace heegarcom.Client.Projects;

public sealed class NpiLookupDemo : IProjectSource
{
    public int Order => 90;

    public ProjectItem Card => new()
    {
        Title = "NPI Provider Lookup",
        Summary = "Search the national registry of U.S. healthcare providers by name and state — live against the federal CMS NPI Registry API.",
        Tags = new[] { "Blazor WASM", "REST API", "Healthcare", "CMS" },
        Kind = "Demo",
        Url = "/demos/npi",
    };
}
