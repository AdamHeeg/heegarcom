namespace heegarcom.Client.Projects;

public sealed class IcdSnomedMapper : IProjectSource
{
    public int Order => 70;

    public ProjectItem Card => new()
    {
        Title = "ICD-10 ↔ SNOMED Mapper",
        Summary = "Two-way lookup between ophthalmology ICD-10 codes and SNOMED CT concepts — live from a SQLite database.",
        Tags = new[] { "SQLite", "Blazor WASM", "Healthcare", "SNOMED", "Data" },
        Kind = "Demo",
        Url = "/demos/icd-snomed",
    };
}
