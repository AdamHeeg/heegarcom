using heegarcom.Components;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(heegarcom.Client._Imports).Assembly);

// Bidirectional ICD-10 <-> SNOMED search over the local SQLite DB (read-only).
// Pass either ?icd=... or ?snomed=...; returns the matching mapped pairs.
app.MapGet("/api/terminology/search", (string? icd, string? snomed, IWebHostEnvironment env) =>
{
    var byIcd = (icd ?? "").Trim();
    var bySnomed = (snomed ?? "").Trim();
    var icdMode = byIcd.Length >= 2;
    var term = icdMode ? byIcd : bySnomed;
    if (term.Length < 2)
        return Results.Ok(Array.Empty<object>());

    var dbPath = Path.Combine(env.ContentRootPath, "Data", "terminology.db");
    using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    using var cmd = conn.CreateCommand();
    if (icdMode)
    {
        cmd.CommandText = @"
            SELECT i.code_dotted, i.description, c.snomed_id, c.term
            FROM icd_snomed_map m
            JOIN icd10 i ON i.code = m.icd10_code
            JOIN snomed_concept c ON c.snomed_id = m.snomed_id
            WHERE i.code LIKE $code || '%' OR i.description LIKE '%' || $q || '%'
            ORDER BY i.code, c.term
            LIMIT 100";
        cmd.Parameters.AddWithValue("$code", term.Replace(".", "").ToUpperInvariant());
        cmd.Parameters.AddWithValue("$q", term);
    }
    else
    {
        cmd.CommandText = @"
            SELECT i.code_dotted, i.description, c.snomed_id, c.term
            FROM icd_snomed_map m
            JOIN icd10 i ON i.code = m.icd10_code
            JOIN snomed_concept c ON c.snomed_id = m.snomed_id
            WHERE c.snomed_id LIKE $q || '%' OR c.term LIKE '%' || $q || '%'
            ORDER BY c.term, i.code
            LIMIT 100";
        cmd.Parameters.AddWithValue("$q", term);
    }

    var rows = new List<MapRow>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
        rows.Add(new MapRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));

    return Results.Ok(rows);
});

app.Run();

record MapRow(string IcdCode, string IcdDescription, string SnomedId, string SnomedTerm);
