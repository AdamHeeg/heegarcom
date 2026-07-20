namespace heegarcom.Client.Projects;

public sealed class IcdSnomedMapper : IProjectSource
{
    public int Order => 70;

    public ProjectItem Card => new(
        "ICD-10 ↔ SNOMED Mapper",
        "Search a diagnosis and see its ICD-10 and SNOMED CT cross-mappings — running live on this site.",
        new[] { "SQLite", "Blazor WASM", "Healthcare", "Data" },
        "Demo",
        Url: "/demos/icd-snomed");
}
