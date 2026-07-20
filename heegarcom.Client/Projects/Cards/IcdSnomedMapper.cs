namespace heegarcom.Client.Projects;

public sealed class IcdSnomedMapper : IProjectSource
{
    public int Order => 70;

    public ProjectItem Card => new()
    {
        Title = "ICD-10 ↔ SNOMED Mapper",
        Summary = "Search a diagnosis and see its ICD-10 and SNOMED CT cross-mappings — running live on this site.",
        Tags = new[] { "SQLite", "Blazor WASM", "Healthcare", "Data" },
        Kind = "Demo",
        Url = "/demos/icd-snomed",
    };
}
