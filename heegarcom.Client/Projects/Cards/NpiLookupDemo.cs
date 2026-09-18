namespace heegarcom.Client.Projects;

public sealed class NpiLookupDemo : IProjectSource
{
    public int Order => 90;

    public ProjectItem Card => new()
    {
        Title = "NPI Provider Lookup",
        Summary = "A forward take on a clunky federal tool. Search providers live by name and state, then "
            + "<strong>expand any result</strong> for that provider's Medicare billing and industry payments "
            + "(CMS Open Payments), each shown with its data year. Plus a <strong>starts-with</strong> name "
            + "search that still catches <strong>maiden and former names</strong>, practice location over "
            + "mailing address, and a state picker that rejects bad input.",
        Tags = new[] { "Blazor WASM", "REST API", "Healthcare", "CMS" },
        Kind = "Demo",
        Url = "/demos/npi",
    };
}
